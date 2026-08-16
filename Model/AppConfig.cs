using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace NoFences.Model
{
    public class AppConfig
    {
        // Existing properties
        public string Language { get; set; } = "English";
        public bool LaptopMode { get; set; } = false;

        // Configuration properties
        public bool EnableSmartSorter { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public int AutoSaveInterval { get; set; } = 5; // seconds
        public bool EnableAnimations { get; set; } = true;
        public string UpdateUrl { get; set; } = "https://raw.githubusercontent.com/Notbazz12/Universe/main/version.json";
        public Dictionary<string, string> SmartSorterRules { get; set; } = null;

        private static readonly object _configLock = new object();
        private static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoFences", "config.json");

        public static AppConfig Load()
        {
            lock (_configLock)
            {
                try
                {
                    if (File.Exists(ConfigPath))
                    {
                        var json = File.ReadAllText(ConfigPath);
                        var cfg = JsonConvert.DeserializeObject<AppConfig>(json);
                        if (cfg != null) return cfg;
                    }
                }
                catch { }
                return new AppConfig();
            }
        }

        public void Save()
        {
            lock (_configLock)
            {
                try
                {
                    var dir = Path.GetDirectoryName(ConfigPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    var tempPath = ConfigPath + ".tmp";
                    var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                    File.WriteAllText(tempPath, json);

                    if (File.Exists(ConfigPath))
                    {
                        File.Delete(ConfigPath);
                    }
                    File.Move(tempPath, ConfigPath);
                }
                catch { }
            }
        }
    }
}
