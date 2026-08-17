using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private CyberScrollPanel scrollHost;

        public GeneralPage()
        {
            config    = AppConfig.Load();
            BackColor = SettingsWindow.BgMain;
            ForeColor = SettingsWindow.TxtHigh;
            Font      = new Font("Segoe UI", 10f);
            Padding   = new Padding(0);

            scrollHost = new CyberScrollPanel
            {
                Dock      = DockStyle.Fill,
                BackColor = SettingsWindow.BgMain,
                Padding   = new Padding(32, 28, 28, 32)
            };
            Controls.Add(scrollHost);

            Build();
        }

        private void Build()
        {
            int y = 20;

            // ── Heading ─────────────────────────────────────────────────────────
            Add(new Label
            {
                Text      = "General Settings",
                Font      = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(32, y)
            });
            y += 34;

            Add(new Label
            {
                Text      = "Configure system behavior, workspace preferences, and automatic sorting.",
                Font      = new Font("Segoe UI", 9.5f),
                ForeColor = SettingsWindow.TxtMuted,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(32, y)
            });
            y += 40;

            // ── System Preferences Card ──────────────────────────────────────────
            var sysCard = CreateGlassCard("SYSTEM PREFERENCES", ref y, 280);
            int cy = 42;

            // Language selector
            sysCard.Controls.Add(CreateLabel("Interface Language", 20, cy + 3));
            var comboLang = new CyberComboBox
            {
                Location = new Point(180, cy),
                Width    = 180
            };
            comboLang.Items.AddRange(new object[] { "English", "Español" });
            comboLang.SelectedIndex = config.Language == "Spanish" ? 1 : 0;
            comboLang.SelectedIndexChanged += (s, e) =>
            {
                var lang = comboLang.SelectedIndex == 1 ? "Spanish" : "English";
                if (config.Language == lang) return;
                config.Language = lang;
                config.Save();
                LocalizationManager.CurrentLanguage = lang == "Spanish"
                    ? LocalizationManager.Language.Spanish : LocalizationManager.Language.English;
                Info(lang == "Spanish" ? "Reinicie Universe para aplicar el cambio." : "Restart Universe to apply the language change.");
            };
            sysCard.Controls.Add(comboLang);
            cy += 48;

            CreateToggleRow(sysCard, "Start Universe with Windows", IsStartupEnabled(), ref cy, on => SetStartup(on));
            CreateToggleRow(sysCard, "Check for updates automatically on startup", config.AutoCheckUpdates, ref cy, on => { config.AutoCheckUpdates = on; config.Save(); });
            CreateToggleRow(sysCard, "Laptop Mode (disables blur to maximize battery)", config.LaptopMode, ref cy,
                on => { config.LaptopMode = on; config.Save(); Info("Restart to apply Laptop Mode."); });
            CreateToggleRow(sysCard, "Smooth Hardware Animations", config.EnableAnimations, ref cy,
                on => { config.EnableAnimations = on; config.Save(); });

            // ── Workspace Features Card ──────────────────────────────────────────
            var featCard = CreateGlassCard("WORKSPACE FEATURES", ref y, 136);
            cy = 42;
            CreateToggleRow(featCard, "Smart Sorter (auto-organize files by rules and types)", config.EnableSmartSorter, ref cy,
                on => { config.EnableSmartSorter = on; config.Save(); Info("Smart Sorter " + (on ? "enabled" : "disabled") + ". Restart to apply."); });
            CreateToggleRow(featCard, "Desktop Notifications & File Activity Alerts", config.ShowNotifications, ref cy,
                on => { config.ShowNotifications = on; config.Save(); });

            // ── Workspace Management Card ────────────────────────────────────────
            var fenceCard = CreateGlassCard("WORKSPACE & FENCE MANAGEMENT", ref y, 140);
            cy = 42;

            var svc = NoFences.Core.DependencyInjection.GetRequiredService<IFenceService>();
            var lblCount = new Label
            {
                Text      = $"⚡  {svc.GetAllFences().Count} active fence workspace(s)",
                Location  = new Point(20, cy),
                AutoSize  = true,
                ForeColor = SettingsWindow.AccentNeon,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            fenceCard.Controls.Add(lblCount);
            cy += 34;

            var btnNew = CreateCyberButton("＋  New Fence", SettingsWindow.AccentNeon, new Point(20, cy), 130);
            btnNew.Click += (s, e) => { svc.CreateFence("New Fence"); lblCount.Text = $"⚡  {svc.GetAllFences().Count} active fence workspace(s)"; };
            fenceCard.Controls.Add(btnNew);

            var btnDel = CreateCyberButton("🗑  Delete All", SettingsWindow.AccentPink, new Point(160, cy), 130);
            btnDel.Click += (s, e) =>
            {
                var all = svc.GetAllFences();
                if (all.Count == 0) { Info("No fences to delete."); return; }
                if (MessageBox.Show($"Delete ALL {all.Count} fence(s)? This will remove the fence containers but keep your desktop files.",
                    "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                foreach (var f in all.ToArray()) svc.RemoveFence(f);
                lblCount.Text = "⚡  0 active fence workspace(s)";
            };
            fenceCard.Controls.Add(btnDel);

            // ── Application Maintenance Card ─────────────────────────────────────
            var appCard = CreateGlassCard("MAINTENANCE & UPDATES", ref y, 140);
            cy = 42;

            var btnUpd = CreateCyberButton("⟳  Check Updates", SettingsWindow.AccentVio, new Point(20, cy), 140);
            btnUpd.Click += (s, e) =>
                NoFences.Core.DependencyInjection.GetRequiredService<IUpdateService>().CheckForUpdates(false);
            appCard.Controls.Add(btnUpd);

            var btnData = CreateCyberButton("📁  Data Folder", Color.FromArgb(70, 85, 120), new Point(170, cy), 130);
            btnData.Click += (s, e) => Process.Start("explorer.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoFences"));
            appCard.Controls.Add(btnData);

            var btnRestart = CreateCyberButton("↺  Restart App", Color.FromArgb(70, 85, 120), new Point(310, cy), 130);
            btnRestart.Click += (s, e) => { Application.Restart(); Environment.Exit(0); };
            appCard.Controls.Add(btnRestart);

            scrollHost.UpdateLayout();
        }

        private Panel CreateGlassCard(string title, ref int y, int height)
        {
            var card = new Panel
            {
                Location  = new Point(32, y),
                Size      = new Size(580, height),
                BackColor = SettingsWindow.BgCard,
                Padding   = new Padding(20, 14, 20, 14)
            };

            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);

                // Rounded Glass background
                using (var bgBrush = new SolidBrush(SettingsWindow.BgCard))
                using (var path = CreateRoundRect(rect, 10))
                {
                    g.FillPath(bgBrush, path);
                }

                // Glass Border
                using (var pen = new Pen(SettingsWindow.BorderCard, 1f))
                using (var path = CreateRoundRect(rect, 10))
                {
                    g.DrawPath(pen, path);
                }

                // Top Specular Highlight
                using (var pen = new Pen(Color.FromArgb(25, 255, 255, 255), 1f))
                {
                    g.DrawLine(pen, 12, 1, card.Width - 12, 1);
                }

                // Title Tag
                using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(SettingsWindow.AccentNeon))
                {
                    g.DrawString(title, font, titleBrush, new PointF(20, 14));
                }
            };

            Add(card);
            y += height + 24;
            return card;
        }

        private void CreateToggleRow(Panel parent, string label, bool initial, ref int cy, Action<bool> onChange)
        {
            var toggle = new ToggleSwitch
            {
                Checked  = initial,
                Location = new Point(20, cy)
            };
            var lbl = new Label
            {
                Text      = label,
                Location  = new Point(76, cy + 3),
                AutoSize  = true,
                ForeColor = initial ? SettingsWindow.TxtHigh : SettingsWindow.TxtMid,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 9.5f)
            };
            toggle.CheckedChanged += (s, e) =>
            {
                lbl.ForeColor = toggle.Checked ? SettingsWindow.TxtHigh : SettingsWindow.TxtMid;
                onChange(toggle.Checked);
            };
            parent.Controls.Add(toggle);
            parent.Controls.Add(lbl);
            cy += 38;
        }

        private Label CreateLabel(string text, int x, int y) => new Label
        {
            Text      = text,
            Location  = new Point(x, y),
            AutoSize  = true,
            ForeColor = SettingsWindow.TxtMid,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 9.5f)
        };

        private Button CreateCyberButton(string text, Color accent, Point loc, int width)
        {
            var b = new Button
            {
                Text      = text,
                Location  = loc,
                Size      = new Size(width, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(28, 32, 50),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            b.FlatAppearance.BorderColor        = accent;
            b.FlatAppearance.BorderSize         = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(
                Math.Min(255, accent.R / 4 + 28),
                Math.Min(255, accent.G / 4 + 32),
                Math.Min(255, accent.B / 4 + 50));
            return b;
        }

        private void Add(Control c) => scrollHost.Controls.Add(c);

        private void Info(string msg) =>
            MessageBox.Show(msg, "Universe", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private bool IsStartupEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                    return key?.GetValue(AppName) != null;
            }
            catch { return false; }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (enable) key?.SetValue(AppName, Application.ExecutablePath);
                    else key?.DeleteValue(AppName, false);
                }
            }
            catch { }
        }

        private static GraphicsPath CreateRoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
