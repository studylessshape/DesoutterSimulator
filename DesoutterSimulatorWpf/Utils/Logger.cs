using System;

namespace DesoutterSimulatorWpf.Utils
{
    public static class Logger
    {
        private static readonly object _lock = new();

        public static void Info(string message) => Log("INFO", message, ConsoleColor.White);
        public static void Debug(string message) => Log("DEBUG", message, ConsoleColor.Gray);
        public static void Warning(string message) => Log("WARN", message, ConsoleColor.Yellow);
        public static void Error(string message) => Log("ERROR", message, ConsoleColor.Red);
        public static void Success(string message) => Log("OK", message, ConsoleColor.Green);

        private static void Log(string level, string message, ConsoleColor color)
        {
            lock (_lock)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                Console.ForegroundColor = color;
                Console.WriteLine($"[{timestamp}] [{level}] {message}");
                Console.ResetColor();
            }
        }
    }
}