using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NoFences.Model;
using NoFences.Services;
using NoFences.Win32;

namespace NoFences.UI
{
    public partial class SettingsWindow : Form
    {
        // ── Cyber-Glass Obsidian Theme Tokens ────────────────────────────────────
        public static readonly Color BgMain     = Color.FromArgb(14, 16, 23);
        public static readonly Color BgSide     = Color.FromArgb(18, 20, 31);
        public static readonly Color BgCard     = Color.FromArgb(23, 26, 40);
        public static readonly Color BgCardHover= Color.FromArgb(28, 32, 50);
        public static readonly Color BorderCard = Color.FromArgb(40, 47, 72);
        public static readonly Color AccentNeon = Color.FromArgb(0, 245, 212);
        public static readonly Color AccentVio  = Color.FromArgb(139, 92, 246);
        public static readonly Color AccentPink = Color.FromArgb(244, 63, 94);
        public static readonly Color TxtHigh    = Color.FromArgb(248, 250, 252);
        public static readonly Color TxtMid     = Color.FromArgb(203, 213, 225);
        public static readonly Color TxtMuted   = Color.FromArgb(130, 143, 168);

        // ── State ────────────────────────────────────────────────────────────────
        private readonly FenceInfo currentFence;
        private readonly Action onSettingsChanged;

        private Panel sidebar;
        private Panel contentHost;
        private NavItem activeNav;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public SettingsWindow(FenceInfo fence, Action onChanged)
        {
            currentFence      = fence;
            onSettingsChanged = onChanged;
            InitializeComponent();
            Build();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int darkMode = 1;
                if (DwmSetWindowAttribute(Handle, 20, ref darkMode, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(Handle, 19, ref darkMode, sizeof(int));
                }
            }
            catch { }
        }

        private void Build()
        {
            Text            = "Universe · Settings";
            Size            = new Size(880, 620);
            MinimumSize     = new Size(780, 520);
            BackColor       = BgMain;
            ForeColor       = TxtHigh;
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = false;
            Font            = new Font("Segoe UI", 10f);
            
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // ── Sidebar ──────────────────────────────────────────────────────────
            sidebar = new Panel
            {
                Dock      = DockStyle.Left,
                Width     = 220,
                BackColor = BgSide,
                Padding   = new Padding(12, 16, 12, 16)
            };
            sidebar.Paint += SidebarPaint;

            // Nav items
            int navY = 96;
            var navGen = MakeNav("⚡  General",         ref navY);
            var navPer = MakeNav("🎨  Personalization", ref navY);
            var navAbt = MakeNav("ℹ️  About",           ref navY);

            navGen.Click += (s, e) => { Activate(navGen); LoadGeneral(); };
            navPer.Click += (s, e) => { Activate(navPer); LoadPersonalization(); };
            navAbt.Click += (s, e) => { Activate(navAbt); LoadAbout(); };

            sidebar.Controls.Add(navGen);
            sidebar.Controls.Add(navPer);
            sidebar.Controls.Add(navAbt);

            // Interactive Update Button at bottom of sidebar
            var btnUpdateSidebar = new Button
            {
                Dock      = DockStyle.Bottom,
                Height    = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(24, 28, 44),
                ForeColor = AccentNeon,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Text      = "⟳  Check Updates",
                Cursor    = Cursors.Hand
            };
            btnUpdateSidebar.FlatAppearance.BorderColor        = Color.FromArgb(48, 56, 85);
            btnUpdateSidebar.FlatAppearance.BorderSize         = 1;
            btnUpdateSidebar.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, 42, 68);
            btnUpdateSidebar.Click += (s, e) =>
            {
                btnUpdateSidebar.Text = "Checking...";
                NoFences.Core.DependencyInjection.GetRequiredService<IUpdateService>().CheckForUpdates(false);
                btnUpdateSidebar.Text = "⟳  Check Updates";
            };
            sidebar.Controls.Add(btnUpdateSidebar);

            // ── Content Host ─────────────────────────────────────────────────────
            contentHost = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = BgMain,
                Padding   = new Padding(0)
            };

            Controls.Add(contentHost);
            Controls.Add(sidebar);

