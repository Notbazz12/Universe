using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NoFences.Persistence;
using NoFences.Services;

namespace NoFences.Model
{
    /// <summary>
    /// Service interface for fence management
    /// </summary>
    public interface IFenceService
    {
        void LoadFences();
        void CreateFence(string name, int posX = 100, int posY = 250, int width = 300, int height = 300);
        FenceInfo CreateImageFence();
        FenceInfo CreateTaskFence();
        FenceInfo CreateSpecializedFence(string name, string filterType, int posX, int posY, int width, int height, int iconSize, List<string> initialFiles = null);
        void UpdateFence(FenceInfo fenceInfo);
        void RemoveFence(FenceInfo fenceInfo);
        List<FenceInfo> GetAllFences();
        FenceInfo GetFence(Guid id);
        void AddFileToFence(Guid fenceId, string filePath);
        void ReloadFence(Guid fenceId);
        void SetAllFencesVisible(bool visible);
        void CloseAllFences();
        bool AreFencesVisible { get; }
    }

    /// <summary>
    /// Fence management service (replaces FenceManager singleton).
    /// Maintains an in-memory cache (Dictionary) and synchronized window list.
    /// </summary>
    public class FenceService : IFenceService
    {
        private readonly IPersistenceService _persistenceService;
        private readonly ILoggingService _loggingService;
        private readonly List<FenceWindow> _openFences;
        private readonly Dictionary<Guid, FenceInfo> _fenceCache;
        private readonly object _syncLock = new object();
        private bool _areFencesVisible = true;

        public bool AreFencesVisible => _areFencesVisible;

        public FenceService(IPersistenceService persistenceService, ILoggingService loggingService)
        {
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _openFences = new List<FenceWindow>();
            _fenceCache = new Dictionary<Guid, FenceInfo>();
        }

        // Bound to the UI thread in LoadFences (not in the constructor: creating a control before
        // Application.SetUnhandledExceptionMode throws). Fences requested by background services
        // (smart sorter, watchers) are created through it; a Form created on a worker thread fails
        // with "DragDrop registration failed".
        private Control _uiInvoker;

        private bool NeedsUiThread => _uiInvoker != null && _uiInvoker.InvokeRequired;

        private T RunOnUiThread<T>(Func<T> action)
        {
            if (NeedsUiThread)
                return (T)_uiInvoker.Invoke(action);
            return action();
        }

        public void LoadFences()
        {
            _loggingService.LogInfo("Loading fences...");

            if (_uiInvoker == null)
            {
                _uiInvoker = new Control();
                _uiInvoker.CreateControl();
            }

            var fences = _persistenceService.LoadAllFences();

            lock (_syncLock)
            {
                _fenceCache.Clear();

                if (fences.Count == 0)
                {
                    _loggingService.LogInfo("No existing fences found");
                    return;
                }

                foreach (var fence in fences)
                {
                    // Ensure mascot companion is enabled on all fences
                    fence.ShowMascot = true;
                    _fenceCache[fence.Id] = fence;
                    try
                    {
                        var window = new FenceWindow(fence);
                        window.Show();
                        _openFences.Add(window);
                        _loggingService.LogDebug($"Loaded fence: {fence.Name}");
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError($"Failed to load fence: {fence.Name}", ex);
                    }
                }

                _loggingService.LogInfo($"Loaded {_openFences.Count} fences");
            }
        }

        public void CreateFence(string name, int posX = 100, int posY = 250, int width = 300, int height = 300)
        {
            if (NeedsUiThread)
            {
                RunOnUiThread(() => { CreateFence(name, posX, posY, width, height); return true; });
                return;
            }

            var fenceInfo = new FenceInfo(Guid.NewGuid())
            {
                Name = name,
                PosX = posX,
                PosY = posY,
                Width = width,
                Height = height,
                TitleHeight = 35,
                CanMinify = false, // open fully; roll-up is opt-in from the menu
                Locked = false
            };

            UpdateFence(fenceInfo); // persists + updates cache

            lock (_syncLock)
            {
                var window = new FenceWindow(fenceInfo);
                window.Show();
                _openFences.Add(window);
            }

            _loggingService.LogInfo($"Created new fence: {name}");
        }

