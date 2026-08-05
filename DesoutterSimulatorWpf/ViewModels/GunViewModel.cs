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
    public class GunViewModel : INotifyPropertyChanged
    {
        private readonly SimulatorEngine _engine;
        private readonly GunState _state = new GunState();
        private bool _isRunning;

        public GunConfig Config { get; }

        public GunState State => _state;
        public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged(); } }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand SendTighteningCommand { get; }

        // 用于手动发送的参数
        public string VIN { get; set; } = "VIN_TEST";
        public double Torque { get; set; } = 10.0;
        public int Angle { get; set; } = 180;
        public int PsetId { get; set; } = 1;

        public GunViewModel(GunConfig config)
        {
            Config = config;
            _engine = new SimulatorEngine(config.Port);
            _engine.StateChanged += OnEngineStateChanged;
            _engine.TighteningGenerated += OnTighteningGenerated;

            StartCommand = new RelayCommand(() => _ = StartAsync());
            StopCommand = new RelayCommand(() => Stop());
            SendTighteningCommand = new RelayCommand(SendTightening);
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
        }

        private void OnEngineStateChanged(object sender, SimulatorEngine.StateEventArgs e)
        {
            State.IsConnected = e.IsConnected;
            State.CurrentPsetId = e.CurrentPsetId;
            State.IsEnabled = e.IsEnabled;
            State.LastSubscription = e.LastSubscription;
        }

        private void OnTighteningGenerated(object sender, TighteningResult result)
        {
            State.LastTighteningTime = DateTime.Now;
            // 可由UI显示最近结果
        }

        private void SendTightening()
        {
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
                // 其他字段使用默认值
                ParameterSetName = $"Pset_{PsetId:D3}",
                Strategy = 2,
                StrategyOptions = "00000",
                TighteningErrors = "0000000000",
                TighteningErrorStatus2 = "0000000000",
                ResultType = 1,
                TorqueValuesUnit = 1,
                CompensatedAngle = 0,
                FinalAngleDecimal = 0,
                // ...
            };
            _engine.SendTighteningResult(result);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}