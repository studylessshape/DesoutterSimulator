using DesoutterSimulator.Protocol;
using DesoutterSimulatorWpf.Services.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DesoutterSimulatorWpf.Services
{
    public class SimulatorEngine
    {
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private int _port;
        private readonly ControllerState _state = new ControllerState();
        private readonly SubscriptionManager _subs = new SubscriptionManager();
        private int _currentSubscribedRevision = 1;
        private NetworkStream _currentStream;
        private readonly object _streamLock = new object();

        public event EventHandler<StateEventArgs> StateChanged;
        public event EventHandler<TighteningResult> TighteningGenerated;
        public event EventHandler<string>? MessageLogged;

        public class StateEventArgs : EventArgs
        {
            public bool IsConnected { get; set; }
            public bool IsEnabled { get; set; }
            public int CurrentPsetId { get; set; }
            public string LastSubscription { get; set; }
        }

        public bool IsRunning => _isRunning;

        /// <summary>监听端口，可在启动前修改，启动时生效。</summary>
        public int Port
        {
            get => _port;
            set => _port = value;
        }

        public SimulatorEngine(int port) => _port = port;

        public async Task StartAsync()
        {
            if (_isRunning) return;
            _cts = new CancellationTokenSource();

            // 端口被占用等异常会在此处抛出，由调用方处理（_isRunning 保持 false）
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            _listener = listener;
            _isRunning = true;

            // 启动监听但尚无客户端连接，上报未连接
            RaiseStateChanged(isConnected: false);

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client, _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                // 日志可忽略
            }
            finally
            {
                _listener.Stop();
                _isRunning = false;
                RaiseStateChanged(isConnected: false);
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
            lock (_streamLock) { _currentStream = null; }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                lock (_streamLock) { _currentStream = stream; }

                // 收到客户端连接，上报已连接
                RaiseStateChanged(isConnected: true);

                var buffer = new byte[8192];
                var messageBuffer = new StringBuilder();

                try
                {
                    while (!token.IsCancellationRequested && client.Connected)
                    {
                        int bytesRead;
                        try { bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token); }
                        catch { break; }
                        if (bytesRead == 0) break;

                        var received = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        messageBuffer.Append(received);

                        while (messageBuffer.Length > 0)
                        {
                            int nulIdx = messageBuffer.ToString().IndexOf('\0');
                            if (nulIdx < 0) break;
                            var msgStr = messageBuffer.ToString(0, nulIdx);
                            messageBuffer.Remove(0, nulIdx + 1);

                            if (!string.IsNullOrEmpty(msgStr))
                            {
                                var response = await ProcessMessageAsync(msgStr);
                                if (response != null)
                                {
                                    var data = response.ToByteArray();
                                    await stream.WriteAsync(data, 0, data.Length, token);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    // 客户端断开，重置状态
                    _state.CommunicationStarted = false;
                    _subs.ClearAll();
                    _currentSubscribedRevision = 1;
                    lock (_streamLock) { _currentStream = null; }
                    RaiseStateChanged(isConnected: false);
                }
            }
        }

        private async Task<Message> ProcessMessageAsync(string msgStr)
        {
            try
            {
                var msg = MessageParser.Parse(msgStr);
                MessageLogged?.Invoke(this, $"收到 MID {msg.MID:D4}({MidDisplayName(msg.MID)}) 修订{msg.Revision} 数据='{msg.DataField}'");
                if (msg.MID == 9999) return msg; // Keep alive

                // 处理请求
                var response = await HandleMessageAsync(msg);
                if (response != null)
                    MessageLogged?.Invoke(this, $"响应 MID {response.MID:D4}({MidDisplayName(response.MID)}) 修订{response.Revision} 数据='{response.DataField}'");
                return response;
            }
            catch
            {
                return null;
            }
        }

        private static string MidDisplayName(int mid) => mid switch
        {
            1 => "通信启动", 2 => "通信启动确认", 3 => "通信停止", 4 => "命令错误", 5 => "命令接受",
            10 => "参数集ID上传请求", 11 => "参数集ID上传回复", 12 => "参数集数据上传请求", 13 => "参数集数据上传回复",
            14 => "订阅参数集选中", 15 => "参数集选中", 17 => "退订参数集选中", 18 => "设定程序号",
            30 => "Job ID上传请求", 31 => "Job ID上传回复", 34 => "订阅Job信息", 35 => "Job信息",
            37 => "退订Job信息", 38 => "选择Job", 40 => "工具数据上传请求", 41 => "工具数据上传回复",
            42 => "下使能", 43 => "上使能", 51 => "订阅VIN", 52 => "VIN号", 54 => "退订VIN",
            60 => "订阅拧紧结果", 61 => "拧紧结果", 63 => "退订拧紧结果", 64 => "旧拧紧结果请求",
            65 => "旧拧紧结果回复", 70 => "订阅报警", 71 => "报警", 73 => "退订报警", 74 => "报警确认",
            76 => "报警状态", 78 => "确认报警", 80 => "读时间", 81 => "时间回复", 270 => "控制器重启",
            9999 => "心跳",
            _ => "未知"
        };

        private Task<Message> HandleMessageAsync(Message request)
        {
            // 除通信启动/停止外，其余消息要求通信已启动
            if (request.MID != 1 && request.MID != 3 && !_state.CommunicationStarted)
                return Task.FromResult(MessageFactory.CreateCommandError(request.MID, 16));

            switch (request.MID)
            {
                // ===== 事件类消息，客户端不应发送 =====
                case 61:   // MID 0061 Last tightening result data
                case 65:   // MID 0065 Old tightening result upload reply
                case 71:   // MID 0071 Alarm
                case 74:   // MID 0074 Alarm acknowledged on controller
                case 76:   // MID 0076 Alarm status
                case 101:  // MID 0101 Multi-spindle result
                case 106:  // MID 0106 Station data
                case 107:  // MID 0107 Bolt data
                    return Task.FromResult(MessageFactory.CreateCommandError(request.MID, 99));

                // ===== 通信消息 =====
                case 1: return Task.FromResult(HandleCommunicationStart(request));
                case 3: return Task.FromResult(HandleCommunicationStop(request));

                // ===== 参数集消息 =====
                case 10: return Task.FromResult(HandleParameterSetIDUploadRequest());
                case 12: return Task.FromResult(HandleParameterSetDataUploadRequest(request));
                case 14: return Task.FromResult(HandleParameterSetSelectedSubscribe(request));
                case 17: return Task.FromResult(HandleParameterSetSelectedUnsubscribe(request));
                case 18: return Task.FromResult(HandleSelectParameterSet(request));

                // ===== 拧紧结果 =====
                case 60: return Task.FromResult(HandleLastTighteningSubscribe(request));
                case 63: return Task.FromResult(HandleLastTighteningUnsubscribe(request));
                case 64: return Task.FromResult(HandleOldTighteningUploadRequest(request));

                // ===== 报警 =====
                case 70: return Task.FromResult(HandleAlarmSubscribe(request));
                case 73: return Task.FromResult(HandleAlarmUnsubscribe(request));
                case 78: return Task.FromResult(HandleAcknowledgeAlarm(request));

                // ===== 工具 =====
                case 40: return Task.FromResult(HandleToolDataUploadRequest(request));
                case 42: return Task.FromResult(HandleDisableTool(request));
                case 43: return Task.FromResult(HandleEnableTool(request));

                // ===== Job =====
                case 30: return Task.FromResult(HandleJobIDUploadRequest(request));
                case 34: return Task.FromResult(HandleJobInfoSubscribe(request));
                case 37: return Task.FromResult(HandleJobInfoUnsubscribe(request));
                case 38: return Task.FromResult(HandleSelectJob(request));

                // ===== VIN =====
                case 51: return Task.FromResult(HandleVINSubscribe(request));
                case 54: return Task.FromResult(HandleVINUnsubscribe(request));

                // ===== 时间 =====
                case 80: return Task.FromResult(HandleReadTimeRequest());

                // ===== 控制器 =====
                case 270: return Task.FromResult(HandleControllerReboot());

                default:
                    return Task.FromResult(MessageFactory.CreateCommandError(request.MID, 99));
            }
        }

        #region 消息处理器

        private Message HandleCommunicationStart(Message request)
        {
            if (_state.CommunicationStarted)
                return MessageFactory.CreateCommandError(request.MID, 96);
            _state.CommunicationStarted = true;
            int rev = request.Revision > 0 ? request.Revision : 1;
            return MessageFactory.CreateCommunicationStartAcknowledge(rev);
        }

        private Message HandleCommunicationStop(Message request)
        {
            _state.CommunicationStarted = false;
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleParameterSetIDUploadRequest()
        {
            var ids = _state.GetParameterSetIDs();
            return MessageFactory.CreateParameterSetIDUploadReply(ids);
        }

        private Message HandleParameterSetDataUploadRequest(Message request)
        {
            var data = request.DataField;
            if (data == null || data.Length < 3)
                return MessageFactory.CreateCommandError(request.MID, 1);

            if (!int.TryParse(data.Substring(0, 3), out int psetId))
                return MessageFactory.CreateCommandError(request.MID, 1);

            var pset = _state.GetParameterSet(psetId);
            if (pset == null)
                return MessageFactory.CreateCommandError(request.MID, 2);

            int rev = request.Revision > 0 ? request.Revision : 1;
            return MessageFactory.CreateParameterSetDataUploadReply(pset, rev);
        }

        private Message HandleParameterSetSelectedSubscribe(Message request)
        {
            if (_subs.HasSubscription("ParameterSetSelected"))
                return MessageFactory.CreateCommandError(request.MID, 13);

            _subs.AddSubscription("ParameterSetSelected", request.NoAckFlag == 1);
            var pset = _state.GetCurrentParameterSet();
            RaiseStateChanged();
            return MessageFactory.CreateParameterSetSelected(pset);
        }

        private Message HandleParameterSetSelectedUnsubscribe(Message request)
        {
            if (!_subs.HasSubscription("ParameterSetSelected"))
                return MessageFactory.CreateCommandError(request.MID, 14);
            _subs.RemoveSubscription("ParameterSetSelected");
            RaiseStateChanged();
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleSelectParameterSet(Message request)
        {
            var data = request.DataField;
            if (data == null || data.Length < 3)
                return MessageFactory.CreateCommandError(request.MID, 1);

            if (!int.TryParse(data.Substring(0, 3), out int psetId))
                return MessageFactory.CreateCommandError(request.MID, 1);

            if (!_state.SelectParameterSet(psetId))
                return MessageFactory.CreateCommandError(request.MID, 3);

            RaiseStateChanged();
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleLastTighteningSubscribe(Message request)
        {
            if (_subs.HasSubscription("LastTightening"))
                return MessageFactory.CreateCommandError(request.MID, 9);

            // 支持的修订版本列表（包含自定义版本 7）
            int[] supportedRevisions = { 1, 2, 3, 4, 5, 6, 7, 998, 999 };
            int rev = request.Revision > 0 ? request.Revision : 1;
            if (!Array.Exists(supportedRevisions, r => r == rev))
                rev = 1;

            _subs.AddSubscription("LastTightening", request.NoAckFlag == 1);
            _currentSubscribedRevision = rev;
            RaiseStateChanged();
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleLastTighteningUnsubscribe(Message request)
        {
            if (!_subs.HasSubscription("LastTightening"))
                return MessageFactory.CreateCommandError(request.MID, 10);
            _subs.RemoveSubscription("LastTightening");
            RaiseStateChanged();
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleOldTighteningUploadRequest(Message request)
        {
            var data = request.DataField;
            if (data == null || data.Length < 10)
                return MessageFactory.CreateCommandError(request.MID, 1);

            if (!long.TryParse(data, out _))
                return MessageFactory.CreateCommandError(request.MID, 1);

            // 模拟器无历史数据，返回错误 15（数据不存在）
            return MessageFactory.CreateCommandError(request.MID, 15);
        }

        private Message HandleAlarmSubscribe(Message request)
        {
            if (_subs.HasSubscription("Alarm"))
                return MessageFactory.CreateCommandError(request.MID, 11);

            _subs.AddSubscription("Alarm", request.NoAckFlag == 1);
            RaiseStateChanged();

            if (_state.HasActiveAlarm())
                return MessageFactory.CreateAlarmStatus(_state.GetActiveAlarm());

            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleAlarmUnsubscribe(Message request)
        {
            if (!_subs.HasSubscription("Alarm"))
                return MessageFactory.CreateCommandError(request.MID, 12);
            _subs.RemoveSubscription("Alarm");
            RaiseStateChanged();
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleAcknowledgeAlarm(Message request)
        {
            if (!_state.HasActiveAlarm())
                return MessageFactory.CreateCommandError(request.MID, 58);
            _state.AcknowledgeAlarm();
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleToolDataUploadRequest(Message request)
        {
            var tool = _state.GetToolData();
            int rev = request.Revision > 0 ? request.Revision : 1;
            return MessageFactory.CreateToolDataUploadReply(tool, rev);
        }

        private Message HandleDisableTool(Message request)
        {
            _state.ToolEnabled = false;
            RaiseStateChanged();
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleEnableTool(Message request)
        {
            _state.ToolEnabled = true;
            RaiseStateChanged();
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleJobIDUploadRequest(Message request)
        {
            var ids = _state.GetJobIDs();
            int rev = request.Revision > 0 ? request.Revision : 1;
            return MessageFactory.CreateJobIDUploadReply(ids, rev);
        }

        private Message HandleJobInfoSubscribe(Message request)
        {
            if (_subs.HasSubscription("JobInfo"))
                return MessageFactory.CreateCommandError(request.MID, 18);

            _subs.AddSubscription("JobInfo", request.NoAckFlag == 1);
            var info = _state.GetCurrentJobInfo();
            RaiseStateChanged();
            return MessageFactory.CreateJobInfo(info);
        }

        private Message HandleJobInfoUnsubscribe(Message request)
        {
            if (!_subs.HasSubscription("JobInfo"))
                return MessageFactory.CreateCommandError(request.MID, 19);
            _subs.RemoveSubscription("JobInfo");
            RaiseStateChanged();
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleSelectJob(Message request)
        {
            var data = request.DataField;
            if (data == null || data.Length < 4)
                return MessageFactory.CreateCommandError(request.MID, 1);

            if (!int.TryParse(data, out int jobId))
                return MessageFactory.CreateCommandError(request.MID, 1);

            if (!_state.SelectJob(jobId))
                return MessageFactory.CreateCommandError(request.MID, 20);

            RaiseStateChanged();
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleVINSubscribe(Message request)
        {
            if (_subs.HasSubscription("VIN"))
                return MessageFactory.CreateCommandError(request.MID, 6);

            _subs.AddSubscription("VIN", request.NoAckFlag == 1);
            var vin = _state.GetVINData();
            int rev = request.Revision > 0 ? request.Revision : 1;
            RaiseStateChanged();
            return MessageFactory.CreateVehicleIDNumber(vin, rev);
        }

        private Message HandleVINUnsubscribe(Message request)
        {
            if (!_subs.HasSubscription("VIN"))
                return MessageFactory.CreateCommandError(request.MID, 7);
            _subs.RemoveSubscription("VIN");
            RaiseStateChanged();
            return MessageFactory.CreateCommandAccepted(request.MID);
        }

        private Message HandleReadTimeRequest()
        {
            return MessageFactory.CreateReadTimeUploadReply(DateTime.Now);
        }

        private Message HandleControllerReboot()
        {
            _state.CommunicationStarted = false;
            _subs.ClearAll();
            _currentSubscribedRevision = 1;
            RaiseStateChanged();
            return MessageFactory.CreateCommandAccepted(270);
        }

        #endregion

        public void SendTighteningResult(TighteningResult result)
        {
            if (!_subs.HasSubscription("LastTightening")) return;
            var msg = MessageFactory.CreateLastTighteningResult(result, _currentSubscribedRevision);
            MessageLogged?.Invoke(this, $"发送 MID {msg.MID:D4}({MidDisplayName(msg.MID)}) 修订{msg.Revision} 数据长度={msg.DataField.Length}");
            lock (_streamLock)
            {
                if (_currentStream == null || !_currentStream.CanWrite) return;
                var data = msg.ToByteArray();
                try { _currentStream.Write(data, 0, data.Length); }
                catch { /* 忽略 */ }
            }
            TighteningGenerated?.Invoke(this, result);
        }

        private void RaiseStateChanged(bool isConnected = true)
        {
            var types = _subs.GetAllSubscriptionTypes().ToList();
            string lastSub = types.Count == 0 ? "无" : string.Join(",", types.Select(SubscriptionDisplayName));

            StateChanged?.Invoke(this, new StateEventArgs
            {
                IsConnected = isConnected,
                IsEnabled = _state.ToolEnabled,
                CurrentPsetId = _state.CurrentParameterSetId,
                LastSubscription = lastSub
            });
        }

        private static string SubscriptionDisplayName(string type) => type switch
        {
            "LastTightening" => "拧紧",
            "ParameterSetSelected" => "程序号选中",
            "JobInfo" => "Job信息",
            "VIN" => "VIN",
            "Alarm" => "报警",
            _ => type
        };
    }
}