        public FenceInfo CreateImageFence()
        {
            var desktopDirs = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };

            var foundImages = new List<string>();
            foreach (var dir in desktopDirs)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                try
                {
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        if (FenceFilterHelper.IsImageFile(file) && !foundImages.Contains(file))
                        {
                            foundImages.Add(file);
                        }
                    }
                }
                catch { }
            }

            // Fallback: If no images on desktop, import up to 8 sample images from MyPictures
            if (foundImages.Count == 0)
            {
                string pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (!string.IsNullOrEmpty(pics) && Directory.Exists(pics))
                {
                    try
                    {
                        foreach (var file in Directory.GetFiles(pics))
                        {
                            if (FenceFilterHelper.IsImageFile(file) && !foundImages.Contains(file))
                            {
                                foundImages.Add(file);
                                if (foundImages.Count >= 8) break;
                            }
                        }
                    }
                    catch { }
                }
            }

            return CreateSpecializedFence("🖼️ Images", "Images", 100, 200, 360, 320, 24, foundImages);
        }

        public FenceInfo CreateTaskFence()
        {
            var desktopDirs = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };

            var foundTasks = new List<string>();
            foreach (var dir in desktopDirs)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                try
                {
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        if (FenceFilterHelper.IsTaskFile(file) && !foundTasks.Contains(file))
                        {
                            foundTasks.Add(file);
                        }
                    }
                }
                catch { }
            }

            // If no task files exist on Desktop, create starter task note
            if (foundTasks.Count == 0)
            {
                try
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    if (!string.IsNullOrEmpty(desktop) && Directory.Exists(desktop))
                    {
                        string noteFile = Path.Combine(desktop, "📝 Mis Tareas.txt");
                        if (!File.Exists(noteFile))
                        {
                            File.WriteAllText(noteFile,
                                "=== 📝 MIS TAREAS / NOTAS ===\r\n" +
                                "Universe v2.0.11\r\n\r\n" +
                                "[ ] Tarea 1: Organizar archivos en Universe Fences\r\n" +
                                "[ ] Tarea 2: Probar zoom con Ctrl + Rueda del ratón\r\n" +
                                "[ ] Tarea 3: Personalizar temas y colores CyberGlass\r\n");
                        }
                        foundTasks.Add(noteFile);
                    }
                }
                catch { }
            }

            return CreateSpecializedFence("📝 Tasks & Notes", "Tasks", 480, 200, 340, 320, 20, foundTasks);
        }

        public FenceInfo CreateSpecializedFence(string name, string filterType, int posX, int posY, int width, int height, int iconSize, List<string> initialFiles = null)
        {
            if (NeedsUiThread)
                return RunOnUiThread(() => CreateSpecializedFence(name, filterType, posX, posY, width, height, iconSize, initialFiles));

            var fenceInfo = new FenceInfo(Guid.NewGuid())
            {
                Name = name,
                FilterType = filterType ?? "All",
                PosX = posX,
                PosY = posY,
                Width = width,
                Height = height,
                IconSize = iconSize > 0 ? iconSize : 24,
                TitleHeight = 35,
                CanMinify = false, // open fully; roll-up is opt-in from the menu
                Locked = false,
                ShowMascot = true,
                Files = initialFiles != null ? new List<string>(initialFiles) : new List<string>()
            };

            UpdateFence(fenceInfo);

            lock (_syncLock)
            {
                var window = new FenceWindow(fenceInfo);
                window.Show();
                _openFences.Add(window);
            }

            _loggingService.LogInfo($"Created specialized fence '{name}' (Filter={filterType}, IconSize={iconSize}) with {fenceInfo.Files.Count} items");
            return fenceInfo;
        }

        public void UpdateFence(FenceInfo fenceInfo)
        {
            if (fenceInfo == null)
                throw new ArgumentNullException(nameof(fenceInfo));

            lock (_syncLock)
            {
                _persistenceService.SaveFence(fenceInfo);
                _fenceCache[fenceInfo.Id] = fenceInfo; // keep cache in sync
            }
            _loggingService.LogDebug($"Updated fence: {fenceInfo.Name}");
        }

        public void RemoveFence(FenceInfo fenceInfo)
        {
            if (fenceInfo == null)
                throw new ArgumentNullException(nameof(fenceInfo));

            lock (_syncLock)
            {
                _persistenceService.DeleteFence(fenceInfo.Id);
                _fenceCache.Remove(fenceInfo.Id);

                // Find and properly close open window on the UI thread
                var windowsToClose = _openFences.Where(f => f.FenceId == fenceInfo.Id).ToList();
                foreach (var window in windowsToClose)
                {
                    _openFences.Remove(window);
                    if (!window.IsDisposed)
                    {
                        try
                        {
                            if (window.InvokeRequired)
                            {
                                window.BeginInvoke(new Action(() =>
                                {
                                    window.Close();
                                    window.Dispose();
                                }));
                            }
                            else
                            {
                                window.Close();
                                window.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Error closing fence window: {ex.Message}");
                        }
                    }
                }
            }

            _loggingService.LogInfo($"Removed fence: {fenceInfo.Name}");
        }

        public List<FenceInfo> GetAllFences()
        {
            lock (_syncLock)
            {
                return _fenceCache.Values.ToList();
            }
        }

        public FenceInfo GetFence(Guid id)
        {
            lock (_syncLock)
            {
                _fenceCache.TryGetValue(id, out var fence);
                return fence;
            }
        }

        public void AddFileToFence(Guid fenceId, string filePath)
        {
            var fence = GetFence(fenceId);
            if (fence == null) return;

            if (!FenceFilterHelper.MatchesFilter(filePath, fence.FilterType))
            {
                _loggingService.LogWarning($"Skipped adding '{filePath}' to fence '{fence.Name}' because it does not match filter '{fence.FilterType}'");
                return;
            }

            FenceWindow window;
            lock (_syncLock)
            {
                window = _openFences.Find(w => w.FenceId == fenceId);
            }

            if (window != null && !window.IsDisposed)
            {
                // The window owns its FenceInfo: mutate the file list on the UI thread only, so the
                // painting code never enumerates it while a background service (e.g. the smart
                // sorter) is adding to it.
                try
                {
                    if (window.InvokeRequired)
                        window.BeginInvoke(new Action(() => window.TryAddFile(filePath)));
                    else
                        window.TryAddFile(filePath);
                    return;
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }

            // No live window (should not happen): update the model and persist it directly
            lock (_syncLock)
            {
                if (fence.Files.Contains(filePath)) return;
                fence.Files.Add(filePath);
            }
            UpdateFence(fence);
        }

        public void ReloadFence(Guid fenceId)
        {
            lock (_syncLock)
            {
                var window = _openFences.Find(w => w.FenceId == fenceId);
                if (window == null || window.IsDisposed) return;

                try
                {
                    if (window.InvokeRequired)
                    {
                        window.BeginInvoke(new Action(() =>
                        {
                            if (!window.IsDisposed)
                            {
                                window.ReloadFiles();
                            }
                        }));
                    }
                    else
                    {
                        window.ReloadFiles();
                    }
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }
        }

        public void SetAllFencesVisible(bool visible)
        {
            lock (_syncLock)
            {
                _areFencesVisible = visible;
                foreach (var window in _openFences)
                {
                    if (window == null || window.IsDisposed) continue;

                    try
                    {
                        if (window.InvokeRequired)
                        {
                            window.BeginInvoke(new Action(() =>
                            {
                                if (!window.IsDisposed)
                                {
                                    if (visible) window.Show();
                                    else window.Hide();
                                }
                            }));
                        }
                        else
                        {
                            if (visible) window.Show();
                            else window.Hide();
                        }
                    }
                    catch { }
                }
            }

            _loggingService.LogInfo($"Fences visibility set to: {visible}");
        }

        public void CloseAllFences()
        {
            lock (_syncLock)
            {
                _loggingService.LogInfo("Closing all open fence windows...");
                foreach (var window in _openFences.ToArray())
                {
                    if (window != null && !window.IsDisposed)
                    {
                        try
                        {
                            window.FlushPendingSave();
                            window.Close();
                            window.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Error closing fence window on exit: {ex.Message}");
                        }
                    }
                }
                _openFences.Clear();
            }

            // Make sure debounced saves reach the disk before the process goes away
            _persistenceService.Flush();
            AppConfig.Flush();
        }
    }
}
