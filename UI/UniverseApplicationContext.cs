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

        public UniverseApplicationContext(
            ITrayIconManager trayIconManager,
            ILoggingService loggingService,
            ISmartSorterService smartSorterService)
        {
            _trayIconManager = trayIconManager ?? throw new ArgumentNullException(nameof(trayIconManager));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _smartSorterService = smartSorterService ?? throw new ArgumentNullException(nameof(smartSorterService));

            // Initialize Tray Icon
            _trayIconManager.Initialize();

            var config = AppConfig.Load();
            if (config.EnableSmartSorter)
            {
                _smartSorterService.Start();
            }

            _loggingService.LogInfo("Universe ApplicationContext initialized and active");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _loggingService.LogInfo("Universe ApplicationContext disposing...");
                _smartSorterService.Stop();
                _trayIconManager.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
