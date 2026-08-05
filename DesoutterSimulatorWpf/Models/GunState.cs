using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace DesoutterSimulatorWpf.Models
{
    public partial class GunState : ObservableObject
    {
        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isEnabled;

        [ObservableProperty]
        private int _currentPsetId = 1;

        [ObservableProperty]
        private string _lastSubscription = "";

        [ObservableProperty]
        private DateTime _lastTighteningTime;
    }
}
