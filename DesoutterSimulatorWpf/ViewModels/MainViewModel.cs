using DesoutterSimulatorWpf.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;

namespace DesoutterSimulatorWpf.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private const string CONFIG_FILE = "guns_config.json";
        private GunViewModel _selectedGun;
        private string _statusMessage = "就绪";
        private string _sendResultMessage = "";

        public ObservableCollection<GunViewModel> Guns { get; } = new ObservableCollection<GunViewModel>();

        public GunViewModel SelectedGun
        {
            get => _selectedGun;
            set
            {
                if (_selectedGun != value)
                {
                    _selectedGun = value;
                    OnPropertyChanged();
                    // 选中枪变化时，重新评估发送按钮
                    SendTighteningCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }
        public string SendResultMessage { get => _sendResultMessage; set { _sendResultMessage = value; OnPropertyChanged(); } }

        public ICommand AddGunCommand { get; }
        public ICommand RemoveGunCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand LoadConfigCommand { get; }
        public RelayCommand SendTighteningCommand { get; }

        public MainViewModel()
        {
            AddGunCommand = new RelayCommand(AddGun);
            RemoveGunCommand = new RelayCommand(RemoveGun, () => Guns.Count > 0);
            SaveConfigCommand = new RelayCommand(SaveConfig);
            LoadConfigCommand = new RelayCommand(LoadConfig);
            SendTighteningCommand = new RelayCommand(SendTightening, CanSendTightening);

            LoadConfig();
        }

        private void AddGun()
        {
            int nextPort = Guns.Count > 0 ? Guns.Max(g => g.Config.Port) + 1 : 4545;
            var config = new GunConfig { Name = $"Gun{Guns.Count + 1}", Port = nextPort };
            var vm = new GunViewModel(config);

            // 监听状态变化，更新发送按钮
            vm.StateChanged += (s, e) =>
            {
                StatusMessage = $"枪 {vm.Config.Name} 状态更新";
                SendTighteningCommand.RaiseCanExecuteChanged();
            };

            // 监听 IsRunning 变化，更新发送按钮
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(GunViewModel.IsRunning))
                {
                    SendTighteningCommand.RaiseCanExecuteChanged();
                }
            };

            Guns.Add(vm);
            SelectedGun = vm;
            SendTighteningCommand.RaiseCanExecuteChanged();
        }

        private void RemoveGun()
        {
            if (Guns.Count == 0) return;

            var last = Guns.Last();
            if (SelectedGun == last)
                SelectedGun = Guns.Count > 1 ? Guns[Guns.Count - 2] : null;

            last.StopCommand.Execute(null);
            Guns.Remove(last);
            SaveConfig();
            SendTighteningCommand.RaiseCanExecuteChanged();
        }

        private void SaveConfig()
        {
            var configs = Guns.Select(g => g.Config).ToList();
            var json = JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CONFIG_FILE, json);
            StatusMessage = "配置已保存";
        }

        private void LoadConfig()
        {
            Guns.Clear();
            if (File.Exists(CONFIG_FILE))
            {
                var json = File.ReadAllText(CONFIG_FILE);
                var configs = JsonSerializer.Deserialize<System.Collections.Generic.List<GunConfig>>(json);
                if (configs != null)
                {
                    foreach (var cfg in configs)
                    {
                        var vm = new GunViewModel(cfg);
                        vm.StateChanged += (s, e) =>
                        {
                            StatusMessage = $"枪 {vm.Config.Name} 状态更新";
                            SendTighteningCommand.RaiseCanExecuteChanged();
                        };
                        vm.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(GunViewModel.IsRunning))
                            {
                                SendTighteningCommand.RaiseCanExecuteChanged();
                            }
                        };
                        Guns.Add(vm);
                    }
                }
            }
            if (Guns.Count == 0)
                AddGun();
            SelectedGun = Guns.FirstOrDefault();
            StatusMessage = $"加载 {Guns.Count} 把枪配置";
            SendTighteningCommand.RaiseCanExecuteChanged();
        }

        private bool CanSendTightening() => SelectedGun != null && SelectedGun.IsRunning;

        private void SendTightening()
        {
            if (SelectedGun == null || !SelectedGun.IsRunning)
            {
                SendResultMessage = "请先选择并启动一把枪";
                return;
            }
            SelectedGun.SendTightening();
            SendResultMessage = $"已向 {SelectedGun.Config.Name} 发送拧紧结果";
            StatusMessage = SendResultMessage;
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