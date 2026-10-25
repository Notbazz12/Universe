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
        int SortDesktopPrograms();
        int SortDesktopImages();
        int SortDesktopTasks();
        (int gamesCount, int programsCount) SortAllDesktop();
    }

    public class SmartSorterService : ISmartSorterService
    {
        private readonly IFenceService _fenceService;
        private readonly ILoggingService _loggingService;
        private readonly IGameDetectionService _gameDetectionService;
        private readonly IProgramDetectionService _programDetectionService;
        private FileSystemWatcher _watcher;
        private readonly string _desktopPath;
        private Dictionary<string, string> _rules; // Extension -> FenceName
        private readonly object _rulesLock = new object();

        public SmartSorterService(
            IFenceService fenceService,
            ILoggingService loggingService,
            IGameDetectionService gameDetectionService,
            IProgramDetectionService programDetectionService)
        {
            _fenceService = fenceService ?? throw new ArgumentNullException(nameof(fenceService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _gameDetectionService = gameDetectionService ?? throw new ArgumentNullException(nameof(gameDetectionService));
            _programDetectionService = programDetectionService ?? throw new ArgumentNullException(nameof(programDetectionService));
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
                    // Only creation/rename matter (no Changed handler), so skip LastWrite noise
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    InternalBufferSize = 64 * 1024, // default 8 KB overflows when many files land at once
                    EnableRaisingEvents = true
                };
                _watcher.Created += OnFileCreated;
                _watcher.Renamed += OnFileRenamed;
                _watcher.Error += OnWatcherError;
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
                    _watcher.Error -= OnWatcherError;
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

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            // Typically an internal buffer overflow: restart the watcher instead of silently going deaf
            _loggingService.LogWarning("SmartSorter: watcher error, restarting: " + e.GetException().Message);
            Task.Run(() => Start());
        }

        // Created + Renamed (and repeated events) for the same path collapse into one run
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _inFlight =
            new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        private static bool IsTransientFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return true;
            return fileName.StartsWith("~$", StringComparison.Ordinal)
                || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".partial", StringComparison.OrdinalIgnoreCase);
        }

        private void ProcessNewFile(string fullPath, string fileName)
        {
            if (IsTransientFile(fileName)) return;
            if (!_inFlight.TryAdd(fullPath, 0)) return;

            Task.Run(async () =>
            {
                try
                {
                    // Short delay to let the creating process finish writing/closing the handle
                    await Task.Delay(600);

                    if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) return;

                    var fences = _fenceService.GetAllFences();

                    // 1. GAME DETECTION CHECK
                    if (_gameDetectionService.IsGame(fullPath, out var gameTitle))
                    {
                        _loggingService.LogInfo($"SmartSorter: Detected game '{gameTitle}' at {fileName}");
                        AddGameToAppropriateFence(fullPath, fences);
                        return;
                    }

                    // 2. PROGRAM / APP DETECTION CHECK
                    if (_programDetectionService.IsProgram(fullPath, out var programTitle))
                    {
                        _loggingService.LogInfo($"SmartSorter: Detected program '{programTitle}' at {fileName}");
                        AddProgramToAppropriateFence(fullPath, fences);
                        return;
                    }

                    // 3. DIRECTORY CHECK (Folders containing multiple games or programs)
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

                        if (_programDetectionService.IsProgramDirectory(fullPath, out var progsInDir) && progsInDir.Count > 0)
                        {
                            _loggingService.LogInfo($"SmartSorter: Detected folder with {progsInDir.Count} programs at {fileName}");
                            foreach (var prog in progsInDir)
                            {
                                AddProgramToAppropriateFence(prog, fences);
                            }
                            return;
                        }
                    }

                    // 3.5 SPECIALIZED FENCE CHECKS (Images & Tasks)
                    if (FenceFilterHelper.IsImageFile(fullPath))
                    {
                        var imageFence = fences.FirstOrDefault(f => f.FilterType == "Images" || f.Name.IndexOf("Images", StringComparison.OrdinalIgnoreCase) >= 0 || f.Name.IndexOf("Imágenes", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (imageFence != null)
                        {
                            _fenceService.AddFileToFence(imageFence.Id, fullPath);
                            _loggingService.LogInfo($"SmartSorter: Auto-routed new image '{fileName}' to '{imageFence.Name}'");
                            return;
                        }
                    }

                    if (FenceFilterHelper.IsTaskFile(fullPath))
                    {
                        var taskFence = fences.FirstOrDefault(f => f.FilterType == "Tasks" || f.Name.IndexOf("Tasks", StringComparison.OrdinalIgnoreCase) >= 0 || f.Name.IndexOf("Tareas", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (taskFence != null)
                        {
                            _fenceService.AddFileToFence(taskFence.Id, fullPath);
                            _loggingService.LogInfo($"SmartSorter: Auto-routed new task '{fileName}' to '{taskFence.Name}'");
                            return;
                        }
                    }

                    // 4. REGULAR EXTENSION RULES
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
                finally
                {
                    _inFlight.TryRemove(fullPath, out _);
                }
            });
        }

        private void AddGameToAppropriateFence(string filePath, List<FenceInfo> fences)
        {
            var targetFence = _gameDetectionService.FindBestFenceForGames(fences);

            if (targetFence == null)
            {
                _loggingService.LogInfo("SmartSorter: Creating '🎮 Games' fence...");
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

        private void AddProgramToAppropriateFence(string filePath, List<FenceInfo> fences)
        {
            var targetFence = _programDetectionService.FindBestFenceForPrograms(fences);

            if (targetFence == null)
            {
                _loggingService.LogInfo("SmartSorter: Creating '⚡ Programs' fence...");
                _fenceService.CreateFence("⚡ Programs", 560, 180, 420, 340);
                fences = _fenceService.GetAllFences();
                targetFence = _programDetectionService.FindBestFenceForPrograms(fences) ?? fences.LastOrDefault();
            }

            if (targetFence != null)
            {
                _fenceService.AddFileToFence(targetFence.Id, filePath);
                _loggingService.LogInfo($"SmartSorter: Program added to fence '{targetFence.Name}' -> {Path.GetFileName(filePath)}");
            }
        }

        public int SortDesktopGames()
        {
            _loggingService.LogInfo("SmartSorter: Triggered manual desktop games sort");
            return _gameDetectionService.OrganizeDesktopGames(_fenceService);
        }

        public int SortDesktopPrograms()
        {
            _loggingService.LogInfo("SmartSorter: Triggered manual desktop programs sort");
            return _programDetectionService.OrganizeDesktopPrograms(_fenceService);
        }

        public int SortDesktopImages()
        {
            _loggingService.LogInfo("SmartSorter: Triggered manual desktop images sort");
            var fences = _fenceService.GetAllFences();
            var targetFence = fences.FirstOrDefault(f => f.FilterType == "Images" || f.Name.IndexOf("Images", StringComparison.OrdinalIgnoreCase) >= 0 || f.Name.IndexOf("Imágenes", StringComparison.OrdinalIgnoreCase) >= 0);

            if (targetFence == null)
            {
                var newFence = _fenceService.CreateImageFence();
                return newFence?.Files?.Count ?? 0;
            }

            int count = 0;
            string[] desktopDirs = {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };

            foreach (var dir in desktopDirs)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                try
                {
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        if (FenceFilterHelper.IsImageFile(file) && !targetFence.Files.Contains(file))
                        {
                            _fenceService.AddFileToFence(targetFence.Id, file);
                            count++;
                        }
                    }
                }
                catch { }
            }
            return count;
        }

        public int SortDesktopTasks()
        {
            _loggingService.LogInfo("SmartSorter: Triggered manual desktop tasks sort");
            var fences = _fenceService.GetAllFences();
            var targetFence = fences.FirstOrDefault(f => f.FilterType == "Tasks" || f.Name.IndexOf("Tasks", StringComparison.OrdinalIgnoreCase) >= 0 || f.Name.IndexOf("Tareas", StringComparison.OrdinalIgnoreCase) >= 0);

            if (targetFence == null)
            {
                var newFence = _fenceService.CreateTaskFence();
                return newFence?.Files?.Count ?? 0;
            }

            int count = 0;
            string[] desktopDirs = {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };

            foreach (var dir in desktopDirs)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                try
                {
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        if (FenceFilterHelper.IsTaskFile(file) && !targetFence.Files.Contains(file))
                        {
                            _fenceService.AddFileToFence(targetFence.Id, file);
                            count++;
                        }
                    }
                }
                catch { }
            }
            return count;
        }

        public (int gamesCount, int programsCount) SortAllDesktop()
        {
            _loggingService.LogInfo("SmartSorter: Triggered full desktop sort (Games & Programs)");
            int gCount = SortDesktopGames();
            int pCount = SortDesktopPrograms();
            return (gCount, pCount);
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
