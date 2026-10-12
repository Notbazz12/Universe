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
    public class GameDetectionService : IGameDetectionService
    {
        private readonly ILoggingService _loggingService;
        private readonly ConcurrentDictionary<string, (bool isGame, string title)> _cache =
            new ConcurrentDictionary<string, (bool, string)>(StringComparer.OrdinalIgnoreCase);

        // Game Platforms & Launchers directory signatures
        private static readonly string[] GamePathSignatures = new[]
        {
            @"\steam\", @"\steamapps\common\", @"\epic games\", @"\riot games\",
            @"\battle.net\", @"\blizzard\", @"\ubisoft\", @"\ubisoft game launcher\",
            @"\gog galaxy\", @"\gog games\", @"\electronic arts\", @"\ea games\",
            @"\origin games\", @"\xboxgames\", @"\windowsapps\", @"\roblox\",
            @"\minecraft\", @".minecraft", @"\genshin impact\", @"\hoyoverse\",
            @"\zenless\", @"\star rail\", @"\emulators\", @"\retroarch\"
        };

        // Common Game URL protocols
        private static readonly string[] GameUrlProtocols = new[]
        {
            "steam://rungameid/",
            "com.epicgames.launcher://apps/",
            "riotgames://",
            "battlenet://",
            "uplay://",
            "origin://"
        };

        // Popular game titles and keywords
        private static readonly string[] GameKeywords = new[]
        {
            "game", "juego", "gta", "grand theft auto", "minecraft", "roblox", "fortnite",
            "valorant", "league of legends", "counter-strike", "csgo", "cs2", "overwatch",
            "cyberpunk", "elden ring", "dota", "apex legends", "call of duty", "warzone",
            "fifa", "pes", "fc 24", "fc 25", "forza", "sims", "witcher", "god of war",
            "red dead", "fall guys", "rocket league", "among us", "terraria", "stardew valley",
            "hollow knight", "dark souls", "sekiro", "palworld", "helldivers", "resident evil",
            "assassin's creed", "far cry", "fallout", "skyrim", "doom", "halo", "battlefield",
            "need for speed", "genshin impact", "honkai", "star rail", "zenless zone zero",
            "pubg", "escape from tarkov", "tarkov", "rust", "ark", "sea of thieves",
            "world of warcraft", "diablo", "starcraft", "hearthstone", "monster hunter",
            "persona", "final fantasy", "tekken", "mortal kombat", "street fighter",
            "super smash", "pokemon", "zelda", "mario", "sonic", "osu!", "geometry dash",
            "cuphead", "undertale", "deltarune", "celeste", "hades", "dead cells",
            "slay the spire", "balatro", "lethal company", "phasmophobia", "content warning",
            "factorio", "rimworld", "civilization", "age of empires", "cities skylines",
            "euro truck", "beamng", "garry's mod", "gmod", "left 4 dead", "portal",
            "half-life", "payday", "subnautica", "no man's sky", "hitman", "dishonored",
            "bioshock", "borderlands", "tomb raider", "batman arkham", "spiderman",
            "dragon ball", "naruto", "yakuza", "like a dragon", "persona 5", "persona 3"
        };

        // Game Publishers & Engines in Executable metadata
        private static readonly string[] GamePublishers = new[]
        {
            "Valve", "Ubisoft", "Epic Games", "Riot Games", "Blizzard", "Activision",
            "Bethesda", "Electronic Arts", "EA Games", "Capcom", "Bandai Namco",
            "FromSoftware", "Rockstar Games", "Sega", "Square Enix", "CD Projekt",
            "Nintendo", "Sony Interactive", "Xbox Game Studios", "2K Games",
            "Paradox Interactive", "Unity Technologies", "Unreal Engine", "Godot"
        };

        // ROM and Disc Image Extensions
        private static readonly string[] GameRomExtensions = new[]
        {
            ".iso", ".cso", ".nsp", ".xci", ".rom", ".gba", ".nds", ".3ds", ".n64", ".sfc", ".nes"
        };

        public GameDetectionService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public bool IsGame(string path, out string gameTitle)
        {
            gameTitle = null;
            if (string.IsNullOrEmpty(path)) return false;

            if (_cache.TryGetValue(path, out var cached))
            {
                gameTitle = cached.title;
                return cached.isGame;
            }

            bool detected = DetectGameInternal(path, out gameTitle);
            _cache[path] = (detected, gameTitle);
            return detected;
        }

        private bool DetectGameInternal(string path, out string gameTitle)
        {
            gameTitle = Path.GetFileNameWithoutExtension(path);

            // 1. Directory Detection
            if (Directory.Exists(path))
            {
                if (IsGameDirectory(path, out var gamesInDir) && gamesInDir.Count > 0)
                {
                    return true;
                }
                return false;
            }

            if (!File.Exists(path)) return false;

            string ext = Path.GetExtension(path).ToLowerInvariant();

            // 2. Internet Shortcuts (.url) - Typical Steam, Epic, Riot shortcuts
            if (ext == ".url")
            {
                var shortcut = ShellLinkHelper.Resolve(path);
                if (shortcut != null && !string.IsNullOrEmpty(shortcut.Url))
                {
                    foreach (var proto in GameUrlProtocols)
                    {
                        if (shortcut.Url.StartsWith(proto, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            // 3. ROMs & Emulated Games
            if (GameRomExtensions.Contains(ext))
            {
                return true;
            }

            // 4. Windows Shortcuts (.lnk)
            if (ext == ".lnk")
            {
                var shortcut = ShellLinkHelper.Resolve(path);
                if (shortcut != null)
                {
                    string target = shortcut.TargetPath ?? string.Empty;
                    string args = shortcut.Arguments ?? string.Empty;

                    // Target path inside known game folders
                    if (!string.IsNullOrEmpty(target))
                    {
                        string targetLower = target.ToLowerInvariant();
                        if (GamePathSignatures.Any(sig => targetLower.Contains(sig)))
                        {
                            return true;
                        }

                        // Check target metadata
                        if (File.Exists(target) && IsGameExecutable(target))
                        {
                            return true;
                        }
                    }

                    // Check arguments (e.g. steam.exe -applaunch 730)
                    if (!string.IsNullOrEmpty(args) && (args.Contains("-applaunch") || args.Contains("rungameid")))
                    {
                        return true;
                    }
                }
            }

            // 5. Direct Executables (.exe)
            if (ext == ".exe")
            {
                if (IsGameExecutable(path))
                {
                    return true;
                }
            }

            // 6. Name keyword matching
            string nameWithoutExt = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (GameKeywords.Any(kw => nameWithoutExt.Contains(kw)))
            {
                return true;
            }

            return false;
        }

        private bool IsGameExecutable(string exePath)
        {
            try
            {
                string pathLower = exePath.ToLowerInvariant();
                if (GamePathSignatures.Any(sig => pathLower.Contains(sig)))
                    return true;

                var vi = FileVersionInfo.GetVersionInfo(exePath);
                string company = vi.CompanyName ?? string.Empty;
                string description = vi.FileDescription ?? string.Empty;
                string product = vi.ProductName ?? string.Empty;

                foreach (var pub in GamePublishers)
                {
                    if (company.IndexOf(pub, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        description.IndexOf(pub, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        product.IndexOf(pub, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public bool IsGameDirectory(string dirPath, out List<string> detectedGames)
        {
            detectedGames = new List<string>();
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
                return false;

            string dirName = Path.GetFileName(dirPath).ToLowerInvariant();
            bool isNamedGames = dirName.Contains("juego") || dirName.Contains("game") || dirName.Contains("gaming");

            try
            {
                // Scan files in this folder (top level and 1 subfolder level)
                var files = Directory.GetFiles(dirPath, "*.*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    if (IsGame(file, out _))
                    {
                        detectedGames.Add(file);
                    }
                }

                // If folder is named games, or contains 2 or more games, it's considered a game folder
                if (detectedGames.Count >= 2 || (isNamedGames && detectedGames.Count >= 1))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"GameDetection: Error inspecting directory {dirPath}: {ex.Message}");
            }

            return false;
        }

        public FenceInfo FindBestFenceForGames(IEnumerable<FenceInfo> fences)
        {
            if (fences == null) return null;

            var fenceList = fences.ToList();

            // 1. First Priority: A fence explicitly named Games / Juegos / Gaming
            var namedFence = fenceList.FirstOrDefault(f =>
                !string.IsNullOrEmpty(f.Name) &&
                (f.Name.IndexOf("juego", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 f.Name.IndexOf("game", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 f.Name.IndexOf("gaming", StringComparison.OrdinalIgnoreCase) >= 0));

            if (namedFence != null)
                return namedFence;

            // 2. Second Priority: An existing fence that already contains multiple games ("si hay uno con varios juegos")
            FenceInfo bestExistingFence = null;
            int maxGameCount = 0;

            foreach (var fence in fenceList)
            {
                if (fence.Files == null || fence.Files.Count == 0) continue;

                int gameCount = fence.Files.Count(f => IsGame(f, out _));
                if (gameCount >= 2 && gameCount > maxGameCount)
                {
                    maxGameCount = gameCount;
                    bestExistingFence = fence;
                }
            }

            return bestExistingFence;
        }

        public List<string> ScanDesktopForGames()
        {
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // User Desktop
            string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            ScanFolderForGames(userDesktop, results);

            // Common/Public Desktop (Steam & Epic often install shortcuts here)
            string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            if (!string.IsNullOrEmpty(publicDesktop) && Directory.Exists(publicDesktop))
            {
                ScanFolderForGames(publicDesktop, results);
            }

            return results.ToList();
        }

        private void ScanFolderForGames(string folder, HashSet<string> results)
        {
            if (!Directory.Exists(folder)) return;

            try
            {
                // Files on desktop
                var files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    if (IsGame(file, out _))
                    {
                        results.Add(file);
                    }
                }

                // Directories on desktop: check if any directory contains games ("si hay uno con varios juegos")
                var dirs = Directory.GetDirectories(folder);
                foreach (var dir in dirs)
                {
                    if (IsGameDirectory(dir, out var gamesInFolder))
                    {
                        foreach (var g in gamesInFolder)
                        {
                            results.Add(g);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"GameDetection: Error scanning {folder}", ex);
            }
        }

        public int OrganizeDesktopGames(IFenceService fenceService)
        {
            if (fenceService == null) return 0;

            var games = ScanDesktopForGames();
            if (games.Count == 0)
            {
                _loggingService.LogInfo("GameDetection: No games detected on desktop");
                return 0;
            }

            var allFences = fenceService.GetAllFences();
            var targetFence = FindBestFenceForGames(allFences);

            // If no game fence exists, create one!
            if (targetFence == null)
            {
                _loggingService.LogInfo("GameDetection: Creating new Games fence");
                fenceService.CreateFence("🎮 Games", 120, 180, 420, 340);
                allFences = fenceService.GetAllFences();
                targetFence = FindBestFenceForGames(allFences) ?? allFences.LastOrDefault();
            }

            if (targetFence == null) return 0;

            int addedCount = 0;
            foreach (var game in games)
            {
                if (!targetFence.Files.Contains(game))
                {
                    fenceService.AddFileToFence(targetFence.Id, game);
                    addedCount++;
                }
            }

            _loggingService.LogInfo($"GameDetection: Organized {addedCount} games into fence '{targetFence.Name}'");
            return addedCount;
        }
    }
}
