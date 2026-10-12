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
        void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, Action onClick = null);
        void SetUpdateAvailable(string newVersion, Action onInstall);
    }

    public class TrayIconManager : ITrayIconManager, IDisposable
    {
        private readonly IFenceService _fenceService;
        private readonly ILoggingService _loggingService;
        private readonly System.Threading.SynchronizationContext _uiContext = System.Threading.SynchronizationContext.Current;
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private Action _notificationClickAction;
        private ToolStripMenuItem _updateMenuItem;

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

            // Organize Games
            var sortGamesItem = new ToolStripMenuItem("🎮 Organize Games into Fence");
            sortGamesItem.Click += (s, e) =>
            {
                var sorter = Core.DependencyInjection.GetService<ISmartSorterService>();
                if (sorter != null)
                {
                    int count = sorter.SortDesktopGames();
                    string msg = count > 0 
                        ? $"Organized {count} games into your Games Fence!" 
                        : "No new desktop games detected.";
                    _notifyIcon.ShowBalloonTip(3000, "Universe", msg, ToolTipIcon.Info);
                }
            };
            _contextMenu.Items.Add(sortGamesItem);

            // Organize Programs
            var sortProgsItem = new ToolStripMenuItem("⚡ Organize Programs into Fence");
            sortProgsItem.Click += (s, e) =>
            {
                var sorter = Core.DependencyInjection.GetService<ISmartSorterService>();
                if (sorter != null)
                {
                    int count = sorter.SortDesktopPrograms();
                    string msg = count > 0 
                        ? $"Organized {count} programs into your Programs Fence!" 
                        : "No new desktop programs detected.";
                    _notifyIcon.ShowBalloonTip(3000, "Universe", msg, ToolTipIcon.Info);
                }
            };
            _contextMenu.Items.Add(sortProgsItem);

            // Desktop Mascot Companion
            var mascotToggleItem = new ToolStripMenuItem("🐾 Cosmic Mascot Companion (Desktop Pet)");
            mascotToggleItem.Click += (s, e) =>
            {
                DesktopMascotWindow.ToggleVisibility();
                mascotToggleItem.Checked = DesktopMascotWindow.Instance != null && DesktopMascotWindow.Instance.Visible;
            };
            mascotToggleItem.Checked = AppConfig.Load().ShowDesktopMascot;
            _contextMenu.Items.Add(mascotToggleItem);

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
            _notifyIcon.BalloonTipClicked += (s, e) =>
            {
                var act = _notificationClickAction;
                _notificationClickAction = null;
                act?.Invoke();
            };
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

        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, Action onClick = null)
        {
            if (_notifyIcon == null) return;
            if (_uiContext != null && System.Threading.SynchronizationContext.Current != _uiContext)
            {
                _uiContext.Post(_ => ShowNotificationInternal(title, message, icon, onClick), null);
            }
            else
            {
                ShowNotificationInternal(title, message, icon, onClick);
            }
        }

        private void ShowNotificationInternal(string title, string message, ToolTipIcon icon, Action onClick)
        {
            if (_notifyIcon == null) return;
            _notificationClickAction = onClick;
            _notifyIcon.ShowBalloonTip(6000, title, message, icon);
        }

        public void SetUpdateAvailable(string newVersion, Action onInstall)
        {
            if (_uiContext != null && System.Threading.SynchronizationContext.Current != _uiContext)
            {
                _uiContext.Post(_ => SetUpdateAvailableInternal(newVersion, onInstall), null);
            }
            else
            {
                SetUpdateAvailableInternal(newVersion, onInstall);
            }
        }

        private void SetUpdateAvailableInternal(string newVersion, Action onInstall)
        {
            if (_contextMenu == null) return;
            var config = AppConfig.Load();
            bool isSpanish = config.Language == "Spanish";
            string text = isSpanish 
                ? $"⭐ Nueva versión v{newVersion} disponible (Instalar)" 
                : $"⭐ Update v{newVersion} Available (Install)";

            if (_updateMenuItem == null)
            {
                _updateMenuItem = new ToolStripMenuItem(text)
                {
                    Font = new Font(_contextMenu.Font, FontStyle.Bold),
                    ForeColor = Color.FromArgb(0, 220, 255)
                };
                _updateMenuItem.Click += (s, e) => onInstall?.Invoke();
                _contextMenu.Items.Insert(0, _updateMenuItem);
                _contextMenu.Items.Insert(1, new ToolStripSeparator());
            }
            else
            {
                _updateMenuItem.Text = text;
                _updateMenuItem.Visible = true;
            }
        }

        public void Dispose()
        {
            _updateMenuItem?.Dispose();
            _updateMenuItem = null;
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
