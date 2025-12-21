using System;
using System.Windows.Forms;
using NoFences.Services;

namespace NoFences.UI
{
    public class HiddenMainForm : Form
    {
        private readonly ITrayIconManager _trayIconManager;
        private readonly ILoggingService _loggingService;
        private readonly ISmartSorterService _smartSorterService;

        public HiddenMainForm(ITrayIconManager trayIconManager, ILoggingService loggingService, ISmartSorterService smartSorterService)
        {
            _trayIconManager = trayIconManager;
            _loggingService = loggingService;
            _smartSorterService = smartSorterService;

            // Configure hidden form
            this.Text = "Universe";
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Opacity = 0;
            this.Size = new System.Drawing.Size(0, 0);

            // Initialize Tray Icon
            _trayIconManager.Initialize();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.Visible = false;
            _loggingService.LogInfo("HiddenMainForm loaded");
            _smartSorterService.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _loggingService.LogInfo($"HiddenMainForm closing. Reason: {e.CloseReason}");
            _smartSorterService.Stop();
            _trayIconManager.Dispose();
            base.OnFormClosing(e);
        }
    }
}
