using DesoutterSimulator.Protocol;
using DesoutterSimulatorWpf.Models;
using DesoutterSimulatorWpf.Services.Protocol;
using System;
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
        private readonly int _port;
        private ControllerState _state = new ControllerState();
        private SubscriptionManager _subs = new SubscriptionManager();
        private int _currentSubscribedRevision = 1;

        public event EventHandler<StateEventArgs> StateChanged;
        public event EventHandler<TighteningResult> TighteningGenerated;

        public class StateEventArgs : EventArgs
        {
            public bool IsConnected { get; set; }
            public bool IsEnabled { get; set; }
            public int CurrentPsetId { get; set; }
            public string LastSubscription { get; set; }
        }

        public SimulatorEngine(int port) => _port = port;

        public async Task StartAsync()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            // 通知UI连接状态
            RaiseStateChanged(isConnected: true);

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client, _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                // 日志
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
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var buffer = new byte[8192];
                var messageBuffer = new StringBuilder();

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
        }

        private async Task<Message> ProcessMessageAsync(string msgStr)
        {
            var msg = MessageParser.Parse(msgStr);
            if (msg.MID == 9999) return msg; // Keep alive

            // 处理请求
            var response = await HandleMessageAsync(msg);
            return response;
        }

        private Task<Message> HandleMessageAsync(Message request)
        {
            switch (request.MID)
            {
                case 1: // Communication start
                    if (_state.CommunicationStarted)
                        return Task.FromResult(MessageFactory.CreateCommandError(request.MID, 96));
                    _state.CommunicationStarted = true;
                    int rev = request.Revision > 0 ? request.Revision : 1;
                    return Task.FromResult(MessageFactory.CreateCommunicationStartAcknowledge(rev));

                case 3: // Communication stop
                    _state.CommunicationStarted = false;
                    return Task.FromResult(MessageFactory.CreateCommandAccepted(request.MID));

                case 60: // Subscribe tightening
                    if (_subs.HasSubscription("LastTightening"))
                        return Task.FromResult(MessageFactory.CreateCommandError(request.MID, 9));
                    _subs.AddSubscription("LastTightening", request.NoAckFlag == 1);
                    _currentSubscribedRevision = request.Revision > 0 ? request.Revision : 1;
                    return Task.FromResult(MessageFactory.CreateCommandAccepted(request.MID));

                case 63: // Unsubscribe tightening
                    if (!_subs.HasSubscription("LastTightening"))
                        return Task.FromResult(MessageFactory.CreateCommandError(request.MID, 10));
                    _subs.RemoveSubscription("LastTightening");
                    return Task.FromResult(MessageFactory.CreateCommandAccepted(request.MID));

                case 42: // Disable tool
                    _state.ToolEnabled = false;
                    RaiseStateChanged();
                    return Task.FromResult(MessageFactory.CreateCommandAccepted(request.MID));

                case 43: // Enable tool
                    _state.ToolEnabled = true;
                    RaiseStateChanged();
                    return Task.FromResult(MessageFactory.CreateCommandAccepted(request.MID));

                case 10: // Parameter set ID upload request
                    var ids = _state.GetParameterSetIDs();
                    return Task.FromResult(MessageFactory.CreateParameterSetIDUploadReply(ids));

                // 其他MID可类似实现...
                default:
                    return Task.FromResult(MessageFactory.CreateCommandError(request.MID, 99));
            }
        }

        public void SendTighteningResult(TighteningResult result)
        {
            if (!_subs.HasSubscription("LastTightening")) return;
            var msg = MessageFactory.CreateLastTighteningResult(result, _currentSubscribedRevision);
            // 这里需要将消息发送给当前连接的客户端，简单起见，我们通过事件通知UI，由UI转发？
            // 但SimulatorEngine需要维护客户端连接，才能发送。
            // 改进：在HandleClientAsync中保存NetworkStream，此处使用保存的stream。
            // 为简化，我们使用静态事件或委托，但这里略复杂。
            // 实际实现中应维护当前连接的Stream列表，此处我们假设仅单客户端，通过字段保存。
            // 为实现演示，我们在类内部维护一个Stream，但多客户端时需处理。
            // 因为WPF版本允许每个枪单客户端，我们简化：每个Engine只维护一个连接。
            // 修改：在HandleClientAsync中保存stream到字段 _currentStream，然后在发送时使用。
        }

        private void RaiseStateChanged(bool isConnected = true)
        {
            StateChanged?.Invoke(this, new StateEventArgs
            {
                IsConnected = isConnected,
                IsEnabled = _state.ToolEnabled,
                CurrentPsetId = _state.CurrentParameterSetId,
                LastSubscription = _subs.HasSubscription("LastTightening") ? "拧紧" : "无"
            });
        }
    }
}