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
using static NoFences.Services.LocalizationManager;

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
                Text      = T("General Settings", "Ajustes generales"),
                Font      = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize  = true,
                UseMnemonic = false,
                Location  = new Point(32, y)
            });
            y += 34;

            Add(new Label
            {
                Text      = T("Configure system behavior, workspace preferences, and automatic sorting.", "Configura el comportamiento del sistema, el espacio de trabajo y el orden automático."),
                Font      = new Font("Segoe UI", 9.5f),
                ForeColor = SettingsWindow.TxtMuted,
                BackColor = Color.Transparent,
                AutoSize  = true,
                UseMnemonic = false,
                Location  = new Point(32, y)
            });
            y += 40;

            // ── System Preferences Card ──────────────────────────────────────────
            var sysCard = CreateGlassCard(T("SYSTEM PREFERENCES", "PREFERENCIAS DEL SISTEMA"), ref y, 262);
            int cy = 42;

            // Language selector
            sysCard.Controls.Add(CreateLabel(T("Interface Language", "Idioma de la interfaz"), 20, cy + 3));
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
                // Redraw the settings window in the new language right away; fence and tray
                // menus are built at startup and pick it up after a restart.
                BeginInvoke(new Action(() => (FindForm() as SettingsWindow)?.Rebuild()));
            };
            sysCard.Controls.Add(comboLang);
            cy += 48;

            CreateToggleRow(sysCard, T("Start Universe with Windows", "Iniciar Universe con Windows"), IsStartupEnabled(), ref cy, on => SetStartup(on));
            CreateToggleRow(sysCard, T("Check for updates automatically on startup", "Buscar actualizaciones automáticamente al iniciar"), config.AutoCheckUpdates, ref cy, on => 
            { 
                config.AutoCheckUpdates = on; 
                config.Save(); 
                var updateSvc = NoFences.Core.DependencyInjection.GetService<IUpdateService>();
                if (on) updateSvc?.StartBackgroundCheck();
                else updateSvc?.StopBackgroundCheck();
            });
            CreateToggleRow(sysCard, T("Laptop Mode (disables blur to maximize battery)", "Modo portátil (desactiva el desenfoque para ahorrar batería)"), config.LaptopMode, ref cy,
                on => { config.LaptopMode = on; config.Save(); Info(T("Restart to apply Laptop Mode.", "Reinicia Universe para aplicar el modo portátil.")); });
            CreateToggleRow(sysCard, T("Smooth Hardware Animations", "Animaciones suaves"), config.EnableAnimations, ref cy,
                on => { config.EnableAnimations = on; config.Save(); });

            // ── Workspace Features Card ──────────────────────────────────────────
            var featCard = CreateGlassCard(T("WORKSPACE FEATURES", "FUNCIONES DEL ESPACIO DE TRABAJO"), ref y, 136);
            cy = 42;
            CreateToggleRow(featCard, T("Smart Sorter (auto-organize files by rules and types)", "Smart Sorter (organiza archivos automáticamente por reglas y tipos)"), config.EnableSmartSorter, ref cy,
                on => { config.EnableSmartSorter = on; config.Save(); Info(on ? T("Smart Sorter enabled. Restart to apply.", "Smart Sorter activado. Reinicia para aplicarlo.") : T("Smart Sorter disabled. Restart to apply.", "Smart Sorter desactivado. Reinicia para aplicarlo.")); });
            CreateToggleRow(featCard, T("Desktop Notifications & File Activity Alerts", "Notificaciones de escritorio y avisos de actividad"), config.ShowNotifications, ref cy,
                on => { config.ShowNotifications = on; config.Save(); });

            // ── Workspace Management Card ────────────────────────────────────────
            var fenceCard = CreateGlassCard(T("WORKSPACE & FENCE MANAGEMENT", "GESTIÓN DE FENCES"), ref y, 214);
            cy = 42;

            var svc = NoFences.Core.DependencyInjection.GetRequiredService<IFenceService>();
            var lblCount = new Label
            {
                Text      = T($"⚡  {svc.GetAllFences().Count} active fence(s)", $"⚡  {svc.GetAllFences().Count} fence(s) activa(s)"),
                Location  = new Point(20, cy),
                AutoSize  = true,
                UseMnemonic = false,
                ForeColor = SettingsWindow.AccentNeon,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            fenceCard.Controls.Add(lblCount);
            cy += 32;

            var btnNew = CreateCyberButton(T("＋  New Fence", "＋  Nueva fence"), SettingsWindow.AccentNeon, new Point(20, cy), 120);
            btnNew.Click += (s, e) => { svc.CreateFence("New Fence"); lblCount.Text = T($"⚡  {svc.GetAllFences().Count} active fence(s)", $"⚡  {svc.GetAllFences().Count} fence(s) activa(s)"); };
            fenceCard.Controls.Add(btnNew);

            var btnNewImg = CreateCyberButton(T("Images Fence", "Fence de imágenes"), SettingsWindow.AccentVio, new Point(150, cy), 130);
            btnNewImg.Click += (s, e) => { svc.CreateImageFence(); lblCount.Text = T($"⚡  {svc.GetAllFences().Count} active fence(s)", $"⚡  {svc.GetAllFences().Count} fence(s) activa(s)"); };
            fenceCard.Controls.Add(btnNewImg);

            var btnNewTask = CreateCyberButton(T("Tasks Fence", "Fence de tareas"), Color.FromArgb(70, 200, 255), new Point(290, cy), 130);
            btnNewTask.Click += (s, e) => { svc.CreateTaskFence(); lblCount.Text = T($"⚡  {svc.GetAllFences().Count} active fence(s)", $"⚡  {svc.GetAllFences().Count} fence(s) activa(s)"); };
            fenceCard.Controls.Add(btnNewTask);

            var btnDel = CreateCyberButton(T("Delete All", "Borrar todas"), SettingsWindow.AccentPink, new Point(430, cy), 120);
            btnDel.Click += (s, e) =>
            {
                var all = svc.GetAllFences();
                if (all.Count == 0) { Info(T("No fences to delete.", "No hay fences para borrar.")); return; }
                if (MessageBox.Show(T($"Delete ALL {all.Count} fence(s)? This will remove the fence containers but keep your desktop files.", $"¿Borrar TODAS las fences ({all.Count})? Se quitan los contenedores, pero tus archivos del escritorio se conservan."),
                    T("Confirm Reset", "Confirmar"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                foreach (var f in all.ToArray()) svc.RemoveFence(f);
                lblCount.Text = T("⚡  0 active fences", "⚡  0 fences activas");
            };
            fenceCard.Controls.Add(btnDel);
            FlowRow(btnNew, btnNewImg, btnNewTask);

            cy += 42;

            var btnGames = CreateCyberButton(T("Organize Games", "Ordenar juegos"), SettingsWindow.AccentVio, new Point(20, cy), 160);
            btnGames.Click += (s, e) =>
            {
                var sorter = NoFences.Core.DependencyInjection.GetService<ISmartSorterService>();
                if (sorter != null)
                {
                    int count = sorter.SortDesktopGames();
                    lblCount.Text = T($"⚡  {svc.GetAllFences().Count} active fence(s)", $"⚡  {svc.GetAllFences().Count} fence(s) activa(s)");
                    Info(count > 0 
                        ? T($"Organized {count} games into your Games fence.", $"Se ordenaron {count} juegos en la fence de juegos.") 
                        : T("No new desktop games detected.", "No se detectaron juegos nuevos en el escritorio."));
                }
            };
            fenceCard.Controls.Add(btnGames);

            var btnProgs = CreateCyberButton(T("Organize Programs", "Ordenar programas"), SettingsWindow.AccentNeon, new Point(190, cy), 170);
            btnProgs.Click += (s, e) =>
            {
                var sorter = NoFences.Core.DependencyInjection.GetService<ISmartSorterService>();
                if (sorter != null)
                {
                    int count = sorter.SortDesktopPrograms();
                    lblCount.Text = T($"⚡  {svc.GetAllFences().Count} active fence(s)", $"⚡  {svc.GetAllFences().Count} fence(s) activa(s)");
                    Info(count > 0 
                        ? T($"Organized {count} programs into your Programs fence.", $"Se ordenaron {count} programas en la fence de programas.") 
                        : T("No new desktop programs detected.", "No se detectaron programas nuevos en el escritorio."));
                }
            };
            fenceCard.Controls.Add(btnProgs);

            var btnAll = CreateCyberButton(T("Organize All", "Ordenar todo"), Color.FromArgb(160, 140, 255), new Point(370, cy), 140);
            btnAll.Click += (s, e) =>
            {
                var sorter = NoFences.Core.DependencyInjection.GetService<ISmartSorterService>();
                if (sorter != null)
                {
                    var (gCount, pCount) = sorter.SortAllDesktop();
                    lblCount.Text = T($"⚡  {svc.GetAllFences().Count} active fence(s)", $"⚡  {svc.GetAllFences().Count} fence(s) activa(s)");
                    Info(T($"Desktop organized.\n• Games: {gCount}\n• Programs: {pCount}", $"Escritorio ordenado.\n• Juegos: {gCount}\n• Programas: {pCount}"));
                }
            };
            fenceCard.Controls.Add(btnAll);
            FlowRow(btnGames, btnProgs, btnAll);

            cy += 42;

            var btnImgs = CreateCyberButton(T("Organize Images", "Ordenar imágenes"), SettingsWindow.AccentVio, new Point(20, cy), 160);
            btnImgs.Click += (s, e) =>
            {
                var sorter = NoFences.Core.DependencyInjection.GetService<ISmartSorterService>();
                if (sorter != null)
                {
                    int count = sorter.SortDesktopImages();
                    lblCount.Text = T($"⚡  {svc.GetAllFences().Count} active fence(s)", $"⚡  {svc.GetAllFences().Count} fence(s) activa(s)");
                    Info(count > 0 
                        ? T($"Organized {count} images into your Images fence.", $"Se ordenaron {count} imágenes en la fence de imágenes.") 
                        : T("No new desktop images detected.", "No se detectaron imágenes nuevas en el escritorio."));
                }
            };
            fenceCard.Controls.Add(btnImgs);

            var btnTasks = CreateCyberButton(T("Organize Tasks", "Ordenar tareas"), Color.FromArgb(70, 200, 255), new Point(190, cy), 170);
            btnTasks.Click += (s, e) =>
            {
                var sorter = NoFences.Core.DependencyInjection.GetService<ISmartSorterService>();
                if (sorter != null)
                {
                    int count = sorter.SortDesktopTasks();
                    lblCount.Text = T($"⚡  {svc.GetAllFences().Count} active fence(s)", $"⚡  {svc.GetAllFences().Count} fence(s) activa(s)");
                    Info(count > 0 
                        ? T($"Organized {count} tasks/notes into your Tasks fence.", $"Se ordenaron {count} tareas/notas en la fence de tareas.") 
                        : T("No new desktop tasks detected.", "No se detectaron tareas nuevas en el escritorio."));
                }
            };
            fenceCard.Controls.Add(btnTasks);
            btnDel.Top = btnImgs.Top; // destructive action sits apart, at the end of the last row
            FlowRow(btnImgs, btnTasks, btnDel);

            // ── Application Maintenance Card ─────────────────────────────────────
            var appCard = CreateGlassCard(T("MAINTENANCE & UPDATES", "MANTENIMIENTO Y ACTUALIZACIONES"), ref y, 100);
            cy = 42;

            var btnUpd = CreateCyberButton(T("Check Updates", "Buscar actualizaciones"), SettingsWindow.AccentVio, new Point(20, cy), 140);
            btnUpd.Click += (s, e) =>
                NoFences.Core.DependencyInjection.GetRequiredService<IUpdateService>().CheckForUpdates(false);
            appCard.Controls.Add(btnUpd);

            var btnData = CreateCyberButton(T("Data Folder", "Carpeta de datos"), Color.FromArgb(70, 85, 120), new Point(170, cy), 130);
            btnData.Click += (s, e) => Process.Start("explorer.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoFences"));
            appCard.Controls.Add(btnData);

            var btnRestart = CreateCyberButton(T("Restart App", "Reiniciar app"), Color.FromArgb(70, 85, 120), new Point(310, cy), 130);
            btnRestart.Click += (s, e) => { Application.Restart(); Environment.Exit(0); };
            appCard.Controls.Add(btnRestart);
            FlowRow(btnUpd, btnData, btnRestart);

            scrollHost.UpdateLayout();
        }

        /// <summary>Places buttons left to right with even gaps after they sized to their text.</summary>
        private static void FlowRow(params Control[] row)
        {
            for (int i = 1; i < row.Length; i++)
                row[i].Left = row[i - 1].Right + 10;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            scrollHost.RecalculateLayout();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            scrollHost.RecalculateLayout();
        }

        private Panel CreateGlassCard(string title, ref int y, int height)
        {
            var card = new Panel
            {
                Location  = new Point(32, y),
                Size      = new Size(Math.Max(500, Width - 80), height),
                Anchor    = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
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
                UseMnemonic = false,
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
            UseMnemonic = false,
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
            b.Width = Math.Max(b.Width, TextRenderer.MeasureText(text, b.Font).Width + 32); // fit translated text
            b.FlatAppearance.BorderColor        = accent;
            b.FlatAppearance.BorderSize         = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(
                Math.Min(255, accent.R / 4 + 28),
                Math.Min(255, accent.G / 4 + 32),
                Math.Min(255, accent.B / 4 + 50));
            return b;
        }

        private void Add(Control c) => scrollHost.AddHostControl(c);

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
