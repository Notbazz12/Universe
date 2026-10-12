using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NoFences.Model;
using NoFences.Core;

namespace NoFences.Services
{
    public interface ISmartSorterService
    {
        void Start();
        void Stop();
        void Learn(string filePath, string fenceName);
        int SortDesktopGames();
    }

    public class SmartSorterService : ISmartSorterService
    {
        private readonly IFenceService _fenceService;
        private readonly ILoggingService _loggingService;
        private readonly IGameDetectionService _gameDetectionService;
        private FileSystemWatcher _watcher;
        private readonly string _desktopPath;
        private Dictionary<string, string> _rules; // Extension -> FenceName
        private readonly object _rulesLock = new object();

        public SmartSorterService(
            IFenceService fenceService,
            ILoggingService loggingService,
            IGameDetectionService gameDetectionService)
        {
            _fenceService = fenceService ?? throw new ArgumentNullException(nameof(fenceService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _gameDetectionService = gameDetectionService ?? throw new ArgumentNullException(nameof(gameDetectionService));
            _desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            LoadRules();
        }

        private void LoadRules()
        {
            var config = AppConfig.Load();
            
            lock (_rulesLock)
            {
                if (config.SmartSorterRules != null && config.SmartSorterRules.Count > 0)
                {
                    _rules = new Dictionary<string, string>(config.SmartSorterRules, StringComparer.OrdinalIgnoreCase);
                    _loggingService.LogInfo($"SmartSorter: Loaded {_rules.Count} rules from config");
                }
                else
                {
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
                        { ".url", "Bookmarks" },
                        { ".zip", "Archives" },
                        { ".rar", "Archives" },
                        { ".7z", "Archives" }
                    };
                    _loggingService.LogInfo("SmartSorter: Using default rules");
                }
            }
        }

        private void SaveRules()
        {
            try
            {
                var config = AppConfig.Load();
                lock (_rulesLock)
                {
                    config.SmartSorterRules = new Dictionary<string, string>(_rules, StringComparer.OrdinalIgnoreCase);
                }
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
            Stop();

            try
            {
                if (!Directory.Exists(_desktopPath)) return;

                _loggingService.LogInfo("SmartSorter: Starting...");
                _watcher = new FileSystemWatcher(_desktopPath)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };
                _watcher.Created += OnFileCreated;
                _watcher.Renamed += OnFileRenamed;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("SmartSorter: Failed to start watcher", ex);
            }
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                try
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Created -= OnFileCreated;
                    _watcher.Renamed -= OnFileRenamed;
                    _watcher.Dispose();
                }
                catch { }
                finally
                {
                    _watcher = null;
                }
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            ProcessNewFile(e.FullPath, e.Name);
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            ProcessNewFile(e.FullPath, e.Name);
        }

        private void ProcessNewFile(string fullPath, string fileName)
        {
            Task.Run(async () =>
            {
                try
                {
                    // Short delay to let the creating process finish writing/closing the handle
                    await Task.Delay(600);

                    if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) return;

                    var fences = _fenceService.GetAllFences();

                    // 1. GAME DETECTION CHECK: Is this file or shortcut a Game?
                    if (_gameDetectionService.IsGame(fullPath, out var gameTitle))
                    {
                        _loggingService.LogInfo($"SmartSorter: Detected game '{gameTitle}' at {fileName}");
                        AddGameToAppropriateFence(fullPath, fences);
                        return;
                    }

                    // 2. DIRECTORY CHECK: Is this a folder with multiple games inside?
                    if (Directory.Exists(fullPath))
                    {
                        if (_gameDetectionService.IsGameDirectory(fullPath, out var gamesInDir) && gamesInDir.Count > 0)
                        {
                            _loggingService.LogInfo($"SmartSorter: Detected folder with {gamesInDir.Count} games at {fileName}");
                            foreach (var game in gamesInDir)
                            {
                                AddGameToAppropriateFence(game, fences);
                            }
                            return;
                        }
                    }

                    // 3. REGULAR EXTENSION RULES
                    string extension = Path.GetExtension(fullPath);
                    string targetFenceName = null;

                    lock (_rulesLock)
                    {
                        if (!string.IsNullOrEmpty(extension))
                        {
                            _rules.TryGetValue(extension, out targetFenceName);
                        }
                    }

                    if (!string.IsNullOrEmpty(targetFenceName))
                    {
                        _loggingService.LogInfo($"SmartSorter: Detected {fileName}, moving to {targetFenceName}");
                        
                        var targetFence = fences.FirstOrDefault(f => f.Name.Equals(targetFenceName, StringComparison.OrdinalIgnoreCase));
                        
                        if (targetFence != null)
                        {
                            _fenceService.AddFileToFence(targetFence.Id, fullPath);
                            _loggingService.LogInfo($"SmartSorter: Added {fileName} to fence {targetFenceName}");
                        }
                        else
                        {
                            _loggingService.LogWarning($"SmartSorter: Target fence '{targetFenceName}' not found");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"SmartSorter error: {ex.Message}");
                }
            });
        }

        private void AddGameToAppropriateFence(string filePath, List<FenceInfo> fences)
        {
            var targetFence = _gameDetectionService.FindBestFenceForGames(fences);

            // If no game fence exists, auto-create one!
            if (targetFence == null)
            {
                _loggingService.LogInfo("SmartSorter: No existing game fence found. Creating '🎮 Games' fence...");
                _fenceService.CreateFence("🎮 Games", 120, 180, 420, 340);
                fences = _fenceService.GetAllFences();
                targetFence = _gameDetectionService.FindBestFenceForGames(fences) ?? fences.LastOrDefault();
            }

            if (targetFence != null)
            {
                _fenceService.AddFileToFence(targetFence.Id, filePath);
                _loggingService.LogInfo($"SmartSorter: Game added to fence '{targetFence.Name}' -> {Path.GetFileName(filePath)}");
            }
        }

        public int SortDesktopGames()
        {
            _loggingService.LogInfo("SmartSorter: Triggered manual desktop games sort");
            return _gameDetectionService.OrganizeDesktopGames(_fenceService);
        }

        public void Learn(string filePath, string fenceName)
        {
            string extension = Path.GetExtension(filePath);
            if (!string.IsNullOrEmpty(extension))
            {
                lock (_rulesLock)
                {
                    _rules[extension] = fenceName;
                }
                _loggingService.LogInfo($"SmartSorter: Learned that {extension} belongs to {fenceName}");
                SaveRules();
            }
        }
    }
}
