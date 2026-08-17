using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NoFences.Model;
using NoFences.Util;
using NoFences.Services;

namespace NoFences.UI.Pages
{
    public class PersonalizationPage : UserControl
    {
        private FenceInfo fenceInfo;
        private Action onChanged;

        private TrackBar hueTrack, satTrack, briTrack, alphaTrack;
        private Panel previewPanel;
        private bool isUpdating;
        private CyberScrollPanel scrollHost;

        public PersonalizationPage(FenceInfo fence, Action onChanged)
        {
            fenceInfo      = fence;
            this.onChanged = onChanged;
            BackColor      = SettingsWindow.BgMain;
            ForeColor      = SettingsWindow.TxtHigh;
            Font           = new Font("Segoe UI", 10f);
            Padding        = new Padding(0);

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
                Text      = "Personalization",
                Font      = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(32, y)
            });
            y += 34;

            Add(new Label
            {
                Text      = "Fine-tune the Cyber-Glass aesthetic, iridescent glowing borders, and visual effects.",
                Font      = new Font("Segoe UI", 9.5f),
                ForeColor = SettingsWindow.TxtMuted,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(32, y)
            });
            y += 38;

            // ── Live preview ─────────────────────────────────────────────────────
            previewPanel = new Panel
            {
                Location  = new Point(32, y),
                Size      = new Size(580, 76),
                BackColor = Color.Transparent
            };
            previewPanel.Paint += PreviewPaint;
            Add(previewPanel);
            y += 94;

            // ── Preset Themes ────────────────────────────────────────────────────
            var themeCard = CreateGlassCard("THEME PRESETS", ref y, 92);
            themeCard.Controls.Add(CreateLabel("Select Theme", 20, 48));
            var themeCombo = new CyberComboBox
            {
                Location = new Point(140, 44),
                Width    = 280
            };
            themeCombo.Items.AddRange(new object[] { "Cyber-Glass & Iridescent", "Dark Obsidian", "Glass Frost", "Light Classic", "Minimal Pure" });
            themeCombo.SelectedIndex = fenceInfo.Theme == "DarkObsidian" ? 1 :
                                       fenceInfo.Theme == "Classic" ? 3 : 0;

            themeCombo.SelectedIndexChanged += (s, e) =>
            {
                switch (themeCombo.SelectedIndex)
                {
                    case 0: // Cyber-Glass & Iridescent
                        fenceInfo.Theme = "CyberGlass";
                        fenceInfo.CornerRadius = 12;
                        fenceInfo.EnableIridescentBorder = true;
                        fenceInfo.ShowItemCountBadge = true;
                        fenceInfo.BackgroundColor = Color.FromArgb(200, 13, 16, 24).ToArgb();
                        fenceInfo.TitleTextColor = Color.White.ToArgb();
                        break;
                    case 1: // Dark Obsidian
                        fenceInfo.Theme = "DarkObsidian";
                        fenceInfo.CornerRadius = 8;
                        fenceInfo.EnableIridescentBorder = false;
                        fenceInfo.ShowItemCountBadge = true;
                        fenceInfo.BackgroundColor = Color.FromArgb(230, 20, 22, 30).ToArgb();
                        fenceInfo.TitleTextColor = Color.White.ToArgb();
                        break;
                    case 2: // Glass Frost
                        fenceInfo.Theme = "CyberGlass";
                        fenceInfo.CornerRadius = 6;
                        fenceInfo.EnableIridescentBorder = false;
                        fenceInfo.ShowItemCountBadge = true;
                        fenceInfo.BackgroundColor = Color.FromArgb(120, 18, 20, 32).ToArgb();
                        break;
                    case 3: // Light Classic
                        ThemeInfo.Light.ApplyTo(fenceInfo);
                        fenceInfo.Theme = "Classic";
                        break;
                    case 4: // Minimal Pure
                        ThemeInfo.Minimal.ApplyTo(fenceInfo);
                        fenceInfo.Theme = "Classic";
                        fenceInfo.ShowHeader = false;
                        break;
                }
                LoadValues();
                onChanged?.Invoke();
                RefreshPreview();
            };
            themeCard.Controls.Add(themeCombo);

            // ── Cyber-Glass Effects ──────────────────────────────────────────────
            var fxCard = CreateGlassCard("CYBER-GLASS & VISUAL EFFECTS", ref y, 150);
            int cy = 42;
            CreateToggleRow(fxCard, "Iridescent Aura Border (luminous neon gradient glow)", ref cy, fenceInfo.EnableIridescentBorder,
                on => { fenceInfo.EnableIridescentBorder = on; onChanged?.Invoke(); RefreshPreview(); });
            CreateToggleRow(fxCard, "Item Count Pill Badge  [ N items ]", ref cy, fenceInfo.ShowItemCountBadge,
                on => { fenceInfo.ShowItemCountBadge = on; onChanged?.Invoke(); RefreshPreview(); });
            CreateToggleRow(fxCard, "Breathing Pulse Animation (on file drop/addition)", ref cy, fenceInfo.EnableBreathingEffect,
                on => { fenceInfo.EnableBreathingEffect = on; onChanged?.Invoke(); });

