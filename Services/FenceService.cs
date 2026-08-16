using System;
using System.Collections.Generic;
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

        public void LoadFences()
        {
            _loggingService.LogInfo("Loading fences...");

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
            var fenceInfo = new FenceInfo(Guid.NewGuid())
            {
                Name = name,
                PosX = posX,
                PosY = posY,
                Width = width,
                Height = height,
                TitleHeight = 35,
                CanMinify = true,
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

            lock (_syncLock)
            {
                if (fence.Files.Contains(filePath)) return;
                fence.Files.Add(filePath);
            }

            UpdateFence(fence);
            ReloadFence(fenceId);
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
        }
    }
}
