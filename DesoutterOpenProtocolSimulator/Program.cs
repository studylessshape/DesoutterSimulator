using System;
using System.Threading.Tasks;

namespace DesoutterSimulator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "Desoutter Open Protocol Simulator v1.3";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     Desoutter Open Protocol Simulator v1.3               ║");
            Console.WriteLine("║     TCP Port: 4545                                       ║");
            Console.WriteLine("║     Press Ctrl+C to stop                                 ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            var simulator = new Simulator();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                simulator.Stop();
            };

            await simulator.StartAsync();
        }
    }
}