            // ── Custom Color Tuning ──────────────────────────────────────────────
            var colorCard = CreateGlassCard("CUSTOM COLOR & TRANSPARENCY TUNING", ref y, 226);
            cy = 40;
            hueTrack   = CreateSlider(colorCard, "Hue",          0, 360, ref cy);
            satTrack   = CreateSlider(colorCard, "Saturation %", 0, 100, ref cy);
            briTrack   = CreateSlider(colorCard, "Brightness %", 0, 100, ref cy);
            alphaTrack = CreateSlider(colorCard, "Transparency", 0, 255, ref cy);
            LoadValues();

            // ── Header & Layout ───────────────────────────────────────────────────
            var optCard = CreateGlassCard("HEADER & BEHAVIOR", ref y, 180);
            cy = 42;
            CreateToggleRow(optCard, "Show Header Title Bar", ref cy, fenceInfo.ShowHeader,
                on => { fenceInfo.ShowHeader = on; onChanged?.Invoke(); RefreshPreview(); });
            CreateToggleRow(optCard, "Chameleon Mode (fade out when mouse is away)", ref cy, fenceInfo.ChameleonMode,
                on => { fenceInfo.ChameleonMode = on; onChanged?.Invoke(); });
            CreateToggleRow(optCard, "Magic Color (infer accent from contained file types)", ref cy, fenceInfo.EnableMagicColor,
                on => { fenceInfo.EnableMagicColor = on; onChanged?.Invoke(); });

            optCard.Controls.Add(CreateLabel("Title Align", 20, cy + 4));
            var alignCombo = new CyberComboBox
            {
                Location = new Point(140, cy),
                Width    = 160
            };
            alignCombo.Items.AddRange(new object[] { "Left", "Center", "Right" });
            alignCombo.SelectedIndex = fenceInfo.TitleAlignment;
            alignCombo.SelectedIndexChanged += (s, e) => { fenceInfo.TitleAlignment = alignCombo.SelectedIndex; onChanged?.Invoke(); RefreshPreview(); };
            optCard.Controls.Add(alignCombo);

            // ── Fonts & Icons ─────────────────────────────────────────────────────
            var fontCard = CreateGlassCard("TYPOGRAPHY & ICONS", ref y, 126);
            cy = 42;

            var btnTF = CreateCyberButton("Title Font…", SettingsWindow.AccentNeon, new Point(20, cy), 130);
            btnTF.Click += (s, e) => ChangeFont(true);
            fontCard.Controls.Add(btnTF);

            var btnIF = CreateCyberButton("Item Font…", SettingsWindow.AccentVio, new Point(160, cy), 130);
            btnIF.Click += (s, e) => ChangeFont(false);
            fontCard.Controls.Add(btnIF);

            fontCard.Controls.Add(CreateLabel("Icon Size", 310, cy + 5));
            var iconCombo = new CyberComboBox
            {
                Location = new Point(390, cy),
                Width    = 160
            };
            iconCombo.Items.AddRange(new object[] { "32 px (Normal)", "48 px (Large)", "64 px (Extra)" });
            iconCombo.SelectedIndex = fenceInfo.IconSize == 64 ? 2 : fenceInfo.IconSize == 48 ? 1 : 0;
            iconCombo.SelectedIndexChanged += (s, e) =>
            {
                fenceInfo.IconSize = iconCombo.SelectedIndex == 2 ? 64 : iconCombo.SelectedIndex == 1 ? 48 : 32;
                onChanged?.Invoke();
            };
            fontCard.Controls.Add(iconCombo);

