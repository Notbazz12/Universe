using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NoFences.Services;

namespace NoFences.UI.Pages
{
    public class AboutPage : UserControl
    {
        private CyberScrollPanel scrollHost;

        public AboutPage()
        {
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
                Text      = "About Universe",
                Font      = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(32, y)
            });
            y += 34;

            Add(new Label
            {
                Text      = "High-performance, GPU-accelerated desktop organization engine for Windows.",
                Font      = new Font("Segoe UI", 9.5f),
                ForeColor = SettingsWindow.TxtMuted,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(32, y)
            });
            y += 40;

            // ── App Info Card ───────────────────────────────────────────────────
            var appCard = CreateGlassCard("APPLICATION INFORMATION", ref y, 160);
            int cy = 42;

            appCard.Controls.Add(new Label
            {
                Text      = "✦  Universe Desktop Organizer",
                Location  = new Point(20, cy),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = SettingsWindow.AccentNeon,
                BackColor = Color.Transparent
            });
            cy += 32;

            appCard.Controls.Add(new Label
            {
                Text      = $"Version {UpdateService.CurrentVersion} (Cyber-Glass & Iridescent Bubble Edition)",
                Location  = new Point(20, cy),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9.5f),
                ForeColor = SettingsWindow.TxtHigh,
                BackColor = Color.Transparent
            });
            cy += 28;

            appCard.Controls.Add(new Label
            {
                Text      = "Created by Notbanzz & Open Source Community",
                Location  = new Point(20, cy),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9f),
                ForeColor = SettingsWindow.TxtMuted,
                BackColor = Color.Transparent
            });

            // ── Links & Updates Card ────────────────────────────────────────────
            var linkCard = CreateGlassCard("COMMUNITY & UPDATES", ref y, 140);
            cy = 42;

            var linkGithub = new LinkLabel
            {
                Text             = "🌐  Visit GitHub Repository (Notbazz12/Universe)",
                Location         = new Point(20, cy),
                AutoSize         = true,
                Font             = new Font("Segoe UI", 10f, FontStyle.Bold),
                LinkColor        = SettingsWindow.AccentNeon,
                ActiveLinkColor  = SettingsWindow.AccentVio,
                VisitedLinkColor = SettingsWindow.AccentNeon,
                BackColor        = Color.Transparent,
                Cursor           = Cursors.Hand
            };
            linkGithub.LinkClicked += (s, e) => Process.Start("https://github.com/Notbazz12/Universe");
            linkCard.Controls.Add(linkGithub);
            cy += 36;

            var btnCheck = CreateCyberButton("⟳  Check for Updates", SettingsWindow.AccentVio, new Point(20, cy), 180);
            btnCheck.Click += (s, e) =>
                NoFences.Core.DependencyInjection.GetRequiredService<IUpdateService>().CheckForUpdates(false);
            linkCard.Controls.Add(btnCheck);

            // ── Architecture & Credits ──────────────────────────────────────────
            var techCard = CreateGlassCard("SYSTEM & FRAMEWORK ARCHITECTURE", ref y, 140);
            cy = 42;

            techCard.Controls.Add(new Label
            {
                Text      = "Runtime: .NET Framework 4.8  ·  Engine: Optimized GDI+ Anti-Aliased Pipeline\nRendering: Dual Buffering + DWM Mica/Acrylic Window Hook Integration\nLicense: Open Source (MIT / GPL Compatible)",
                Location  = new Point(20, cy),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9f),
                ForeColor = SettingsWindow.TxtMid,
                BackColor = Color.Transparent
            });

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

                using (var bgBrush = new SolidBrush(SettingsWindow.BgCard))
                using (var path = CreateRoundRect(rect, 10))
                {
                    g.FillPath(bgBrush, path);
                }

                using (var pen = new Pen(SettingsWindow.BorderCard, 1f))
                using (var path = CreateRoundRect(rect, 10))
                {
                    g.DrawPath(pen, path);
                }

                using (var pen = new Pen(Color.FromArgb(25, 255, 255, 255), 1f))
                {
                    g.DrawLine(pen, 12, 1, card.Width - 12, 1);
                }

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

        private void Add(Control c) => scrollHost.AddHostControl(c);

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
