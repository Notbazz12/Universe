using System;
using System.Drawing;
using System.Windows.Forms;
using NoFences.Model;

namespace NoFences.Services
{
    public interface ITrayIconManager
    {
        void Initialize();
        void Dispose();
    }

    public class TrayIconManager : ITrayIconManager, IDisposable
    {
        private readonly IFenceService _fenceService;
        private readonly ILoggingService _loggingService;
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;

        public TrayIconManager(IFenceService fenceService, ILoggingService loggingService)
        {
            _fenceService = fenceService ?? throw new ArgumentNullException(nameof(fenceService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public void Initialize()
        {
            _loggingService.LogInfo("Initializing Tray Icon...");

            _contextMenu = new ContextMenuStrip();
            
            // New Fence
            var newFenceItem = new ToolStripMenuItem(LocalizationManager.GetString("NewFence"));
            newFenceItem.Click += (s, e) => CreateNewFence();
            _contextMenu.Items.Add(newFenceItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Show/Hide Fences (Toggle)
            var toggleItem = new ToolStripMenuItem(LocalizationManager.GetString("HideFences"));
            toggleItem.Click += (s, e) => ToggleFences(toggleItem);
            _contextMenu.Items.Add(toggleItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Exit
            var exitItem = new ToolStripMenuItem(LocalizationManager.GetString("Exit"));
            exitItem.Click += (s, e) => ExitApplication();
            _contextMenu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                Text = "Universe",
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            _notifyIcon.DoubleClick += (s, e) => CreateNewFence(); // Double click creates a fence too
        }

        private void CreateNewFence()
        {
            _loggingService.LogInfo("Tray Icon: Creating new fence");
            _fenceService.CreateFence("New Fence");
        }

        private void ToggleFences(ToolStripMenuItem item)
        {
            // TODO: Implement global show/hide logic in FenceService
            // For now, just log
            _loggingService.LogInfo("Tray Icon: Toggle fences requested");
            // This requires FenceService to support hiding/showing all windows.
            // We'll leave this as a placeholder or implement basic loop if needed.
            // item.Text = visible ? LocalizationManager.GetString("HideFences") : LocalizationManager.GetString("ShowFences");
        }

        private void ExitApplication()
        {
            _loggingService.LogInfo("Tray Icon: Exiting application");
            _notifyIcon.Visible = false;
            Application.Exit();
        }

        public void Dispose()
        {
            _notifyIcon?.Dispose();
            _contextMenu?.Dispose();
        }
    }
}
