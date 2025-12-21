using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NoFences.Model;
using NoFences.Core;

namespace NoFences.Services
{
    public interface ISmartSorterService
    {
        void Start();
        void Stop();
        void Learn(string filePath, string fenceName);
    }

    public class SmartSorterService : ISmartSorterService
    {
        private readonly IFenceService _fenceService;
        private readonly ILoggingService _loggingService;
        private FileSystemWatcher _watcher;
        private readonly string _desktopPath;
        private Dictionary<string, string> _rules; // Extension -> FenceName

        public SmartSorterService(IFenceService fenceService, ILoggingService loggingService)
        {
            _fenceService = fenceService;
            _loggingService = loggingService;
            _desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            LoadRules();
        }

        private void LoadRules()
        {
            var config = AppConfig.Load();
            
            // If we have saved rules, use them
            if (config.SmartSorterRules != null && config.SmartSorterRules.Count > 0)
            {
                _rules = new Dictionary<string, string>(config.SmartSorterRules, StringComparer.OrdinalIgnoreCase);
                _loggingService.LogInfo($"SmartSorter: Loaded {_rules.Count} rules from config");
            }
            else
            {
                // Default rules
                _rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { ".jpg", "Images" },
                    { ".png", "Images" },
                    { ".gif", "Images" },
                    { ".bmp", "Images" },
                    { ".doc", "Documents" },
                    { ".docx", "Documents" },
                    { ".pdf", "Documents" },
                    { ".txt", "Documents" },
                    { ".xls", "Documents" },
                    { ".xlsx", "Documents" },
                    { ".exe", "Programs" },
                    { ".lnk", "Shortcuts" },
                    { ".zip", "Archives" },
                    { ".rar", "Archives" },
                    { ".7z", "Archives" }
                };
                _loggingService.LogInfo("SmartSorter: Using default rules");
            }
        }

        private void SaveRules()
        {
            try
            {
                var config = AppConfig.Load();
                config.SmartSorterRules = _rules;
                config.Save();
                _loggingService.LogInfo("SmartSorter: Rules saved to config");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("SmartSorter: Failed to save rules", ex);
            }
        }

        public void Start()
        {
            _loggingService.LogInfo("SmartSorter: Starting...");
            _watcher = new FileSystemWatcher(_desktopPath);
            _watcher.Created += OnFileCreated;
            _watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                string extension = Path.GetExtension(e.FullPath);
                if (_rules.TryGetValue(extension, out string fenceName))
                {
                    _loggingService.LogInfo($"SmartSorter: Detected {e.Name}, moving to {fenceName}");
                    
                    var fences = _fenceService.GetAllFences();
                    var targetFence = fences.FirstOrDefault(f => f.Name.Equals(fenceName, StringComparison.OrdinalIgnoreCase));
                    
                    if (targetFence != null)
                    {
                        _fenceService.AddFileToFence(targetFence.Id, e.FullPath);
                        _loggingService.LogInfo($"SmartSorter: Added {e.Name} to fence {fenceName}");
                    }
                    else
                    {
                        _loggingService.LogWarning($"SmartSorter: Target fence '{fenceName}' not found");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"SmartSorter error: {ex.Message}");
            }
        }

        public void Learn(string filePath, string fenceName)
        {
            string extension = Path.GetExtension(filePath);
            if (!string.IsNullOrEmpty(extension))
            {
                _rules[extension] = fenceName;
                _loggingService.LogInfo($"SmartSorter: Learned that {extension} belongs to {fenceName}");
                SaveRules();
            }
        }
    }
}
