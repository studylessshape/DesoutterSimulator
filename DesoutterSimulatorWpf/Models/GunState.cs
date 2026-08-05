using System;
using System.Collections.Generic;

namespace DesoutterSimulatorWpf.Models
{
    public class GunState : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isConnected;
        private bool _isEnabled;
        private int _currentPsetId = 1;
        private string _lastSubscription = "";
        private DateTime _lastTighteningTime;

        public bool IsConnected { get => _isConnected; set { _isConnected = value; OnPropertyChanged(); } }
        public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; OnPropertyChanged(); } }
        public int CurrentPsetId { get => _currentPsetId; set { _currentPsetId = value; OnPropertyChanged(); } }
        public string LastSubscription { get => _lastSubscription; set { _lastSubscription = value; OnPropertyChanged(); } }
        public DateTime LastTighteningTime { get => _lastTighteningTime; set { _lastTighteningTime = value; OnPropertyChanged(); } }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}