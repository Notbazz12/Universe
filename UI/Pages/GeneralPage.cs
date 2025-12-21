using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;
using NoFences.Model;
using NoFences.Services;

namespace NoFences.UI.Pages
{
    public class GeneralPage : UserControl
    {
        private const string RunKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string AppName = "Universe";
        private AppConfig config;

        public GeneralPage()
        {
            config = AppConfig.Load();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.BackColor = Color.White;
            this.AutoScroll = true; // Enable scrolling for more content
            
            var title = new Label { Text = LocalizationManager.GetString("General"), Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true, Location = new Point(0, 0) };
            var subtitle = new Label { Text = LocalizationManager.GetString("GeneralSettings"), Font = new Font("Segoe UI", 10), AutoSize = true, Location = new Point(2, 40), ForeColor = Color.Gray };

            Controls.Add(title);
            Controls.Add(subtitle);

            int y = 80;

            // === System Settings Section ===
            var systemGroup = new GroupBox { Text = "System Settings", Location = new Point(0, y), Size = new Size(500, 200), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            int groupY = 30;

            // Language
            systemGroup.Controls.Add(new Label { Text = LocalizationManager.GetString("Language"), Location = new Point(20, groupY), AutoSize = true, Font = new Font("Segoe UI", 10) });
            var comboLang = new ComboBox { Location = new Point(180, groupY - 3), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            comboLang.Items.AddRange(new object[] { "English", "Español" });
            comboLang.SelectedIndex = config.Language == "Spanish" ? 1 : 0;
            comboLang.SelectedIndexChanged += (s, e) => 
            {
                var newLang = comboLang.SelectedIndex == 1 ? "Spanish" : "English";
                if (config.Language != newLang)
                {
                    config.Language = newLang;
                    config.Save();
                    LocalizationManager.CurrentLanguage = newLang == "Spanish" ? LocalizationManager.Language.Spanish : LocalizationManager.Language.English;
                    MessageBox.Show(newLang == "Spanish" ? "Reinicie la aplicación para aplicar los cambios." : "Please restart the application to apply changes.");
                }
            };
            systemGroup.Controls.Add(comboLang);
            groupY += 40;

            // Startup
            var chkStartup = new CheckBox { Text = LocalizationManager.GetString("StartWithWindows"), Location = new Point(20, groupY), AutoSize = true, Font = new Font("Segoe UI", 10) };
            chkStartup.Checked = IsStartupEnabled();
            chkStartup.CheckedChanged += (s, e) => SetStartup(chkStartup.Checked);
            systemGroup.Controls.Add(chkStartup);
            groupY += 35;

            // Laptop Mode
            var chkLaptop = new CheckBox { Text = LocalizationManager.GetString("LaptopMode") + " (Ahorro Batería)", Location = new Point(20, groupY), AutoSize = true, Font = new Font("Segoe UI", 10) };
            chkLaptop.Checked = config.LaptopMode;
            chkLaptop.CheckedChanged += (s, e) => 
            {
                config.LaptopMode = chkLaptop.Checked;
                config.Save();
                MessageBox.Show(LocalizationManager.CurrentLanguage == LocalizationManager.Language.Spanish ? "Reinicie para aplicar cambios." : "Restart to apply changes.");
            };
            systemGroup.Controls.Add(chkLaptop);
            groupY += 35;

            // Enable Animations
            var chkAnimate = new CheckBox { Text = "Enable Animations", Location = new Point(20, groupY), AutoSize = true, Font = new Font("Segoe UI", 10), Checked = config.EnableAnimations };
            chkAnimate.CheckedChanged += (s, e) => { config.EnableAnimations = chkAnimate.Checked; config.Save(); };
            systemGroup.Controls.Add(chkAnimate);

            Controls.Add(systemGroup);
            y += 210;

            // === Features Section ===
            var featuresGroup = new GroupBox { Text = "Features", Location = new Point(0, y), Size = new Size(500, 120), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            groupY = 30;

            // Smart Sorter
            var chkSorter = new CheckBox { Text = "Enable Smart Sorter (Auto-organize files)", Location = new Point(20, groupY), AutoSize = true, Font = new Font("Segoe UI", 10), Checked = config.EnableSmartSorter };
            chkSorter.CheckedChanged += (s, e) => 
            {
                config.EnableSmartSorter = chkSorter.Checked;
                config.Save();
                MessageBox.Show("Smart Sorter " + (chkSorter.Checked ? "enabled" : "disabled") + ". Restart to apply.");
            };
            featuresGroup.Controls.Add(chkSorter);
            groupY += 35;

            // Notifications
            var chkNotify = new CheckBox { Text = "Show Notifications", Location = new Point(20, groupY), AutoSize = true, Font = new Font("Segoe UI", 10), Checked = config.ShowNotifications };
            chkNotify.CheckedChanged += (s, e) => { config.ShowNotifications = chkNotify.Checked; config.Save(); };
            featuresGroup.Controls.Add(chkNotify);
            groupY += 35;

            Controls.Add(featuresGroup);
            y += 130;

            // === Fence Management Section ===
            var fenceGroup = new GroupBox { Text = "Fence Management", Location = new Point(0, y), Size = new Size(500, 140), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            groupY = 30;

            // Fence Count Display
            var fenceService = NoFences.Core.DependencyInjection.GetRequiredService<NoFences.Model.IFenceService>();
            var fenceCount = fenceService.GetAllFences().Count;
            var lblFenceCount = new Label { Text = $"Active Fences: {fenceCount}", Location = new Point(20, groupY), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Italic), ForeColor = Color.DarkBlue };
            fenceGroup.Controls.Add(lblFenceCount);
            groupY += 30;

            // New Fence Button
            var btnCreate = new Button { Text = LocalizationManager.GetString("NewFence"), Location = new Point(20, groupY), Size = new Size(150, 35), FlatStyle = FlatStyle.System };
            btnCreate.Click += (s, e) => 
            {
                fenceService.CreateFence("New Fence");
                lblFenceCount.Text = $"Active Fences: {fenceService.GetAllFences().Count}";
            };
            fenceGroup.Controls.Add(btnCreate);

            // Delete All Fences Button
            var btnDeleteAll = new Button { Text = "Delete All Fences", Location = new Point(180, groupY), Size = new Size(150, 35), FlatStyle = FlatStyle.System, ForeColor = Color.DarkRed };
            btnDeleteAll.Click += (s, e) =>
            {
                var allFences = fenceService.GetAllFences();
                if (allFences.Count == 0)
                {
                    MessageBox.Show("No fences to delete.", "Delete All", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to delete ALL {allFences.Count} fences?\\n\\nThis action cannot be undone!",
                    "Delete All Fences",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    foreach (var fence in allFences.ToArray())
                    {
                        fenceService.RemoveFence(fence);
                    }
                    lblFenceCount.Text = $"Active Fences: 0";
                    MessageBox.Show("All fences deleted successfully.", "Delete All", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            fenceGroup.Controls.Add(btnDeleteAll);

            Controls.Add(fenceGroup);
            y += 150;

            // === Application Section ===
            var appGroup = new GroupBox { Text = "Application", Location = new Point(0, y), Size = new Size(500, 140), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            groupY = 30;

            // Check Updates Button
            var btnUpdate = new Button { Text = "Check for Updates", Location = new Point(20, groupY), Size = new Size(150, 35), FlatStyle = FlatStyle.System };
            btnUpdate.Click += (s, e) =>
            {
                var updateService = NoFences.Core.DependencyInjection.GetRequiredService<IUpdateService>();
                updateService.CheckForUpdates(false);
            };
            appGroup.Controls.Add(btnUpdate);

            // Data Folder
            var btnData = new Button { Text = LocalizationManager.GetString("OpenDataFolder"), Location = new Point(180, groupY), Size = new Size(150, 35), FlatStyle = FlatStyle.System };
            btnData.Click += (s, e) => Process.Start("explorer.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoFences"));
            appGroup.Controls.Add(btnData);
            groupY += 45;

            // Restart
            var btnRestart = new Button { Text = LocalizationManager.GetString("RestartApp"), Location = new Point(20, groupY), Size = new Size(150, 35), FlatStyle = FlatStyle.System };
            btnRestart.Click += (s, e) => { Application.Restart(); Environment.Exit(0); };
            appGroup.Controls.Add(btnRestart);

            Controls.Add(appGroup);
        }

        private bool IsStartupEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                return key?.GetValue(AppName) != null;
            }
        }

        private void SetStartup(bool enable)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (enable)
                {
                    key.SetValue(AppName, Application.ExecutablePath);
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
        }
    }
}
