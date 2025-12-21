using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using NoFences.Model;
using NoFences.Services;

namespace NoFences.Migrations
{
    /// <summary>
    /// Migrates fence data from legacy XML format to new JSON format
    /// </summary>
    public class XmlToJsonMigrator
    {
        private const string OldMetaFileName = "__fence_metadata.xml";
        private readonly string _oldBasePath;
        private readonly ILoggingService _loggingService;

        public XmlToJsonMigrator(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _oldBasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NoFences"
            );
        }

        /// <summary>
        /// Checks if migration is needed
        /// </summary>
        public bool IsMigrationNeeded()
        {
            if (!Directory.Exists(_oldBasePath))
                return false;

            // Check if there are any XML metadata files
            foreach (var dir in Directory.EnumerateDirectories(_oldBasePath))
            {
                var metaFile = Path.Combine(dir, OldMetaFileName);
                if (File.Exists(metaFile))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Migrates all fences from XML to JSON format
        /// </summary>
        /// <returns>List of migrated fences</returns>
        public List<FenceInfo> MigrateAllFences()
        {
            var migratedFences = new List<FenceInfo>();

            if (!Directory.Exists(_oldBasePath))
            {
                _loggingService.LogInfo("No old data directory found, skipping migration");
                return migratedFences;
            }

            _loggingService.LogInfo("Starting XML to JSON migration...");

            foreach (var dir in Directory.EnumerateDirectories(_oldBasePath))
            {
                var metaFile = Path.Combine(dir, OldMetaFileName);
                if (File.Exists(metaFile))
                {
                    try
                    {
                        var fence = LoadXmlFence(metaFile);
                        if (fence != null)
                        {
                            migratedFences.Add(fence);
                            _loggingService.LogInfo($"Migrated fence: {fence.Name} (ID: {fence.Id})");
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError($"Failed to migrate fence from {dir}", ex);
                    }
                }
            }

            _loggingService.LogInfo($"Migration completed. Migrated {migratedFences.Count} fences");
            return migratedFences;
        }

        /// <summary>
        /// Loads a fence from XML file
        /// </summary>
        private FenceInfo LoadXmlFence(string xmlFilePath)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(FenceInfo));
                using (var reader = new StreamReader(xmlFilePath))
                {
                    return serializer.Deserialize(reader) as FenceInfo;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error loading XML fence from {xmlFilePath}", ex);
                return null;
            }
        }

        /// <summary>
        /// Creates a backup of the old XML data before migration
        /// </summary>
        public void BackupOldData()
        {
            if (!Directory.Exists(_oldBasePath))
                return;

            var backupPath = Path.Combine(_oldBasePath, "Backup_XML_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            
            _loggingService.LogInfo($"Creating backup at: {backupPath}");

            try
            {
                Directory.CreateDirectory(backupPath);

                foreach (var dir in Directory.EnumerateDirectories(_oldBasePath))
                {
                    var metaFile = Path.Combine(dir, OldMetaFileName);
                    if (File.Exists(metaFile))
                    {
                        var dirName = new DirectoryInfo(dir).Name;
                        var backupFile = Path.Combine(backupPath, $"{dirName}_{OldMetaFileName}");
                        File.Copy(metaFile, backupFile, true);
                    }
                }

                _loggingService.LogInfo("Backup completed successfully");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to create backup", ex);
            }
        }

        /// <summary>
        /// Cleans up old XML files after successful migration
        /// </summary>
        public void CleanupOldFiles()
        {
            if (!Directory.Exists(_oldBasePath))
                return;

            _loggingService.LogInfo("Cleaning up old XML files...");

            foreach (var dir in Directory.EnumerateDirectories(_oldBasePath))
            {
                var metaFile = Path.Combine(dir, OldMetaFileName);
                if (File.Exists(metaFile))
                {
                    try
                    {
                        File.Delete(metaFile);
                        _loggingService.LogDebug($"Deleted old XML file: {metaFile}");
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Failed to delete {metaFile}: {ex.Message}");
                    }
                }
            }

            _loggingService.LogInfo("Cleanup completed");
        }
    }
}
