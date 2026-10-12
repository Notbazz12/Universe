using System;
using System.Windows.Forms;
using NoFences.Model;
using NoFences.Services;

namespace NoFences.UI
{
    /// <summary>
    /// Application context for running Universe in the background with tray icon and fences.
    /// Replaces HiddenMainForm to prevent premature application exit on Windows Forms.
    /// </summary>
    public class UniverseApplicationContext : ApplicationContext
    {
        private readonly ITrayIconManager _trayIconManager;
        private readonly ILoggingService _loggingService;
        private readonly ISmartSorterService _smartSorterService;
        private readonly IUpdateService _updateService;

        public UniverseApplicationContext(
            ITrayIconManager trayIconManager,
            ILoggingService loggingService,
            ISmartSorterService smartSorterService,
            IUpdateService updateService)
        {
            _trayIconManager = trayIconManager ?? throw new ArgumentNullException(nameof(trayIconManager));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _smartSorterService = smartSorterService ?? throw new ArgumentNullException(nameof(smartSorterService));
            _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));

            // Initialize Tray Icon
            _trayIconManager.Initialize();

            var config = AppConfig.Load();
            if (config.EnableSmartSorter)
            {
                _smartSorterService.Start();
            }

            // Start background update checker
            _updateService.StartBackgroundCheck();

            // Start Cosmic Desktop Mascot Companion
            if (config.ShowDesktopMascot)
            {
                try
                {
                    var mascotWin = new DesktopMascotWindow();
                    mascotWin.Show();
                    _loggingService.LogInfo("Cosmic Desktop Mascot companion activated");
                }
                catch (Exception ex)
                {
                    _loggingService.LogWarning($"Failed to start Desktop Mascot: {ex.Message}");
                }
            }

            _loggingService.LogInfo("Universe ApplicationContext initialized and active");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _loggingService.LogInfo("Universe ApplicationContext disposing...");
                DesktopMascotWindow.Instance?.Dispose();
                _updateService?.StopBackgroundCheck();
                _smartSorterService.Stop();
                _trayIconManager.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
