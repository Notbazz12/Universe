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

        // New configuration properties
        public bool EnableSmartSorter { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public int AutoSaveInterval { get; set; } = 5; // seconds
        public bool EnableAnimations { get; set; } = true;
        public Dictionary<string, string> SmartSorterRules { get; set; } = null;

        private static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoFences", "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    return JsonConvert.DeserializeObject<AppConfig>(File.ReadAllText(ConfigPath));
                }
            }
            catch { }
            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch { }
        }
    }
}
