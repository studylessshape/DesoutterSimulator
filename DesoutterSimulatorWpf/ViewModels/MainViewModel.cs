using DesoutterSimulatorWpf.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace DesoutterSimulatorWpf.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private const string CONFIG_FILE = "guns_config.json";

        public ObservableCollection<GunViewModel> Guns { get; } = new ObservableCollection<GunViewModel>();

        public ICommand AddGunCommand { get; }
        public ICommand RemoveGunCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand LoadConfigCommand { get; }

        public MainViewModel()
        {
            LoadConfig();

            AddGunCommand = new RelayCommand(AddGun);
            RemoveGunCommand = new RelayCommand(RemoveGun, () => Guns.Count > 0);
            SaveConfigCommand = new RelayCommand(SaveConfig);
            LoadConfigCommand = new RelayCommand(LoadConfig);
        }

        private void AddGun()
        {
            int nextPort = Guns.Count > 0 ? Guns.Max(g => g.Config.Port) + 1 : 4545;
            var config = new GunConfig { Name = $"Gun{Guns.Count + 1}", Port = nextPort };
            var vm = new GunViewModel(config);
            Guns.Add(vm);
        }

        private void RemoveGun()
        {
            if (Guns.Count > 0)
            {
                var last = Guns.Last();
                last.StopCommand.Execute(null);
                Guns.Remove(last);
            }
        }

        private void SaveConfig()
        {
            var configs = Guns.Select(g => g.Config).ToList();
            var json = System.Text.Json.JsonSerializer.Serialize(configs);
            File.WriteAllText(CONFIG_FILE, json);
        }

        private void LoadConfig()
        {
            if (!File.Exists(CONFIG_FILE)) return;
            var json = File.ReadAllText(CONFIG_FILE);
            var configs = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<GunConfig>>(json);
            if (configs == null) return;

            Guns.Clear();
            foreach (var cfg in configs)
                Guns.Add(new GunViewModel(cfg));
            if (Guns.Count == 0)
                AddGun();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}