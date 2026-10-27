using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NoFences.Persistence;

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
        public bool AutoCheckUpdates { get; set; } = true;
        public bool ShowDesktopMascot { get; set; } = true;
        public int DesktopMascotX { get; set; } = -1;
        public int DesktopMascotY { get; set; } = -1;
        public int DesktopMascotSize { get; set; } = 96;
        public bool DesktopMascotTopMost { get; set; } = false;
        public string MascotSkin { get; set; } = "Cosmic";
        public bool MascotWander { get; set; } = true;
        public string UpdateUrl { get; set; } = "https://raw.githubusercontent.com/Notbazz12/Universe/main/version.json";
        public Dictionary<string, string> SmartSorterRules { get; set; } = null;

        private static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoFences", "config.json");

        private static readonly Lazy<AppConfig> _current = new Lazy<AppConfig>(LoadFromDisk);

        private static readonly DeferredFileWriter _writer = new DeferredFileWriter(
            TimeSpan.FromMilliseconds(500),
            (path, ex) => System.Diagnostics.Debug.WriteLine($"Config save failed ({path}): {ex.Message}"));

        /// <summary>Shared in-memory configuration; disk is read once and written debounced.</summary>
        [JsonIgnore]
        public static AppConfig Current => _current.Value;

        /// <summary>Kept for compatibility: returns the shared instance instead of re-reading the disk.</summary>
        public static AppConfig Load()
        {
            return Current;
        }

        private static AppConfig LoadFromDisk()
        {
            return AtomicFile.ReadWithRecovery(ConfigPath, JsonConvert.DeserializeObject<AppConfig>) ?? new AppConfig();
        }

        public void Save()
        {
            _writer.Schedule(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        /// <summary>Writes any pending configuration change immediately (call on shutdown).</summary>
        public static void Flush()
        {
            _writer.Flush();
        }
    }
}
