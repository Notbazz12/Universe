using System;
using System.Collections.Generic;
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
    }

    /// <summary>
    /// Fence management service (replaces FenceManager singleton)
    /// </summary>
    public class FenceService : IFenceService
    {
        private readonly IPersistenceService _persistenceService;
        private readonly ILoggingService _loggingService;
        private readonly List<FenceWindow> _openFences;

        public FenceService(IPersistenceService persistenceService, ILoggingService loggingService)
        {
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _openFences = new List<FenceWindow>();
        }

        public void LoadFences()
        {
            _loggingService.LogInfo("Loading fences...");

            var fences = _persistenceService.LoadAllFences();
            
            if (fences.Count == 0)
            {
                _loggingService.LogInfo("No existing fences found");
                return;
            }

            foreach (var fence in fences)
            {
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

            UpdateFence(fenceInfo);
            
            var window = new FenceWindow(fenceInfo);
            window.Show();
            _openFences.Add(window);

            _loggingService.LogInfo($"Created new fence: {name}");
        }

        public void UpdateFence(FenceInfo fenceInfo)
        {
            if (fenceInfo == null)
                throw new ArgumentNullException(nameof(fenceInfo));

            _persistenceService.SaveFence(fenceInfo);
            _loggingService.LogDebug($"Updated fence: {fenceInfo.Name}");
        }

        public void RemoveFence(FenceInfo fenceInfo)
        {
            if (fenceInfo == null)
                throw new ArgumentNullException(nameof(fenceInfo));

            _persistenceService.DeleteFence(fenceInfo.Id);
            
            // Remove from open fences list
            _openFences.RemoveAll(f => f.Text == fenceInfo.Name);

            _loggingService.LogInfo($"Removed fence: {fenceInfo.Name}");
        }

        public List<FenceInfo> GetAllFences()
        {
            return _persistenceService.LoadAllFences();
        }

        public FenceInfo GetFence(Guid id)
        {
            return _persistenceService.LoadFence(id);
        }

        public void AddFileToFence(Guid fenceId, string filePath)
        {
            var fence = GetFence(fenceId);
            if (fence != null)
            {
                if (!fence.Files.Contains(filePath))
                {
                    fence.Files.Add(filePath);
                    UpdateFence(fence);
                    
                    // Refresh window if open
                    var window = _openFences.Find(w => w.FenceId == fenceId);
                    if (window != null)
                    {
                        window.Invoke(new Action(() => window.ReloadFiles()));
                    }
                }
            }
        }
    }
}