            scrollHost.UpdateLayout();
        }

        private void PreviewPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, previewPanel.Width - 1, previewPanel.Height - 1);

            if (fenceInfo.Theme == "CyberGlass" || string.IsNullOrEmpty(fenceInfo.Theme))
            {
                CyberGlassRenderer.RenderCyberGlassContainer(g, rect, true, false, fenceInfo.CornerRadius > 0 ? fenceInfo.CornerRadius : 10);

                using (var f = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                using (var b = new SolidBrush(Color.White))
                {
                    g.DrawString($"  {fenceInfo.Name}  ·  Cyber-Glass Iridescent Preview", f, b, new PointF(12, 16));
                }

                if (fenceInfo.ShowItemCountBadge)
                {
                    var countFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
                    CyberGlassRenderer.RenderPillBadge(g, new Rectangle(previewPanel.Width - 85, 14, 65, 20), $"{fenceInfo.Files.Count} items", countFont, CyberGlassRenderer.IridescentCyan);
                }

                // Sample Mini Floating Bubble
                var bubbleRect = new Rectangle(18, 44, 130, 22);
                CyberGlassRenderer.RenderBubbleCard(g, bubbleRect, true, false, 6);
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var bf = new Font("Segoe UI", 8f, FontStyle.Bold))
                using (var bb = new SolidBrush(Color.White))
                {
                    g.DrawString("✦ Bubble Card", bf, bb, bubbleRect, sf);
                }
            }
            else
            {
                using (var b = new SolidBrush(Color.FromArgb(fenceInfo.BackgroundColor)))
                    g.FillRectangle(b, rect);

                using (var f = new Font("Segoe UI", 10f, FontStyle.Bold))
                using (var b = new SolidBrush(Color.FromArgb(fenceInfo.TitleTextColor)))
                    g.DrawString($"  {fenceInfo.Name}  ·  {fenceInfo.Files.Count} items  — preview", f, b, new PointF(8, 22));

                using (var pen = new Pen(SettingsWindow.BorderCard))
                    g.DrawRectangle(pen, rect);
            }
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

        private TrackBar CreateSlider(Panel parent, string label, int min, int max, ref int cy)
        {
            parent.Controls.Add(new Label
            {
                Text      = label,
                Location  = new Point(20, cy + 6),
                Size      = new Size(110, 22),
                ForeColor = SettingsWindow.TxtMid,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 9f)
            });
            var tb = new TrackBar
            {
                Location  = new Point(140, cy),
                Size      = new Size(400, 36),
                Minimum   = min,
                Maximum   = max,
                TickStyle = TickStyle.None,
                BackColor = SettingsWindow.BgCard
            };
            tb.ValueChanged += (s, e) => { UpdateColor(); RefreshPreview(); };
            parent.Controls.Add(tb);
            cy += 42;
            return tb;
        }

        private void CreateToggleRow(Panel parent, string label, ref int cy, bool init, Action<bool> onChange)
        {
            var sw = new ToggleSwitch { Checked = init, Location = new Point(20, cy) };
            var lb = new Label
            {
                Text      = label,
                Location  = new Point(76, cy + 3),
                AutoSize  = true,
                ForeColor = init ? SettingsWindow.TxtHigh : SettingsWindow.TxtMid,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 9.5f)
            };
            sw.CheckedChanged += (s, e) =>
            {
                lb.ForeColor = sw.Checked ? SettingsWindow.TxtHigh : SettingsWindow.TxtMid;
                onChange(sw.Checked);
            };
            parent.Controls.Add(sw);
            parent.Controls.Add(lb);
            cy += 38;
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

        private Label CreateLabel(string text, int x, int y) => new Label
        {
            Text      = text,
            Location  = new Point(x, y),
            AutoSize  = true,
            ForeColor = SettingsWindow.TxtMid,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 9.5f)
        };

        private void Add(Control c) => scrollHost.AddHostControl(c);

        private void RefreshPreview()
        {
            previewPanel?.Invalidate();
        }

        private void LoadValues()
        {
            isUpdating = true;
            var c   = Color.FromArgb(fenceInfo.BackgroundColor);
            var hsl = ColorUtil.FromColor(c);
            hueTrack.Value   = Math.Max(0, Math.Min(360, (int)hsl.H));
            satTrack.Value   = Math.Max(0, Math.Min(100, (int)(hsl.S * 100)));
            briTrack.Value   = Math.Max(0, Math.Min(100, (int)(hsl.L * 100)));
            alphaTrack.Value = Math.Max(0, Math.Min(255, (int)c.A));
            isUpdating = false;
        }

        private void UpdateColor()
        {
            if (isUpdating) return;
            var hsl = new ColorUtil.HSL
            {
                H = hueTrack.Value,
                S = satTrack.Value / 100f,
                L = briTrack.Value / 100f
            };
            var c = ColorUtil.ToColor(hsl, alphaTrack.Value);
            fenceInfo.BackgroundColor = c.ToArgb();
            fenceInfo.TitleColor      = Color.FromArgb(50, hsl.L > 0.6f ? 0 : 255, hsl.L > 0.6f ? 0 : 255, hsl.L > 0.6f ? 0 : 255).ToArgb();
            fenceInfo.TitleTextColor  = (hsl.L > 0.6f ? Color.Black : Color.White).ToArgb();
            onChanged?.Invoke();
        }

        private void ChangeFont(bool isTitle)
        {
            using (var fd = new FontDialog())
            {
                try { fd.Font = isTitle ? new Font(fenceInfo.TitleFontName, fenceInfo.TitleFontSize) : new Font(fenceInfo.ItemFontName, fenceInfo.ItemFontSize); }
                catch { }
                if (fd.ShowDialog(this) != DialogResult.OK) return;
                if (isTitle) { fenceInfo.TitleFontName = fd.Font.Name; fenceInfo.TitleFontSize = fd.Font.Size; }
                else         { fenceInfo.ItemFontName  = fd.Font.Name; fenceInfo.ItemFontSize  = fd.Font.Size; }
                onChanged?.Invoke();
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
