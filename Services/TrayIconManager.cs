using System;
using System.Drawing;
using System.Windows.Forms;
using NoFences.Model;
using NoFences.UI;

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
            _contextMenu.Renderer = new FenceMenuRenderer();
            
            // New Fence
            var newFenceItem = new ToolStripMenuItem(LocalizationManager.GetString("NewFence"));
            newFenceItem.Click += (s, e) => CreateNewFence();
            _contextMenu.Items.Add(newFenceItem);

            // Show/Hide Fences (Toggle)
            var toggleItem = new ToolStripMenuItem(LocalizationManager.GetString("HideFences"));
            toggleItem.Click += (s, e) => ToggleFences(toggleItem);
            _contextMenu.Items.Add(toggleItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Settings
            var settingsItem = new ToolStripMenuItem(LocalizationManager.GetString("ConfigureFences") ?? "Settings...");
            settingsItem.Click += (s, e) => OpenSettings();
            _contextMenu.Items.Add(settingsItem);

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

            _notifyIcon.DoubleClick += (s, e) => OpenSettings();
        }

        private void CreateNewFence()
        {
            _loggingService.LogInfo("Tray Icon: Creating new fence");
            _fenceService.CreateFence("New Fence");
        }

        private void ToggleFences(ToolStripMenuItem item)
        {
            bool current = _fenceService.AreFencesVisible;
            bool next = !current;
            _fenceService.SetAllFencesVisible(next);
            item.Text = next ? LocalizationManager.GetString("HideFences") : LocalizationManager.GetString("ShowFences");
            _loggingService.LogInfo($"Tray Icon: Toggle fences set to {next}");
        }

        private void OpenSettings()
        {
            var allFences = _fenceService.GetAllFences();
            var targetFence = allFences.Count > 0 ? allFences[0] : new FenceInfo(Guid.NewGuid()) { Name = "General" };
            var settings = new SettingsWindow(targetFence, () =>
            {
                foreach (var fence in _fenceService.GetAllFences())
                {
                    _fenceService.ReloadFence(fence.Id);
                }
            });
            settings.Show();
            settings.BringToFront();
            settings.Activate();
        }

        private void ExitApplication()
        {
            _loggingService.LogInfo("Tray Icon: Exiting application completely");
            Dispose();
            try
            {
                _fenceService.CloseAllFences();
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Error closing fences during exit: {ex.Message}");
            }
            Application.Exit();
            Environment.Exit(0);
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            _contextMenu?.Dispose();
        }
    }
}
