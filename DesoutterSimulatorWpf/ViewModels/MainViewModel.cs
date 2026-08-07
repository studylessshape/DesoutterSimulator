using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesoutterSimulatorWpf.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DesoutterSimulatorWpf.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private const string CONFIG_FILE = "guns_config.json";

        public ObservableCollection<GunViewModel> Guns { get; } = new ObservableCollection<GunViewModel>();

        [ObservableProperty]
        private GunViewModel? _selectedGun;

        [ObservableProperty]
        private string _statusMessage = "就绪";

        [ObservableProperty]
        private string _sendResultMessage = "";

        public MainViewModel()
        {
            LoadConfig();
        }

        partial void OnSelectedGunChanged(GunViewModel? value)
        {
            // 选中枪变化时，重新评估发送按钮
            SendTighteningCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void AddGun()
        {
            int nextPort = Guns.Count > 0 ? Guns.Max(g => g.Config.Port) + 1 : 4545;
            var config = new GunConfig { Name = $"Gun{Guns.Count + 1}", Port = nextPort };
            var vm = new GunViewModel(config);

            // 监听状态变化，更新发送按钮
            vm.StateChanged += (s, e) =>
            {
                StatusMessage = $"枪 {vm.Config.Name} 状态更新";
                SendTighteningCommand.NotifyCanExecuteChanged();
            };

            // 监听 IsRunning 变化，更新发送按钮
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(GunViewModel.IsRunning))
                {
                    SendTighteningCommand.NotifyCanExecuteChanged();
                }
            };

            Guns.Add(vm);
            SelectedGun = vm;
            SendTighteningCommand.NotifyCanExecuteChanged();
            RemoveGunCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanRemoveGun))]
        private void RemoveGun(GunViewModel? gun)
        {
            if (gun == null || !Guns.Contains(gun)) return;

            bool removingSelected = SelectedGun == gun;
            int idx = Guns.IndexOf(gun);

            gun.StopCommand.Execute(null);
            Guns.Remove(gun);

            if (removingSelected)
            {
                // 先移除再选相邻枪（优先前一项），避免指向已移出的对象
                SelectedGun = Guns.Count > 0 ? Guns[Math.Max(0, idx - 1)] : null;
            }

            SaveConfig();
            SendTighteningCommand.NotifyCanExecuteChanged();
            RemoveGunCommand.NotifyCanExecuteChanged();
        }

        private bool CanRemoveGun(GunViewModel? gun) => gun != null;

        [RelayCommand]
        private void SaveConfig()
        {
            var configs = Guns.Select(g => g.Config).ToList();
            var json = JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CONFIG_FILE, json);
            StatusMessage = "配置已保存";
        }

        [RelayCommand]
        private void LoadConfig()
        {
            // 先停止所有正在运行的枪，释放端口，避免重建后端口被占用
            foreach (var gun in Guns)
                gun.StopCommand.Execute(null);
            Guns.Clear();
            if (File.Exists(CONFIG_FILE))
            {
                var json = File.ReadAllText(CONFIG_FILE);
                var configs = JsonSerializer.Deserialize<List<GunConfig>>(json);
                if (configs != null)
                {
                    foreach (var cfg in configs)
                    {
                        var vm = new GunViewModel(cfg);
                        vm.StateChanged += (s, e) =>
                        {
                            StatusMessage = $"枪 {vm.Config.Name} 状态更新";
                            SendTighteningCommand.NotifyCanExecuteChanged();
                        };
                        vm.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(GunViewModel.IsRunning))
                            {
                                SendTighteningCommand.NotifyCanExecuteChanged();
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
            SendTighteningCommand.NotifyCanExecuteChanged();
            RemoveGunCommand.NotifyCanExecuteChanged();
        }

        private bool CanSendTightening() => SelectedGun != null && SelectedGun.IsRunning;

        [RelayCommand(CanExecute = nameof(CanSendTightening))]
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
    }
}
