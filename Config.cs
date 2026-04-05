using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ObsidianMonitor
{
    public class AppConfig
    {
        public double PositionX { get; set; } = -1;
        public double PositionY { get; set; } = 10;
        public bool IsLocked { get; set; } = true;
        public bool ShowTemperature { get; set; } = true;
        public double SizeScale { get; set; } = 1.0;
        public string Theme { get; set; } = "Black";
        public string CustomColor { get; set; } = "#E6000000";

        // New Dynamic Properties
        public List<string> OrderedElements { get; set; } = new List<string> { "CPU", "GPU", "RAM" };
        public bool IsProcessMonitorActive { get; set; } = false;
        public bool IsProcessMonitorPinned { get; set; } = false;
        public string ProcessSortMetric { get; set; } = "RAM"; // CPU, RAM, DISK, NETWORK, GPU

        // Legacy fans toggle or specific fan IDs
        public List<string> SelectedFans { get; set; } = new List<string> { "FANS" }; 
        
        // Hotkey Settings
        public uint ToggleModifier { get; set; } = 0x0001; // Default MOD_ALT
        public uint ToggleKey { get; set; } = 0x53; // Default 'S'
        public string ToggleHotkeyText { get; set; } = "Alt + S";
        
        // Game Metrics Toggles
        public bool ShowFps { get; set; } = false;
        public bool ShowLatency { get; set; } = false;
        public bool ShowOnePercentLow { get; set; } = false;
        public bool ShowAvgFps { get; set; } = false;
        public bool ShowGpuWatts { get; set; } = false;

        private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch { }
            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}
