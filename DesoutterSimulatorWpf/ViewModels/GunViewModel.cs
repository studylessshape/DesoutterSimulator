using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesoutterSimulatorWpf.Models;
using DesoutterSimulatorWpf.Services;
using DesoutterSimulatorWpf.Services.Protocol;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace DesoutterSimulatorWpf.ViewModels
{
    public partial class GunViewModel : ObservableObject, IDisposable
    {
        private const int MaxLogCount = 1000;

        private readonly SimulatorEngine _engine;
        private readonly GunState _state = new GunState();

        public GunConfig Config { get; }
        public GunState State => _state;

        /// <summary>枪日志文本（从上到下时间正序，最新在最下方，最多 1000 行）</summary>
        [ObservableProperty]
        private string _logText = "";

        private readonly List<string> _logLines = new List<string>();

        [ObservableProperty]
        private bool _isRunning;

        /// <summary>端口号仅在启动前可编辑</summary>
        public bool CanEditPort => !_isRunning;

        // 手动发送参数（绑定到UI）
        [ObservableProperty]
        private double _torque = 10.0;

        [ObservableProperty]
        private int _angle = 180;

        [ObservableProperty]
        private int _psetId = 1;

        // 拧紧结果 OK/NG 设置（手动发送时使用）
        [ObservableProperty]
        private TighteningOutcome _outcome = TighteningOutcome.OK;

        public TighteningOutcome[] Outcomes { get; } = { TighteningOutcome.OK, TighteningOutcome.NG };

        public event EventHandler StateChanged;

        public GunViewModel(GunConfig config)
        {
            Config = config;
            _engine = new SimulatorEngine(config.Port);
            _engine.StateChanged += OnEngineStateChanged;
            _engine.TighteningGenerated += OnTighteningGenerated;
            _engine.MessageLogged += (s, msg) => AddLog(msg);
            AddLog($"枪 {Config.Name} 初始化完成(端口={Config.Port})");
        }

        /// <summary>添加日志（线程安全，自动切换到 UI 线程）</summary>
        private void AddLog(string message)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                AppendLog(message);
                return;
            }
            dispatcher.InvokeAsync(() => AppendLog(message));
        }

        private void AppendLog(string message)
        {
            _logLines.Add($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            // 超过上限时移除最旧（顶部）的日志
            while (_logLines.Count > MaxLogCount)
                _logLines.RemoveAt(0);
            // 末尾追加空行，保证最后一行可点击定位（类似 VS 输出窗口）
            LogText = string.Join(Environment.NewLine, _logLines) + Environment.NewLine;
        }

        partial void OnIsRunningChanged(bool value)
        {
            // 关键：通知命令重新评估 CanExecute
            StartCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanEditPort));
        }

        [RelayCommand(CanExecute = nameof(CanStart))]
        private async Task Start()
        {
            if (IsRunning) return;
            IsRunning = true;
            AddLog($"启动监听，端口={Config.Port}");
            try
            {
                // 启动前同步最新端口，支持启动前编辑端口号
                _engine.Port = Config.Port;
                await _engine.StartAsync();
            }
            catch
            {
                // 启动失败（端口被占用等），复位状态
                AddLog("启动失败：端口可能被占用");
                IsRunning = false;
                State.IsEnabled = false;
            }

            // StartAsync 返回意味着引擎已停止（主动 Stop 或意外异常），同步 UI 状态
            if (!_engine.IsRunning)
                IsRunning = false;
        }

        private bool CanStart() => !IsRunning;

        [RelayCommand(CanExecute = nameof(CanStop))]
        private void Stop()
        {
            if (!IsRunning) return;
            _engine.Stop();
            IsRunning = false;
            State.IsConnected = false;
            State.IsEnabled = false;
            State.LastSubscription = "无";
            AddLog("已停止监听");
        }

        private bool CanStop() => IsRunning;

        private void OnEngineStateChanged(object sender, SimulatorEngine.StateEventArgs e)
        {
            State.IsConnected = e.IsConnected;
            State.CurrentPsetId = e.CurrentPsetId;
            State.IsEnabled = e.IsEnabled;
            State.LastSubscription = e.LastSubscription;
            AddLog($"状态：连接={(e.IsConnected ? "是" : "否")}，使能={(e.IsEnabled ? "ON" : "OFF")}，程序号={e.CurrentPsetId}，订阅={e.LastSubscription}");
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnTighteningGenerated(object sender, TighteningResult result)
        {
            State.LastTighteningTime = DateTime.Now;
        }

        public void SendTightening()
        {
            if (!IsRunning) return;
            AddLog($"发送拧紧结果：{(Outcome == TighteningOutcome.OK ? "OK" : "NG")}，扭矩={Torque:0.##}Nm，角度={Angle}°，程序号={PsetId}");

            var result = new TighteningResult
            {
                VIN = "VIN_TEST",
                Torque = Torque,
                Angle = Angle,
                PsetId = PsetId,
                TimeStamp = DateTime.Now,
                PsetChangeTime = DateTime.Now.AddDays(-1),
                TighteningId = DateTime.Now.Ticks % 10000000000,
                JobId = 1,
                ChannelId = 1,
                CellId = 1,
                Status = (int)Outcome,
                BatchStatus = 1,
                TorqueStatus = 1,
                AngleStatus = 1,
                ParameterSetName = $"Pset_{PsetId:D3}",
                Strategy = 2,
                StrategyOptions = "00000",
                TighteningErrors = "0000000000",
                TighteningErrorStatus2 = "0000000000",
                ResultType = 1,
                TorqueValuesUnit = 1,
                CompensatedAngle = 0,
                FinalAngleDecimal = Angle,
                RundownAngleStatus = 1,
                CurrentMonitoringStatus = 1,
                SelftapStatus = 1,
                PrevailTorqueStatus = 1,
                CompensateStatus = 1,
                RundownAngleMin = 0,
                RundownAngleMax = 100,
                RundownAngle = 50,
                CurrentMonitoringMin = 80,
                CurrentMonitoringMax = 120,
                CurrentMonitoringValue = 100,
                SelftapMin = 1,
                SelftapMax = 5,
                SelftapTorque = 3,
                PrevailTorqueMin = 1,
                PrevailTorqueMax = 5,
                PrevailTorque = 3,
                JobSequence = 12345,
                SyncTighteningId = 0,
                ToolSerialNumber = "C341212025487",
            };
            _engine.SendTighteningResult(result);
        }

        public void Dispose() => _engine.Stop();
    }

    /// <summary>拧紧结果状态，与 TighteningResult.Status 对应（0=NOK, 1=OK）</summary>
    public enum TighteningOutcome
    {
        NG = 0,
        OK = 1
    }
}
