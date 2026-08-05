using DesoutterSimulator.Protocol;
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
        private NetworkStream _currentStream;
        private readonly object _streamLock = new object();

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
                if (msg.MID == 9999) return msg; // Keep alive

                // 处理请求
                var response = await HandleMessageAsync(msg);
                return response;
            }
            catch
            {
                return null;
            }
        }

        private Task<Message> HandleMessageAsync(Message request)
        {
            switch (request.MID)
            {
                case 1:
                    if (_state.CommunicationStarted)
                        return Task.FromResult(MessageFactory.CreateCommandError(request.MID, 96));
                    _state.CommunicationStarted = true;
                    int rev = request.Revision > 0 ? request.Revision : 1;
                    return Task.FromResult(MessageFactory.CreateCommunicationStartAcknowledge(rev));

                case 3:
                    _state.CommunicationStarted = false;
                    return Task.FromResult(MessageFactory.CreateCommandAccepted(request.MID));

                case 60:
                    if (_subs.HasSubscription("LastTightening"))
                        return Task.FromResult(MessageFactory.CreateCommandError(request.MID, 9));
                    _subs.AddSubscription("LastTightening", request.NoAckFlag == 1);
                    _currentSubscribedRevision = request.Revision > 0 ? request.Revision : 1;
                    RaiseStateChanged();
                    return Task.FromResult(MessageFactory.CreateCommandAccepted(request.MID));

                case 63:
                    if (!_subs.HasSubscription("LastTightening"))
                        return Task.FromResult(MessageFactory.CreateCommandError(request.MID, 10));
                    _subs.RemoveSubscription("LastTightening");
                    RaiseStateChanged();
                    return Task.FromResult(MessageFactory.CreateCommandAccepted(request.MID));

                case 42:
                    _state.ToolEnabled = false;
                    RaiseStateChanged();
                    return Task.FromResult(MessageFactory.CreateCommandAccepted(request.MID));

                case 43:
                    _state.ToolEnabled = true;
                    RaiseStateChanged();
                    return Task.FromResult(MessageFactory.CreateCommandAccepted(request.MID));

                case 10:
                    var ids = _state.GetParameterSetIDs();
                    return Task.FromResult(MessageFactory.CreateParameterSetIDUploadReply(ids));

                default:
                    return Task.FromResult(MessageFactory.CreateCommandError(request.MID, 99));
            }
        }

        public void SendTighteningResult(TighteningResult result)
        {
            if (!_subs.HasSubscription("LastTightening")) return;
            var msg = MessageFactory.CreateLastTighteningResult(result, _currentSubscribedRevision);
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