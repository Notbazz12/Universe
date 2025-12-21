using System;
using System.Net;
using System.Diagnostics;
using System.IO;
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
        private const string VersionUrl = "https://raw.githubusercontent.com/Notbanzz/NoFences/main/version.json";
        private const string CurrentVersion = "2.0.0";
        private readonly ILoggingService _loggingService;

        public UpdateService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public void CheckForUpdates(bool silent)
        {
            _loggingService.LogInfo("Checking for updates...");
            try
            {
                // GitHub requires TLS 1.2
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                using (var client = new WebClient())
                {
                    var json = client.DownloadString(VersionUrl);
                    var updateInfo = JObject.Parse(json);
                    var remoteVersion = updateInfo["version"].ToString();
                    var downloadUrl = updateInfo["downloadUrl"].ToString();

                    if (IsNewerVersion(CurrentVersion, remoteVersion))
                    {
                        _loggingService.LogInfo($"New version found: {remoteVersion}");
                        var result = MessageBox.Show(
                            $"A new version of Universe is available ({remoteVersion}).\n\nDo you want to download and install it now?",
                            "Update Available",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information
                        );

                        if (result == DialogResult.Yes)
                        {
                            DownloadAndInstall(downloadUrl);
                        }
                    }
                    else
                    {
                        _loggingService.LogInfo("Universe is up to date.");
                        if (!silent)
                        {
                            MessageBox.Show("Universe is up to date.", "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to check for updates", ex);
                if (!silent)
                {
                    MessageBox.Show("Failed to check for updates. Please try again later.", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private bool IsNewerVersion(string current, string remote)
        {
            var v1 = new Version(current);
            var v2 = new Version(remote);
            return v2 > v1;
        }

        private void DownloadAndInstall(string url)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), "Universe_Setup.exe");
                using (var client = new WebClient())
                {
                    client.DownloadFile(url, tempPath);
                }

                _loggingService.LogInfo("Installer downloaded. Starting installation...");
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    Arguments = "/SILENT", // Inno Setup silent flag
                    UseShellExecute = true
                });

                Application.Exit();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to download/install update", ex);
                MessageBox.Show("Failed to download the update.", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
