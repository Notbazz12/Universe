using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using NoFences.Model;

namespace NoFences.Services
{
    public interface IUpdateService
    {
        void CheckForUpdates(bool silent);
    }

    public class UpdateService : IUpdateService
    {
        private const string DefaultVersionUrl = "https://raw.githubusercontent.com/Notbazz12/Universe/main/version.json";
        private const string CurrentVersion = "2.0.0";
        private readonly ILoggingService _loggingService;

        // Captured on construction (UI thread); used to marshal MessageBox calls
        // back to the UI thread when CheckForUpdates runs via Task.Run.
        private readonly SynchronizationContext _uiContext;

        public UpdateService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            // SynchronizationContext.Current is non-null on the UI thread (WinForms installs
            // WindowsFormsSynchronizationContext before any service is resolved).
            _uiContext = SynchronizationContext.Current;
        }

        public void CheckForUpdates(bool silent)
        {
            var config = AppConfig.Load();
            if (silent && !config.AutoCheckUpdates)
            {
                return; // User has auto-update check on startup disabled
            }

            _loggingService.LogInfo("Checking for updates...");
            try
            {
                // GitHub/Render requires TLS 1.2+
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                var versionUrl = !string.IsNullOrWhiteSpace(config.UpdateUrl) ? config.UpdateUrl : DefaultVersionUrl;

                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "Universe-App/" + CurrentVersion;
                    var json = client.DownloadString(versionUrl);
                    var updateInfo = JObject.Parse(json);
                    var remoteVersion = updateInfo["version"]?.ToString();
                    var downloadUrl = updateInfo["downloadUrl"]?.ToString();
                    var expectedHash = updateInfo["sha256"]?.ToString();

                    if (string.IsNullOrEmpty(remoteVersion) || string.IsNullOrEmpty(downloadUrl))
                    {
                        _loggingService.LogWarning("Update manifest is missing required fields.");
                        return;
                    }

                    if (IsNewerVersion(CurrentVersion, remoteVersion))
                    {
                        _loggingService.LogInfo($"New version found: {remoteVersion}");

                        if (!silent)
                        {
                            // Manual check (user clicked button): show confirmation dialog
                            RunOnUiThread(() =>
                            {
                                var result = MessageBox.Show(
                                    $"A new version of Universe is available ({remoteVersion}).\n\nDo you want to download and install it now?",
                                    "Update Available",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Information
                                );

                                if (result == DialogResult.Yes)
                                {
                                    DownloadAndInstall(downloadUrl, expectedHash);
                                }
                            });
                        }
                    }
                    else
                    {
                        _loggingService.LogInfo("Universe is up to date.");
                        if (!silent)
                        {
                            RunOnUiThread(() =>
                                MessageBox.Show("Universe is up to date.", "Check for Updates",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to check for updates", ex);
                if (!silent)
                {
                    RunOnUiThread(() =>
                        MessageBox.Show("Failed to check for updates. Please try again later.",
                            "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
                }
            }
        }

        private bool IsNewerVersion(string current, string remote)
        {
            var v1 = new Version(current);
            var v2 = new Version(remote);
            return v2 > v1;
        }

        private void DownloadAndInstall(string url, string expectedSha256)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), "Universe_Setup.exe");

                using (var client = new WebClient())
                {
                    client.DownloadFile(url, tempPath);
                }

                // SECURITY FIX: Verify SHA-256 hash before executing the downloaded binary.
                // If the manifest includes a hash and it does not match, abort and alert.
                if (!string.IsNullOrEmpty(expectedSha256))
                {
                    var actualHash = ComputeSha256(tempPath);
                    if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        _loggingService.LogError(
                            $"Update hash mismatch! Expected: {expectedSha256} Got: {actualHash}");

                        TryDeleteTempFile(tempPath);

                        RunOnUiThread(() =>
                            MessageBox.Show(
                                "The downloaded installer failed integrity verification and will not be run.\n\n" +
                                "Please download the update manually from the official repository.",
                                "Security Warning – Update Aborted",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning));

                        return;
                    }
                }
                else
                {
                    // No hash in manifest: warn the user but allow them to proceed
                    _loggingService.LogWarning("Update manifest does not contain a SHA-256 hash. Skipping integrity check.");
                }

                _loggingService.LogInfo("Installer downloaded. Starting installation...");

                try
                {
                    var fenceService = NoFences.Core.DependencyInjection.GetRequiredService<IFenceService>();
                    fenceService?.CloseAllFences();
                }
                catch { }

                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    Arguments = "/SILENT /SUPPRESSMSGBOXES",
                    UseShellExecute = true
                });

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to download/install update", ex);
                RunOnUiThread(() =>
                    MessageBox.Show("Failed to download the update.", "Update Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error));
            }
        }

        /// <summary>
        /// Computes the SHA-256 hash of a file and returns it as a lowercase hex string.
        /// </summary>
        private static string ComputeSha256(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void TryDeleteTempFile(string path)
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }

        /// <summary>
        /// Posts an action to the UI thread via the captured SynchronizationContext.
        /// Falls back to a direct call when running on the UI thread already
        /// (e.g. during manual "Check for Updates" from a menu item).
        /// </summary>
        private void RunOnUiThread(Action action)
        {
            if (_uiContext != null && SynchronizationContext.Current != _uiContext)
            {
                _uiContext.Post(_ => action(), null);
            }
            else
            {
                action();
            }
        }
    }
}
