using System;
using System.Collections.Generic;
using NoFences.Core;
using NoFences.Services;

namespace NoFences.Model
{
    /// <summary>
    /// Legacy FenceManager - now acts as a wrapper around FenceService for backward compatibility
    /// </summary>
    public class FenceManager
    {
        private static FenceManager _instance;
        public static FenceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new FenceManager();
                }
                return _instance;
            }
        }

        private IFenceService _fenceService;

        private FenceManager()
        {
            // Get service from DI container
            _fenceService = DependencyInjection.GetRequiredService<IFenceService>();
        }

        public void LoadFences()
        {
            _fenceService.LoadFences();
        }

        public void CreateFence(string name)
        {
            _fenceService.CreateFence(name);
        }

        public void RemoveFence(FenceInfo info)
        {
            _fenceService.RemoveFence(info);
        }

        public void UpdateFence(FenceInfo fenceInfo)
        {
            _fenceService.UpdateFence(fenceInfo);
        }

        public List<FenceInfo> GetAllFences()
        {
            return _fenceService.GetAllFences();
        }

        public FenceInfo GetFence(Guid id)
        {
            return _fenceService.GetFence(id);
        }
    }
}
