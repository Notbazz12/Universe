using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using static NoFences.Services.LocalizationManager;

namespace NoFences.UI
{
    /// <summary>
    /// Branded "new version" card shown bottom-right instead of a plain tray balloon.
    /// </summary>
    public class UpdateToast : Form
    {
        private static UpdateToast _current;

        private readonly string _version;
        private readonly string _notes;
        private readonly Action _onUpdate;
        private readonly Image _logo;
        private readonly Timer _fadeTimer = new Timer { Interval = 15 };
        private readonly Timer _lifeTimer = new Timer { Interval = 30000 };
        private Rectangle _closeRect;
        private bool _closeHover;

        public static void ShowUpdate(string version, string notes, Action onUpdate)
        {
            _current?.Close();
            _current = new UpdateToast(version, notes, onUpdate);
            _current.Show();
        }

        private UpdateToast(string version, string notes, Action onUpdate)
        {
            _version  = version;
            _notes    = string.IsNullOrWhiteSpace(notes) ? T("Performance improvements and bug fixes.", "Mejoras de rendimiento y correcciones.") : notes;
            _onUpdate = onUpdate;
            _logo     = LoadLogo();

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar   = false;
            TopMost         = true;
            StartPosition   = FormStartPosition.Manual;
            BackColor       = SettingsWindow.BgCard;
            Font            = new Font("Segoe UI", 9f);
            Size            = new Size(380, 168);
            Opacity         = 0;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            var area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);

            var btnUpdate = MakeButton(T("Update now", "Actualizar ahora"), SettingsWindow.AccentNeon, true);
            btnUpdate.Location = new Point(88, Height - 46);
            btnUpdate.Click += (s, e) => { Close(); _onUpdate?.Invoke(); };
            Controls.Add(btnUpdate);

            var btnLater = MakeButton(T("Later", "Más tarde"), Color.FromArgb(70, 85, 120), false);
            btnLater.Location = new Point(btnUpdate.Right + 10, Height - 46);
            btnLater.Click += (s, e) => Close();
            Controls.Add(btnLater);

            _closeRect = new Rectangle(Width - 30, 10, 18, 18);

            _fadeTimer.Tick += (s, e) =>
            {
                Opacity = Math.Min(1.0, Opacity + 0.08);
                if (Opacity >= 1.0) _fadeTimer.Stop();
            };
            // Auto-dismiss after a while; the tray menu keeps the "update available" entry
            _lifeTimer.Tick += (s, e) =>
            {
                if (Bounds.Contains(Cursor.Position)) return; // not while the user is reading it
                Close();
            };
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80;      // WS_EX_TOOLWINDOW: no Alt+Tab entry
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            System.Media.SystemSounds.Asterisk.Play();
            _fadeTimer.Start();
            _lifeTimer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _fadeTimer.Dispose();
            _lifeTimer.Dispose();
            if (_current == this) _current = null;
            base.OnFormClosed(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hover = _closeRect.Contains(e.Location);
            if (hover != _closeHover) { _closeHover = hover; Invalidate(_closeRect); }
            Cursor = hover ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_closeRect.Contains(e.Location)) Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Accent strip + border
            using (var strip = new LinearGradientBrush(new Rectangle(0, 0, Width, 4), SettingsWindow.AccentNeon, SettingsWindow.AccentVio, 0f))
                g.FillRectangle(strip, 0, 0, Width, 4);
            using (var pen = new Pen(SettingsWindow.BorderCard))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            // Logo inside a soft glow
            var logoRect = new Rectangle(20, 24, 52, 52);
            using (var glow = new SolidBrush(Color.FromArgb(40, SettingsWindow.AccentVio)))
                g.FillEllipse(glow, Rectangle.Inflate(logoRect, 6, 6));
            if (_logo != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(_logo, logoRect);
            }

            using (var small = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var accent = new SolidBrush(SettingsWindow.AccentNeon))
                g.DrawString(T("UPDATE AVAILABLE", "ACTUALIZACIÓN DISPONIBLE"), small, accent, 88, 20);

            using (var title = new Font("Segoe UI", 13f, FontStyle.Bold))
                g.DrawString("Universe v" + _version, title, Brushes.White, 86, 36);

            using (var notesFont = new Font("Segoe UI", 8.5f))
            using (var muted = new SolidBrush(SettingsWindow.TxtMuted))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisWord })
                g.DrawString(_notes, notesFont, muted, new RectangleF(88, 64, Width - 108, 34), sf);

            // Close "x"
            using (var pen = new Pen(_closeHover ? Color.White : SettingsWindow.TxtMuted, 1.5f))
            {
                var r = Rectangle.Inflate(_closeRect, -5, -5);
                g.DrawLine(pen, r.Left, r.Top, r.Right, r.Bottom);
                g.DrawLine(pen, r.Right, r.Top, r.Left, r.Bottom);
            }
        }

        private Button MakeButton(string text, Color accent, bool primary)
        {
            var b = new Button
            {
                Text      = text,
                Height    = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Color.FromArgb(20, 60, 70) : Color.FromArgb(28, 32, 50),
                ForeColor = primary ? SettingsWindow.AccentNeon : Color.White,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            b.Width = TextRenderer.MeasureText(text, b.Font).Width + 32;
            b.FlatAppearance.BorderColor = accent;
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(25, 85, 95) : Color.FromArgb(35, 42, 68);
            return b;
        }

        private static Image LoadLogo()
        {
            // universe.ico is installed next to the exe; ask for a large frame so it stays sharp
            try
            {
                var ico = Path.Combine(Path.GetDirectoryName(typeof(UpdateToast).Assembly.Location) ?? "", "universe.ico");
                if (File.Exists(ico))
                    using (var icon = new Icon(ico, 64, 64))
                        return icon.ToBitmap();
            }
            catch { }
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath)?.ToBitmap(); }
            catch { return null; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _logo?.Dispose();
            base.Dispose(disposing);
        }
    }
}
