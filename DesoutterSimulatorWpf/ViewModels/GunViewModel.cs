using DesoutterSimulatorWpf.Models;
using DesoutterSimulatorWpf.Services;
using DesoutterSimulatorWpf.Services.Protocol;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace DesoutterSimulatorWpf.ViewModels
{
    public class GunViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SimulatorEngine _engine;
        private readonly GunState _state = new GunState();
        private bool _isRunning;
        private readonly RelayCommand _startCommand;
        private readonly RelayCommand _stopCommand;

        public GunConfig Config { get; }
        public GunState State => _state;

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanEditPort));
                    // 关键：通知命令重新评估 CanExecute
                    _startCommand.RaiseCanExecuteChanged();
                    _stopCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>端口号仅在启动前可编辑</summary>
        public bool CanEditPort => !_isRunning;

        // 手动发送参数（绑定到UI）
        public double Torque { get; set; } = 10.0;
        public int Angle { get; set; } = 180;
        public int PsetId { get; set; } = 1;

        // 拧紧结果 OK/NG 设置（手动发送时使用）
        public TighteningOutcome Outcome { get; set; } = TighteningOutcome.OK;
        public TighteningOutcome[] Outcomes { get; } = { TighteningOutcome.OK, TighteningOutcome.NG };

        public ICommand StartCommand => _startCommand;
        public ICommand StopCommand => _stopCommand;

        public event EventHandler StateChanged;

        public GunViewModel(GunConfig config)
        {
            Config = config;
            _engine = new SimulatorEngine(config.Port);
            _engine.StateChanged += OnEngineStateChanged;
            _engine.TighteningGenerated += OnTighteningGenerated;

            _startCommand = new RelayCommand(() => _ = StartAsync(), () => !IsRunning);
            _stopCommand = new RelayCommand(() => Stop(), () => IsRunning);
        }

        private async Task StartAsync()
        {
            if (IsRunning) return;
            IsRunning = true;
            try
            {
                // 启动前同步最新端口，支持启动前编辑端口号
                _engine.Port = Config.Port;
                await _engine.StartAsync();
            }
            catch
            {
                // 启动失败（端口被占用等），复位状态
                IsRunning = false;
            }

            // StartAsync 返回意味着引擎已停止（主动 Stop 或意外异常），同步 UI 状态
            if (!_engine.IsRunning)
                IsRunning = false;
        }

        private void Stop()
        {
            if (!IsRunning) return;
            _engine.Stop();
            IsRunning = false;
            State.IsConnected = false;
            State.LastSubscription = "无";
        }

        private void OnEngineStateChanged(object sender, SimulatorEngine.StateEventArgs e)
        {
            State.IsConnected = e.IsConnected;
            State.CurrentPsetId = e.CurrentPsetId;
            State.IsEnabled = e.IsEnabled;
            State.LastSubscription = e.LastSubscription;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnTighteningGenerated(object sender, TighteningResult result)
        {
            State.LastTighteningTime = DateTime.Now;
        }

        public void SendTightening()
        {
            if (!IsRunning) return;

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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>拧紧结果状态，与 TighteningResult.Status 对应（0=NOK, 1=OK）</summary>
    public enum TighteningOutcome
    {
        NG = 0,
        OK = 1
    }
}