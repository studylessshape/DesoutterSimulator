using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using DesoutterSimulatorWpf.Models;
using DesoutterSimulatorWpf.Services;
using DesoutterSimulatorWpf.Services.Protocol;

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
                    // 关键：通知命令重新评估 CanExecute
                    _startCommand.RaiseCanExecuteChanged();
                    _stopCommand.RaiseCanExecuteChanged();
                }
            }
        }

        // 手动发送参数（绑定到UI）
        public string VIN { get; set; } = "VIN_TEST";
        public double Torque { get; set; } = 10.0;
        public int Angle { get; set; } = 180;
        public int PsetId { get; set; } = 1;

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
            await _engine.StartAsync();
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
                VIN = VIN,
                Torque = Torque,
                Angle = Angle,
                PsetId = PsetId,
                TimeStamp = DateTime.Now,
                PsetChangeTime = DateTime.Now.AddDays(-1),
                TighteningId = DateTime.Now.Ticks % 10000000000,
                JobId = 1,
                ChannelId = 1,
                CellId = 1,
                Status = 1,
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
                FinalAngleDecimal = 0,
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
}