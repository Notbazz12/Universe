using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NoFences.Model;
using NoFences.Win32;

namespace NoFences.Services
{
    public class ProgramDetectionService : IProgramDetectionService
    {
        private readonly ILoggingService _loggingService;
        private readonly IGameDetectionService _gameDetectionService;
        private readonly ConcurrentDictionary<string, (bool isProgram, string title)> _cache =
            new ConcurrentDictionary<string, (bool, string)>(StringComparer.OrdinalIgnoreCase);

        // Standard Windows Program Directory Signatures
        private static readonly string[] ProgramDirectorySignatures = new[]
        {
            @"\program files\", @"\program files (x86)\", @"\appdata\local\programs\",
            @"\appdata\roaming\", @"\appdata\local\", @"\windows\system32\",
            @"\windows\syswow64\", @"\bin\", @"\tools\", @"\software\"
        };

        // Folders indicative of software/programs
        private static readonly string[] ProgramFolderKeywords = new[]
        {
            "program", "app", "herramienta", "tool", "software", "util",
            "dev", "trabajo", "work", "office", "productiv"
        };

        // Well known desktop application keywords (Browsers, Dev Tools, Creative, Utilities)
        private static readonly string[] KnownAppKeywords = new[]
        {
            "chrome", "firefox", "edge", "brave", "opera", "code", "visual studio",
            "photoshop", "illustrator", "premiere", "after effects", "blender",
            "discord", "telegram", "whatsapp", "slack", "zoom", "teams", "skype",
            "spotify", "vlc", "obs studio", "obs", "audacity", "handbrake",
            "word", "excel", "powerpoint", "outlook", "onenote", "access",
            "git", "github", "docker", "postman", "figma", "notion", "canva",
            "7-zip", "winrar", "notepad++", "sublime", "terminal", "powershell",
            "calculator", "paint", "gimp", "inkscape", "anydesk", "teamviewer"
        };

        public ProgramDetectionService(ILoggingService loggingService, IGameDetectionService gameDetectionService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _gameDetectionService = gameDetectionService ?? throw new ArgumentNullException(nameof(gameDetectionService));
        }

        public bool IsProgram(string path, out string programTitle)
        {
            programTitle = null;
            if (string.IsNullOrEmpty(path)) return false;

            if (_cache.TryGetValue(path, out var cached))
            {
                programTitle = cached.title;
                return cached.isProgram;
            }

            bool detected = DetectProgramInternal(path, out programTitle);
            _cache[path] = (detected, programTitle);
            return detected;
        }

        private bool DetectProgramInternal(string path, out string programTitle)
        {
            programTitle = Path.GetFileNameWithoutExtension(path);

            // 1. Directory Detection
            if (Directory.Exists(path))
            {
                if (IsProgramDirectory(path, out var appsInDir) && appsInDir.Count > 0)
                {
                    return true;
                }
                return false;
            }

            if (!File.Exists(path)) return false;

            // 2. Games are handled by GameDetectionService, not as generic programs
            if (_gameDetectionService.IsGame(path, out _))
            {
                return false;
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();

            // 3. Windows Shortcut (.lnk)
            if (ext == ".lnk")
            {
                var shortcut = ShellLinkHelper.Resolve(path);
                if (shortcut != null)
                {
                    string target = shortcut.TargetPath ?? string.Empty;
                    if (!string.IsNullOrEmpty(target))
                    {
                        string targetExt = Path.GetExtension(target).ToLowerInvariant();
                        if (targetExt == ".exe" || targetExt == ".msc" || targetExt == ".cpl" || targetExt == ".bat" || targetExt == ".cmd")
                        {
                            // If target is an executable and not a game, it is an application/program!
                            return true;
                        }

                        string targetLower = target.ToLowerInvariant();
                        if (ProgramDirectorySignatures.Any(sig => targetLower.Contains(sig)))
                        {
                            return true;
                        }
                    }
                }
            }

            // 4. Direct Executable (.exe)
            if (ext == ".exe")
            {
                // Already verified it's not a game above
                return true;
            }

            // 5. Name keyword matching
            string nameLower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (KnownAppKeywords.Any(kw => nameLower.Contains(kw)))
            {
                return true;
            }

            return false;
        }

        public bool IsProgramDirectory(string dirPath, out List<string> detectedPrograms)
        {
            detectedPrograms = new List<string>();
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
                return false;

            string dirName = Path.GetFileName(dirPath).ToLowerInvariant();
            bool isNamedPrograms = ProgramFolderKeywords.Any(kw => dirName.Contains(kw));

            try
            {
                var files = Directory.GetFiles(dirPath, "*.*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    if (IsProgram(file, out _))
                    {
                        detectedPrograms.Add(file);
                    }
                }

                if (detectedPrograms.Count >= 2 || (isNamedPrograms && detectedPrograms.Count >= 1))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"ProgramDetection: Error inspecting directory {dirPath}: {ex.Message}");
            }

            return false;
        }

