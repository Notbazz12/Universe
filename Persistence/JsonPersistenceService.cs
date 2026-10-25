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
        /// <summary>Writes any pending debounced saves to disk immediately.</summary>
        void Flush();
    }

    public class JsonPersistenceService : IPersistenceService, IDisposable
    {
        private const string MetaFileName = "fence.json";
        private readonly string _basePath;
        private readonly JsonSerializerSettings _jsonSettings;
        private readonly ILoggingService _loggingService;
        private readonly object _fileLock = new object();
        private readonly DeferredFileWriter _writer;

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

            _writer = new DeferredFileWriter(TimeSpan.FromMilliseconds(750),
                (path, ex) => _loggingService.LogError($"Failed to save {path}: {ex.Message}", ex));

            EnsureDirectoryExists(_basePath);
        }

        public void SaveFence(FenceInfo fenceInfo)
        {
            if (fenceInfo == null)
                throw new ArgumentNullException(nameof(fenceInfo));

            // Serialize now (consistent snapshot on the caller's thread); the disk write is
            // debounced, coalesced and performed atomically on a background thread.
            var json = JsonConvert.SerializeObject(fenceInfo, _jsonSettings);
            _writer.Schedule(GetMetaFile(fenceInfo.Id), json);
        }

        public void Flush()
        {
            _writer.Flush();
        }

        public FenceInfo LoadFence(Guid fenceId)
        {
            var fence = AtomicFile.ReadWithRecovery(GetMetaFile(fenceId),
                json => JsonConvert.DeserializeObject<FenceInfo>(json, _jsonSettings));
            if (fence == null && File.Exists(GetMetaFile(fenceId)))
                _loggingService.LogError($"Failed to load fence {fenceId}: unreadable fence.json and no valid backup");
            return fence;
        }

        public List<FenceInfo> LoadAllFences()
        {
            var fences = new List<FenceInfo>();

            if (!Directory.Exists(_basePath))
                return fences;

            foreach (var dir in Directory.EnumerateDirectories(_basePath))
            {
                var metaFile = Path.Combine(dir, MetaFileName);
                if (!File.Exists(metaFile) && !File.Exists(metaFile + ".bak"))
                    continue;

                var fence = AtomicFile.ReadWithRecovery(metaFile,
                    json => JsonConvert.DeserializeObject<FenceInfo>(json, _jsonSettings));
                if (fence != null)
                    fences.Add(fence);
                else
                    _loggingService.LogWarning($"Failed to load fence from {metaFile}");
            }

            return fences;
        }

        public void DeleteFence(Guid fenceId)
        {
            var fencePath = GetFencePath(fenceId);
            // Avoid a pending write resurrecting the directory after deletion
            _writer.Discard(GetMetaFile(fenceId));
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

        public void Dispose()
        {
            _writer.Dispose();
        }

        public bool FenceExists(Guid fenceId)
        {
            return File.Exists(GetMetaFile(fenceId)) || _writer.HasPending(GetMetaFile(fenceId));
        }

        private string GetMetaFile(Guid fenceId)
        {
            return Path.Combine(GetFencePath(fenceId), MetaFileName);
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