            Activate(navGen);
            LoadGeneral();
        }

        private void SidebarPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Brand Header Area
            using (var titleFont = new Font("Segoe UI", 14f, FontStyle.Bold))
            using (var brush = new LinearGradientBrush(new Rectangle(18, 22, 160, 30), AccentNeon, AccentVio, 0f))
            {
                g.DrawString("✦ UNIVERSE", titleFont, brush, 18, 22);
            }

            using (var subFont = new Font("Segoe UI", 8f))
            using (var b = new SolidBrush(TxtMuted))
            {
                g.DrawString("Desktop Workspace Engine", subFont, b, 20, 50);
            }

            // Subtle Glass Divider
            using (var pen = new Pen(Color.FromArgb(35, 255, 255, 255), 1))
            {
                g.DrawLine(pen, 16, 78, sidebar.Width - 16, 78);
            }

            // Right border separation
            using (var pen = new Pen(Color.FromArgb(30, 40, 60), 1))
            {
                g.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
            }
        }

        private NavItem MakeNav(string text, ref int y)
        {
            var item = new NavItem(text)
            {
                Location = new Point(12, y),
                Size     = new Size(196, 44)
            };
            y += 50;
            return item;
        }

        private void Activate(NavItem nav)
        {
            if (activeNav != null) { activeNav.IsActive = false; activeNav.Invalidate(); }
            activeNav = nav;
            nav.IsActive = true;
            nav.Invalidate();
        }

        private void LoadGeneral()
        {
            contentHost.Controls.Clear();
            var page = new Pages.GeneralPage { Dock = DockStyle.Fill };
            contentHost.Controls.Add(page);
        }

        private void LoadPersonalization()
        {
            contentHost.Controls.Clear();
            var page = new Pages.PersonalizationPage(currentFence, onSettingsChanged) { Dock = DockStyle.Fill };
            contentHost.Controls.Add(page);
        }

        private void LoadAbout()
        {
            contentHost.Controls.Clear();
            var page = new Pages.AboutPage { Dock = DockStyle.Fill };
            contentHost.Controls.Add(page);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Name = "SettingsWindow";
            ResumeLayout(false);
        }
    }

    // ── Modern Floating Nav Capsule ──────────────────────────────────────────────
    internal class NavItem : Control
    {
        public bool IsActive;
        private bool _hover;
        private readonly string _text;

        public NavItem(string text)
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint  |
                     ControlStyles.UserPaint, true);
            _text     = text;
            BackColor = Color.Transparent;
            Cursor    = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true;  Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            if (IsActive)
            {
                // Active Frosted Glow Pill
                using (var bgBrush = new SolidBrush(Color.FromArgb(32, 37, 58)))
                using (var path = CreateRoundRect(rect, 8))
                {
                    g.FillPath(bgBrush, path);
                }

                // Left Neon Glow Indicator
                var indRect = new Rectangle(3, 8, 4, Height - 16);
                using (var indBrush = new LinearGradientBrush(indRect, SettingsWindow.AccentNeon, SettingsWindow.AccentVio, 90f))
                using (var path = CreateRoundRect(indRect, 2))
                {
                    g.FillPath(indBrush, path);
                }

                // Border Highlight
                using (var pen = new Pen(Color.FromArgb(70, SettingsWindow.AccentVio), 1f))
                using (var path = CreateRoundRect(rect, 8))
                {
                    g.DrawPath(pen, path);
                }
            }
            else if (_hover)
            {
                // Subtle Hover Capsule
                using (var bgBrush = new SolidBrush(Color.FromArgb(24, 28, 44)))
                using (var path = CreateRoundRect(rect, 8))
                {
                    g.FillPath(bgBrush, path);
                }
            }

            Color textColor = IsActive ? Color.White : (_hover ? SettingsWindow.TxtMid : SettingsWindow.TxtMuted);

            using (var f = new Font("Segoe UI", 9.5f, IsActive ? FontStyle.Bold : FontStyle.Regular))
            using (var b = new SolidBrush(textColor))
            {
                var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                g.DrawString(_text, f, b, new RectangleF(16, 0, Width - 20, Height), sf);
            }
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
