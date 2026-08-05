using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using DesoutterSimulator.Core;
using DesoutterSimulator.Protocol;
using DesoutterSimulator.Utils;

namespace DesoutterSimulator
{
    public class Simulator
    {
        private TcpListener _listener;
        private bool _isRunning;
        private readonly ConcurrentDictionary<string, ClientSession> _sessions;
        private readonly DataGenerator _dataGenerator;

        public Simulator()
        {
            _sessions = new ConcurrentDictionary<string, ClientSession>();
            _dataGenerator = new DataGenerator();
            _dataGenerator.TighteningGenerated += OnTighteningGenerated;
            _dataGenerator.AlarmGenerated += OnAlarmGenerated;
        }

        public async Task StartAsync()
        {
            _isRunning = true;
            _listener = new TcpListener(IPAddress.Any, 4545);
            _listener.Start();
            Logger.Info($"服务器已启动，监听端口 4545");
            _dataGenerator.Start();

            while (_isRunning)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    var clientId = $"{client.Client.RemoteEndPoint}";
                    Logger.Info($"客户端连接: {clientId}");

                    var session = new ClientSession(client, this);
                    _sessions.TryAdd(clientId, session);
                    _ = session.HandleClientAsync();
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                        Logger.Error($"接受连接时出错: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _dataGenerator.Stop();
            _listener?.Stop();
            foreach (var session in _sessions.Values)
            {
                session.Disconnect();
            }
            _sessions.Clear();
            Logger.Info("模拟器已停止");
        }

        public async Task SendMessageAsync(string clientId, Message message)
        {
            if (_sessions.TryGetValue(clientId, out var session))
            {
                await session.SendMessageAsync(message);
            }
        }

        private void OnTighteningGenerated(object sender, TighteningResult result)
        {
            // 遍历所有会话，向订阅了 LastTightening 的会话发送结果
            foreach (var session in _sessions.Values)
            {
                if (session.SubscriptionManager.HasSubscription("LastTightening"))
                {
                    var rev = session.LastTighteningRevision > 0 ? session.LastTighteningRevision : 1;
                    var msg = MessageFactory.CreateLastTighteningResult(result, rev);
                    _ = session.SendMessageAsync(msg);
                }
            }
        }

        private void OnAlarmGenerated(object sender, Alarm alarm)
        {
            foreach (var session in _sessions.Values)
            {
                if (session.SubscriptionManager.HasSubscription("Alarm"))
                {
                    if (session.ControllerState.HasActiveAlarm())
                        continue; // 已有报警，不覆盖
                    session.ControllerState.SetAlarm(alarm);
                    var msg = MessageFactory.CreateAlarm(alarm);
                    _ = session.SendMessageAsync(msg);
                }
            }
        }

        public class ClientSession
        {
            private readonly TcpClient _client;
            private readonly Simulator _simulator;
            private NetworkStream _stream;
            private bool _isConnected;
            private readonly string _clientId;
            private readonly StringBuilder _messageBuffer;

            // 每个会话独立的状态
            public ControllerState ControllerState { get; }
            public SubscriptionManager SubscriptionManager { get; }
            public int LastTighteningRevision { get; set; }

            public ClientSession(TcpClient client, Simulator simulator)
            {
                _client = client;
                _simulator = simulator;
                _stream = client.GetStream();
                _isConnected = true;
                _clientId = $"{client.Client.RemoteEndPoint}";
                _messageBuffer = new StringBuilder();
                ControllerState = new ControllerState();
                SubscriptionManager = new SubscriptionManager();
                LastTighteningRevision = 1;
            }

            public async Task HandleClientAsync()
            {
                try
                {
                    var buffer = new byte[8192];
                    while (_isConnected && _client.Connected)
                    {
                        var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            Logger.Info($"客户端 {_clientId} 断开连接");
                            break;
                        }

                        var received = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        _messageBuffer.Append(received);

                        // 处理完整消息（以 NUL 结尾）
                        while (_messageBuffer.Length > 0)
                        {
                            var nulIndex = _messageBuffer.ToString().IndexOf('\0');
                            if (nulIndex < 0) break;

                            var messageStr = _messageBuffer.ToString(0, nulIndex);
                            _messageBuffer.Remove(0, nulIndex + 1);

                            if (!string.IsNullOrEmpty(messageStr))
                            {
                                await ProcessMessageAsync(messageStr);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"客户端 {_clientId} 处理出错: {ex.Message}");
                }
                finally
                {
                    Disconnect();
                    _simulator._sessions.TryRemove(_clientId, out _);
                }
            }

            private async Task ProcessMessageAsync(string messageStr)
            {
                try
                {
                    var message = MessageParser.Parse(messageStr);
                    if (message.MID == 9999)
                    {
                        await SendMessageAsync(message);
                        return;
                    }

                    var response = await _simulator.HandleMessageAsync(message, this);
                    if (response != null)
                    {
                        await SendMessageAsync(response);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"处理消息时出错: {ex.Message}");
                }
            }

            public async Task SendMessageAsync(Message message)
            {
                try
                {
                    var data = message.ToByteArray();
                    Logger.Debug($"发送 {message.MID} 修订版 {message.Revision}，长度 {data.Length}，数据: {message.DataField}");
                    await _stream.WriteAsync(data, 0, data.Length);
                }
                catch (Exception ex)
                {
                    Logger.Error($"发送消息时出错: {ex.Message}");
                }
            }

            public void Disconnect()
            {
                _isConnected = false;
                _stream?.Dispose();
                _client?.Close();
            }
        }

        private async Task<Message> HandleMessageAsync(Message request, ClientSession session)
        {
            var state = session.ControllerState;
            var subs = session.SubscriptionManager;

            // 检查通信是否已启动（除了 MID 0001 和 0003 外）
            if (request.MID != 1 && request.MID != 3 && !state.CommunicationStarted)
            {
                return MessageFactory.CreateCommandError(request.MID, 16); // Connection rejected protocol busy
            }

            switch (request.MID)
            {
                case 61:   // MID 0061 Last tightening result data (controller -> client)
                case 65:   // MID 0065 Old tightening result upload reply
                case 71:   // MID 0071 Alarm
                case 74:   // MID 0074 Alarm acknowledged on controller
                case 76:   // MID 0076 Alarm status
                case 101:  // MID 0101 Multi-spindle result
                case 106:  // MID 0106 Station data
                case 107:  // MID 0107 Bolt data
                    Logger.Warning($"客户端不应发送事件 MID: {request.MID}");
                    return MessageFactory.CreateCommandError(request.MID, 99); // Unknown MID
                // ===== 通信消息 =====
                case 1: return HandleCommunicationStart(request, state);
                case 3: return HandleCommunicationStop(request, state);

                // ===== 参数集消息 =====
                case 10: return HandleParameterSetIDUploadRequest(request, state);
                case 12: return HandleParameterSetDataUploadRequest(request, state);
                case 14: return HandleParameterSetSelectedSubscribe(request, state, subs);
                case 17: return HandleParameterSetSelectedUnsubscribe(request, state, subs);
                case 18: return HandleSelectParameterSet(request, state);

                // ===== 拧紧结果 =====
                case 60: return HandleLastTighteningSubscribe(request, session, subs);
                case 63: return HandleLastTighteningUnsubscribe(request, subs);
                case 64: return HandleOldTighteningUploadRequest(request);

                // ===== 报警 =====
                case 70: return HandleAlarmSubscribe(request, state, subs);
                case 73: return HandleAlarmUnsubscribe(request, subs);
                case 78: return HandleAcknowledgeAlarm(request, state);

                // ===== 工具 =====
                case 40: return HandleToolDataUploadRequest(request);
                case 42: return HandleDisableTool(request, state);
                case 43: return HandleEnableTool(request, state);

                // ===== Job =====
                case 30: return HandleJobIDUploadRequest(request, state);
                case 34: return HandleJobInfoSubscribe(request, state, subs);
                case 37: return HandleJobInfoUnsubscribe(request, subs);
                case 38: return HandleSelectJob(request, state);

                // ===== VIN =====
                case 51: return HandleVINSubscribe(request, state, subs);
                case 54: return HandleVINUnsubscribe(request, subs);

                // ===== 时间 =====
                case 80: return HandleReadTimeRequest(request);

                // ===== 控制器 =====
                case 270: return HandleControllerReboot(request, state, subs);

                default:
                    Logger.Warning($"未处理的 MID: {request.MID}");
                    return MessageFactory.CreateCommandError(request.MID, 99);
            }
        }

        #region 消息处理器

        private Message HandleCommunicationStart(Message request, ControllerState state)
        {
            if (state.CommunicationStarted)
                return MessageFactory.CreateCommandError(request.MID, 96);
            state.CommunicationStarted = true;
            var rev = request.Revision > 0 ? request.Revision : 1;
            return MessageFactory.CreateCommunicationStartAcknowledge(rev);
        }

        private Message HandleCommunicationStop(Message request, ControllerState state)
        {
            state.CommunicationStarted = false;
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleParameterSetIDUploadRequest(Message request, ControllerState state)
        {
            var ids = state.GetParameterSetIDs();
            return MessageFactory.CreateParameterSetIDUploadReply(ids);
        }

        private Message HandleParameterSetDataUploadRequest(Message request, ControllerState state)
        {
            var data = request.DataField;
            if (data == null || data.Length < 3)
                return MessageFactory.CreateCommandError(request.MID, 1);

            if (!int.TryParse(data.Substring(0, 3), out int psetId))
                return MessageFactory.CreateCommandError(request.MID, 1);

            var pset = state.GetParameterSet(psetId);
            if (pset == null)
                return MessageFactory.CreateCommandError(request.MID, 2);

            var rev = request.Revision > 0 ? request.Revision : 1;
            return MessageFactory.CreateParameterSetDataUploadReply(pset, rev);
        }

        private Message HandleParameterSetSelectedSubscribe(Message request, ControllerState state, SubscriptionManager subs)
        {
            if (subs.HasSubscription("ParameterSetSelected"))
                return MessageFactory.CreateCommandError(request.MID, 13);

            subs.AddSubscription("ParameterSetSelected", request.NoAckFlag == 1);
            var pset = state.GetCurrentParameterSet();
            return MessageFactory.CreateParameterSetSelected(pset);
        }

        private Message HandleParameterSetSelectedUnsubscribe(Message request, ControllerState state, SubscriptionManager subs)
        {
            if (!subs.HasSubscription("ParameterSetSelected"))
                return MessageFactory.CreateCommandError(request.MID, 14);
            subs.RemoveSubscription("ParameterSetSelected");
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleSelectParameterSet(Message request, ControllerState state)
        {
            var data = request.DataField;
            if (data == null || data.Length < 3)
                return MessageFactory.CreateCommandError(request.MID, 1);

            if (!int.TryParse(data.Substring(0, 3), out int psetId))
                return MessageFactory.CreateCommandError(request.MID, 1);

            if (!state.SelectParameterSet(psetId))
                return MessageFactory.CreateCommandError(request.MID, 3);

            // 如果有订阅，推送新的参数集选择
            // 这里简化，实际应触发事件

            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleLastTighteningSubscribe(Message request, ClientSession session, SubscriptionManager subs)
        {
            if (subs.HasSubscription("LastTightening"))
                return MessageFactory.CreateCommandError(request.MID, 9);

            // 支持的修订版本列表（包含客户端自定义的版本 7）
            int[] supportedRevisions = { 1, 2, 3, 4, 5, 6, 7, 998, 999 };
            int rev = request.Revision > 0 ? request.Revision : 1;

            if (!Array.Exists(supportedRevisions, r => r == rev))
            {
                Logger.Warning($"客户端请求不支持的修订版本 {rev}，自动降级为修订版本 1");
                rev = 1;
            }

            subs.AddSubscription("LastTightening", request.NoAckFlag == 1);
            session.LastTighteningRevision = rev;
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleLastTighteningUnsubscribe(Message request, SubscriptionManager subs)
        {
            if (!subs.HasSubscription("LastTightening"))
                return MessageFactory.CreateCommandError(request.MID, 10);
            subs.RemoveSubscription("LastTightening");
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleOldTighteningUploadRequest(Message request)
        {
            var data = request.DataField;
            if (data == null || data.Length < 10)
                return MessageFactory.CreateCommandError(request.MID, 1);

            if (!long.TryParse(data, out long tid))
                return MessageFactory.CreateCommandError(request.MID, 1);

            var result = DataGenerator.GetTighteningResult(tid);
            if (result == null)
                return MessageFactory.CreateCommandError(request.MID, 15);

            var rev = request.Revision > 0 ? request.Revision : 1;
            return MessageFactory.CreateOldTighteningUploadReply(result, rev);
        }

        private Message HandleAlarmSubscribe(Message request, ControllerState state, SubscriptionManager subs)
        {
            if (subs.HasSubscription("Alarm"))
                return MessageFactory.CreateCommandError(request.MID, 11);

            subs.AddSubscription("Alarm", request.NoAckFlag == 1);

            if (state.HasActiveAlarm())
                return MessageFactory.CreateAlarmStatus(state.GetActiveAlarm());

            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleAlarmUnsubscribe(Message request, SubscriptionManager subs)
        {
            if (!subs.HasSubscription("Alarm"))
                return MessageFactory.CreateCommandError(request.MID, 12);
            subs.RemoveSubscription("Alarm");
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleAcknowledgeAlarm(Message request, ControllerState state)
        {
            if (!state.HasActiveAlarm())
                return MessageFactory.CreateCommandError(request.MID, 58);
            state.AcknowledgeAlarm();
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleToolDataUploadRequest(Message request)
        {
            var tool = new ToolData();
            var rev = request.Revision > 0 ? request.Revision : 1;
            return MessageFactory.CreateToolDataUploadReply(tool, rev);
        }

        private Message HandleDisableTool(Message request, ControllerState state)
        {
            state.ToolEnabled = false;
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleEnableTool(Message request, ControllerState state)
        {
            state.ToolEnabled = true;
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleJobIDUploadRequest(Message request, ControllerState state)
        {
            var ids = state.GetJobIDs();
            var rev = request.Revision > 0 ? request.Revision : 1;
            return MessageFactory.CreateJobIDUploadReply(ids, rev);
        }

        private Message HandleJobInfoSubscribe(Message request, ControllerState state, SubscriptionManager subs)
        {
            if (subs.HasSubscription("JobInfo"))
                return MessageFactory.CreateCommandError(request.MID, 18);

            subs.AddSubscription("JobInfo", request.NoAckFlag == 1);
            var info = state.GetCurrentJobInfo();
            return MessageFactory.CreateJobInfo(info);
        }

        private Message HandleJobInfoUnsubscribe(Message request, SubscriptionManager subs)
        {
            if (!subs.HasSubscription("JobInfo"))
                return MessageFactory.CreateCommandError(request.MID, 19);
            subs.RemoveSubscription("JobInfo");
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleSelectJob(Message request, ControllerState state)
        {
            var data = request.DataField;
            if (data == null || data.Length < 4)
                return MessageFactory.CreateCommandError(request.MID, 1);

            if (!int.TryParse(data, out int jobId))
                return MessageFactory.CreateCommandError(request.MID, 1);

            if (!state.SelectJob(jobId))
                return MessageFactory.CreateCommandError(request.MID, 20);

            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleVINSubscribe(Message request, ControllerState state, SubscriptionManager subs)
        {
            if (subs.HasSubscription("VIN"))
                return MessageFactory.CreateCommandError(request.MID, 6);

            subs.AddSubscription("VIN", request.NoAckFlag == 1);
            var vin = state.GetVINData();
            var rev = request.Revision > 0 ? request.Revision : 1;
            return MessageFactory.CreateVehicleIDNumber(vin, rev);
        }

        private Message HandleVINUnsubscribe(Message request, SubscriptionManager subs)
        {
            if (!subs.HasSubscription("VIN"))
                return MessageFactory.CreateCommandError(request.MID, 7);
            subs.RemoveSubscription("VIN");
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleReadTimeRequest(Message request)
        {
            return MessageFactory.CreateReadTimeUploadReply(DateTime.Now);
        }

        private Message HandleControllerReboot(Message request, ControllerState state, SubscriptionManager subs)
        {
            state.CommunicationStarted = false;
            subs.ClearAll();
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        #endregion
    }
}