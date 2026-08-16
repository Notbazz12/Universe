using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NoFences.Model;
using NoFences.Services;

namespace NoFences.Persistence
{
    /// <summary>
    /// JSON-based persistence service for storing fence configurations
    /// </summary>
    public interface IPersistenceService
    {
        void SaveFence(FenceInfo fenceInfo);
        FenceInfo LoadFence(Guid fenceId);
        List<FenceInfo> LoadAllFences();
        void DeleteFence(Guid fenceId);
        bool FenceExists(Guid fenceId);
    }

    public class JsonPersistenceService : IPersistenceService
    {
        private const string MetaFileName = "fence.json";
        private readonly string _basePath;
        private readonly JsonSerializerSettings _jsonSettings;
        private readonly ILoggingService _loggingService;
        private readonly object _fileLock = new object();

        public JsonPersistenceService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            
            _basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NoFences",
                "Fences"
            );

            _jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Populate
            };

            EnsureDirectoryExists(_basePath);
        }

        public void SaveFence(FenceInfo fenceInfo)
        {
            if (fenceInfo == null)
                throw new ArgumentNullException(nameof(fenceInfo));

            var fencePath = GetFencePath(fenceInfo.Id);
            EnsureDirectoryExists(fencePath);

            var metaFile = Path.Combine(fencePath, MetaFileName);
            var tempFile = Path.Combine(fencePath, MetaFileName + ".tmp");
            var json = JsonConvert.SerializeObject(fenceInfo, _jsonSettings);

            lock (_fileLock)
            {
                try
                {
                    // Atomic write: write to temp file first, then replace live file
                    File.WriteAllText(tempFile, json);
                    if (File.Exists(metaFile))
                    {
                        File.Delete(metaFile);
                    }
                    File.Move(tempFile, metaFile);
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Failed to save fence {fenceInfo.Name} atomically: {ex.Message}", ex);
                    // Fallback direct write
                    File.WriteAllText(metaFile, json);
                }
            }
        }

        public FenceInfo LoadFence(Guid fenceId)
        {
            var fencePath = GetFencePath(fenceId);
            var metaFile = Path.Combine(fencePath, MetaFileName);

            if (!File.Exists(metaFile))
                return null;

            lock (_fileLock)
            {
                try
                {
                    var json = File.ReadAllText(metaFile);
                    return JsonConvert.DeserializeObject<FenceInfo>(json, _jsonSettings);
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Failed to load fence {fenceId}: {ex.Message}", ex);
                    return null;
                }
            }
        }

        public List<FenceInfo> LoadAllFences()
        {
            var fences = new List<FenceInfo>();

            if (!Directory.Exists(_basePath))
                return fences;

            foreach (var dir in Directory.EnumerateDirectories(_basePath))
            {
                var metaFile = Path.Combine(dir, MetaFileName);
                if (File.Exists(metaFile))
                {
                    try
                    {
                        string json;
                        lock (_fileLock)
                        {
                            json = File.ReadAllText(metaFile);
                        }
                        var fence = JsonConvert.DeserializeObject<FenceInfo>(json, _jsonSettings);
                        if (fence != null)
                        {
                            fences.Add(fence);
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Failed to load fence from {metaFile}: {ex.Message}");
                        continue;
                    }
                }
            }

            return fences;
        }

        public void DeleteFence(Guid fenceId)
        {
            var fencePath = GetFencePath(fenceId);
            lock (_fileLock)
            {
                if (Directory.Exists(fencePath))
                {
                    try
                    {
                        Directory.Delete(fencePath, true);
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError($"Failed to delete fence directory {fencePath}: {ex.Message}", ex);
                    }
                }
            }
        }

        public bool FenceExists(Guid fenceId)
        {
            var fencePath = GetFencePath(fenceId);
            var metaFile = Path.Combine(fencePath, MetaFileName);
            return File.Exists(metaFile);
        }

        private string GetFencePath(Guid fenceId)
        {
            return Path.Combine(_basePath, fenceId.ToString());
        }

        private void EnsureDirectoryExists(string path)
        {
            var dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                dirInfo.Create();
            }
        }
    }
}
