namespace DesoutterSimulatorWpf.Models
{
    [Serializable]
    public class GunConfig
    {
        public string Name { get; set; } = "Gun1";
        public int Port { get; set; } = 4545;
        public bool AutoStart { get; set; } = false;
    }
}