using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using NoFences.Model;

namespace NoFences.Services
{
    public interface IUpdateService
    {
        void CheckForUpdates(bool silent);
        void StartBackgroundCheck();
        void StopBackgroundCheck();
    }

    public class UpdateService : IUpdateService
    {
        private const string DefaultVersionUrl = "https://raw.githubusercontent.com/Notbazz12/Universe/main/version.json";
        public const string CurrentVersion = "2.0.6";
        private readonly ILoggingService _loggingService;

        // Captured on construction (UI thread); used to marshal MessageBox calls
        // back to the UI thread when CheckForUpdates runs via Task.Run.
        private readonly SynchronizationContext _uiContext;
        private System.Threading.Timer _bgTimer;

        public UpdateService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            // SynchronizationContext.Current is non-null on the UI thread (WinForms installs
            // WindowsFormsSynchronizationContext before any service is resolved).
            _uiContext = SynchronizationContext.Current;
        }

        public void StartBackgroundCheck()
        {
            var config = AppConfig.Load();
            if (!config.AutoCheckUpdates) return;

            StopBackgroundCheck();

            // Wait 10 seconds after startup so app launch is 100% instant and silent.
            // Then check periodically every 4 hours.
            _bgTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    CheckForUpdates(silent: true);
                }
                catch (Exception ex)
                {
                    _loggingService.LogWarning($"Background update check failed: {ex.Message}");
                }
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromHours(4));

            _loggingService.LogInfo("Background update check timer started (initial in 10s, then every 4h)");
        }

        public void StopBackgroundCheck()
        {
            if (_bgTimer != null)
            {
                _bgTimer.Dispose();
                _bgTimer = null;
                _loggingService.LogInfo("Background update check timer stopped");
            }
        }

        public void CheckForUpdates(bool silent)
        {
            Task.Run(() => CheckForUpdatesInternal(silent));
        }

        private void CheckForUpdatesInternal(bool silent)
        {
            var config = AppConfig.Load();
            if (silent && !config.AutoCheckUpdates)
            {
                return; // User has auto-update check disabled
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
                    var releaseNotes = updateInfo["releaseNotes"]?.ToString();

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
                            PromptUpdate(remoteVersion, downloadUrl, expectedHash, releaseNotes);
                        }
                        else
                        {
                            // Background automatic check: notify user!
                            var tray = Core.DependencyInjection.GetService<ITrayIconManager>();
                            if (tray != null)
                            {
                                tray.SetUpdateAvailable(remoteVersion, () =>
                                {
                                    PromptUpdate(remoteVersion, downloadUrl, expectedHash, releaseNotes);
                                });

                                if (config.ShowNotifications)
                                {
                                    bool isSpanish = config.Language == "Spanish";
                                    string title = isSpanish 
                                        ? "🚀 Universe - ¡Nueva Actualización!" 
                                        : "🚀 Universe - Update Available!";
                                    string msg = isSpanish
                                        ? $"¡La versión v{remoteVersion} está lista!\nHaz clic aquí para actualizar automáticamente."
                                        : $"Universe v{remoteVersion} is now available!\nClick here to download and install.";

                                    tray.ShowNotification(title, msg, ToolTipIcon.Info, () =>
                                    {
                                        PromptUpdate(remoteVersion, downloadUrl, expectedHash, releaseNotes);
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        _loggingService.LogInfo("Universe is up to date.");
                        if (!silent)
                        {
                            bool isSpanish = config.Language == "Spanish";
                            string msg = isSpanish ? "Universe está actualizado a la última versión." : "Universe is up to date.";
                            string title = isSpanish ? "Buscar Actualizaciones" : "Check for Updates";
                            RunOnUiThread(() =>
                                MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Information));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to check for updates", ex);
                if (!silent)
                {
                    bool isSpanish = config.Language == "Spanish";
                    string msg = isSpanish 
                        ? "Error al buscar actualizaciones. Por favor inténtalo más tarde." 
                        : "Failed to check for updates. Please try again later.";
                    string title = isSpanish ? "Error de Actualización" : "Update Error";
                    RunOnUiThread(() =>
                        MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Error));
                }
            }
        }

        private void PromptUpdate(string remoteVersion, string downloadUrl, string expectedHash, string releaseNotes)
        {
            RunOnUiThread(() =>
            {
                var config = AppConfig.Load();
                bool isSpanish = config.Language == "Spanish";
                string title = isSpanish ? "Actualización Disponible - Universe" : "Update Available - Universe";
                string notes = string.IsNullOrWhiteSpace(releaseNotes)
                    ? (isSpanish ? "Mejoras de rendimiento y correcciones de errores." : "Performance improvements and bug fixes.")
                    : releaseNotes;

                string promptText = isSpanish
                    ? $"¡Una nueva versión de Universe está disponible! (v{remoteVersion})\n\nNotas de la versión:\n{notes}\n\n¿Deseas descargar e instalar la actualización ahora?"
                    : $"A new version of Universe is available ({remoteVersion}).\n\nRelease Notes:\n{notes}\n\nDo you want to download and install it now?";

                var result = MessageBox.Show(
                    promptText,
                    title,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information
                );

                if (result == DialogResult.Yes)
                {
                    DownloadAndInstall(downloadUrl, expectedHash, remoteVersion);
                }
            });
        }

        private bool IsNewerVersion(string current, string remote)
        {
            var v1 = new Version(current);
            var v2 = new Version(remote);
            return v2 > v1;
        }

        private void DownloadAndInstall(string url, string expectedSha256, string newVersion)
        {
            Task.Run(() =>
            {
                try
                {
                    var config = AppConfig.Load();
                    bool isSpanish = config.Language == "Spanish";
                    var tray = Core.DependencyInjection.GetService<ITrayIconManager>();
                    tray?.ShowNotification("Universe",
                        isSpanish ? $"Descargando actualización v{newVersion}..." : $"Downloading update v{newVersion}...",
                        ToolTipIcon.Info);

                    var tempPath = Path.Combine(Path.GetTempPath(), "Universe_Setup.exe");

                    using (var client = new WebClient())
                    {
                        client.DownloadFile(url, tempPath);
                    }

                    // SECURITY FIX: Verify SHA-256 hash before executing the downloaded binary.
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
                                    isSpanish 
                                        ? "El instalador descargado no superó la verificación de seguridad y no se ejecutará.\n\nPor favor descarga la actualización manualmente desde el repositorio oficial."
                                        : "The downloaded installer failed integrity verification and will not be run.\n\nPlease download the update manually from the official repository.",
                                    isSpanish ? "Advertencia de Seguridad – Actualización Cancelada" : "Security Warning – Update Aborted",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning));

                            return;
                        }
                    }
                    else
                    {
                        _loggingService.LogWarning("Update manifest does not contain a SHA-256 hash. Skipping integrity check.");
                    }

                    _loggingService.LogInfo("Installer downloaded. Starting installation...");

                    try
                    {
                        var fenceService = Core.DependencyInjection.GetRequiredService<IFenceService>();
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
                        MessageBox.Show(
                            "Failed to download the update.", 
                            "Update Error",
                            MessageBoxButtons.OK, 
                            MessageBoxIcon.Error));
                }
            });
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