        public FenceInfo FindBestFenceForPrograms(IEnumerable<FenceInfo> fences)
        {
            if (fences == null) return null;

            var fenceList = fences.ToList();

            // 1. First Priority: A fence explicitly named Programs / Programas / Apps / Aplicaciones / Software
            var namedFence = fenceList.FirstOrDefault(f =>
                !string.IsNullOrEmpty(f.Name) &&
                (f.Name.IndexOf("program", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 f.Name.IndexOf("app", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 f.Name.IndexOf("software", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 f.Name.IndexOf("herramienta", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 f.Name.IndexOf("tool", StringComparison.OrdinalIgnoreCase) >= 0));

            if (namedFence != null)
                return namedFence;

            // 2. Second Priority: An existing fence that already contains multiple programs ("si hay uno con varios programas")
            FenceInfo bestExistingFence = null;
            int maxProgramCount = 0;

            foreach (var fence in fenceList)
            {
                if (fence.Files == null || fence.Files.Count == 0) continue;

                int programCount = fence.Files.Count(f => IsProgram(f, out _));
                if (programCount >= 2 && programCount > maxProgramCount)
                {
                    maxProgramCount = programCount;
                    bestExistingFence = fence;
                }
            }

            return bestExistingFence;
        }

        public List<string> ScanDesktopForPrograms()
        {
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // User Desktop
            string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            ScanFolderForPrograms(userDesktop, results);

            // Common/Public Desktop
            string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            if (!string.IsNullOrEmpty(publicDesktop) && Directory.Exists(publicDesktop))
            {
                ScanFolderForPrograms(publicDesktop, results);
            }

            return results.ToList();
        }

        private void ScanFolderForPrograms(string folder, HashSet<string> results)
        {
            if (!Directory.Exists(folder)) return;

            try
            {
                var files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    if (IsProgram(file, out _))
                    {
                        results.Add(file);
                    }
                }

                var dirs = Directory.GetDirectories(folder);
                foreach (var dir in dirs)
                {
                    if (IsProgramDirectory(dir, out var programsInFolder))
                    {
                        foreach (var p in programsInFolder)
                        {
                            results.Add(p);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"ProgramDetection: Error scanning {folder}", ex);
            }
        }

        public int OrganizeDesktopPrograms(IFenceService fenceService)
        {
            if (fenceService == null) return 0;

            var programs = ScanDesktopForPrograms();
            if (programs.Count == 0)
            {
                _loggingService.LogInfo("ProgramDetection: No programs detected on desktop");
                return 0;
            }

            var allFences = fenceService.GetAllFences();
            var targetFence = FindBestFenceForPrograms(allFences);

            // If no program fence exists, create one!
            if (targetFence == null)
            {
                _loggingService.LogInfo("ProgramDetection: Creating new Programs fence");
                fenceService.CreateFence("⚡ Programs", 560, 180, 420, 340);
                allFences = fenceService.GetAllFences();
                targetFence = FindBestFenceForPrograms(allFences) ?? allFences.LastOrDefault();
            }

            if (targetFence == null) return 0;

            int addedCount = 0;
            foreach (var prog in programs)
            {
                if (!targetFence.Files.Contains(prog))
                {
                    fenceService.AddFileToFence(targetFence.Id, prog);
                    addedCount++;
                }
            }

            _loggingService.LogInfo($"ProgramDetection: Organized {addedCount} programs into fence '{targetFence.Name}'");
            return addedCount;
        }
    }
}
