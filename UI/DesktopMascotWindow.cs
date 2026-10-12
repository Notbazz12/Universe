using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NoFences.Model;
using NoFences.Util;
using NoFences.Win32;

namespace NoFences.UI
{
    public class DesktopMascotWindow : Form
    {
        private static DesktopMascotWindow _instance;
        public static DesktopMascotWindow Instance => _instance;

        private MascotMood _currentMood = MascotMood.Happy;
        private readonly Timer _floatTimer;
        private readonly Timer _moodTimer;
        private float _floatAngle = 0f;
        private bool _isDragging = false;
        private Point _dragStartCursor;
        private Point _dragStartForm;
        private bool _hasDragged = false;
        private ContextMenuStrip _mascotMenu;

        public enum MascotMood
        {
            Happy,
            Wink,
            Love,
            Excited,
            Jump,
            Sleep
        }

        #region Win32 P/Invoke for Per-Pixel Alpha Layered Window

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
            public POINT(int x, int y) { this.x = x; this.y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
            public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        private const int ULW_ALPHA = 0x02;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        #endregion

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW (hide from Alt+Tab)
                return cp;
            }
        }

        public DesktopMascotWindow()
        {
            _instance = this;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;

            var config = AppConfig.Load();
            int size = config.DesktopMascotSize > 0 ? config.DesktopMascotSize : 96;
            Size = new Size(size, size);

            TopMost = config.DesktopMascotTopMost;

            // Restore saved position or default near bottom-right above taskbar
            if (config.DesktopMascotX >= 0 && config.DesktopMascotY >= 0)
            {
                Location = new Point(config.DesktopMascotX, config.DesktopMascotY);
            }
            else
            {
                var screen = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(screen.Right - size - 40, screen.Bottom - size - 40);
            }

            InitContextMenu();

            // Zero-gravity floating timer (30ms interval for fluid physics)
            _floatTimer = new Timer { Interval = 35 };
            _floatTimer.Tick += (s, e) =>
            {
                _floatAngle += 0.07f;
                if (_floatAngle > 6.283f) _floatAngle -= 6.283f;
                RenderMascot();
            };

            // Temporary mood timer (resets back to Happy after interactive reactions)
            _moodTimer = new Timer { Interval = 2500 };
            _moodTimer.Tick += (s, e) =>
            {
                _moodTimer.Stop();
                if (_currentMood != MascotMood.Sleep)
                {
                    _currentMood = MascotMood.Happy;
                    RenderMascot();
                }
            };

            MouseDown += Mascot_MouseDown;
            MouseMove += Mascot_MouseMove;
            MouseUp += Mascot_MouseUp;
            MouseDoubleClick += Mascot_MouseDoubleClick;

            _floatTimer.Start();
            RenderMascot();
        }

        private void InitContextMenu()
        {
            _mascotMenu = new ContextMenuStrip();
            _mascotMenu.Renderer = new FenceMenuRenderer();

            var titleItem = new ToolStripMenuItem("🐾 Cosmic Mascot Companion") { Enabled = false };
            titleItem.Font = new Font(_mascotMenu.Font, FontStyle.Bold);
            _mascotMenu.Items.Add(titleItem);
            _mascotMenu.Items.Add(new ToolStripSeparator());

            var petItem = new ToolStripMenuItem("💖 Pet Mascot (Give Love)");
            petItem.Click += (s, e) => SetMood(MascotMood.Love, 3500);
            _mascotMenu.Items.Add(petItem);

            var moodMenu = new ToolStripMenuItem("✨ Expressions");
            moodMenu.DropDownItems.Add("😄 Happy", null, (s, e) => SetMood(MascotMood.Happy));
            moodMenu.DropDownItems.Add("😉 Wink", null, (s, e) => SetMood(MascotMood.Wink, 3000));
            moodMenu.DropDownItems.Add("🚀 Excited", null, (s, e) => SetMood(MascotMood.Excited, 3000));
            moodMenu.DropDownItems.Add("🦘 Jump", null, (s, e) => SetMood(MascotMood.Jump, 3000));
            moodMenu.DropDownItems.Add("💤 Sleep (Zzz)", null, (s, e) => SetMood(MascotMood.Sleep));
            _mascotMenu.Items.Add(moodMenu);

            _mascotMenu.Items.Add(new ToolStripSeparator());

            var topMostItem = new ToolStripMenuItem("🔝 Always on Top") { Checked = TopMost };
            topMostItem.Click += (s, e) =>
            {
                TopMost = !TopMost;
                topMostItem.Checked = TopMost;
                var cfg = AppConfig.Load();
                cfg.DesktopMascotTopMost = TopMost;
                cfg.Save();
            };
            _mascotMenu.Items.Add(topMostItem);

            var hideItem = new ToolStripMenuItem("❌ Hide Desktop Mascot");
            hideItem.Click += (s, e) =>
            {
                Hide();
                var cfg = AppConfig.Load();
                cfg.ShowDesktopMascot = false;
                cfg.Save();
            };
            _mascotMenu.Items.Add(hideItem);

            ContextMenuStrip = _mascotMenu;
        }

        public void SetMood(MascotMood mood, int durationMs = 0)
        {
            _currentMood = mood;
            RenderMascot();

            _moodTimer.Stop();
            if (durationMs > 0)
            {
                _moodTimer.Interval = durationMs;
                _moodTimer.Start();
            }
        }

        private Image GetCurrentImage()
        {
            switch (_currentMood)
            {
                case MascotMood.Wink:    return MascotProvider.Wink ?? MascotProvider.Happy;
                case MascotMood.Love:    return MascotProvider.Love ?? MascotProvider.Happy;
                case MascotMood.Excited: return MascotProvider.Excited ?? MascotProvider.Happy;
                case MascotMood.Jump:    return MascotProvider.Jump ?? MascotProvider.Happy;
                case MascotMood.Sleep:   return MascotProvider.Sleep ?? MascotProvider.Happy;
                default:                 return MascotProvider.Happy;
            }
        }

        private void RenderMascot()
        {
            if (!IsHandleCreated || IsDisposed) return;

            var sprite = GetCurrentImage();
            if (sprite == null) return;

            int size = Width;
            int floatY = (int)(Math.Sin(_floatAngle) * 3);

            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    // Subtle celestial aura behind the slime
                    int auraPad = 8;
                    var auraRect = new Rectangle(auraPad, auraPad + floatY, size - auraPad * 2, size - auraPad * 2);
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(auraRect);
                        using (var pgb = new PathGradientBrush(path))
                        {
                            pgb.CenterColor = Color.FromArgb(40, 0, 220, 255);
                            pgb.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) };
                            g.FillEllipse(pgb, auraRect);
                        }
                    }

                    // Render Mascot Sprite
                    g.DrawImage(sprite, 4, 4 + floatY, size - 8, size - 8);
                }

                // Pre-multiply alpha for UpdateLayeredWindow
                unsafe
                {
                    var data = bmp.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                    byte* scan0 = (byte*)data.Scan0.ToPointer();
                    int total = data.Stride * size;
                    for (int i = 0; i < total; i += 4)
                    {
                        byte b = scan0[i];
                        byte gVal = scan0[i + 1];
                        byte r = scan0[i + 2];
                        byte a = scan0[i + 3];

                        if (a == 0)
                        {
                            scan0[i] = scan0[i + 1] = scan0[i + 2] = 0;
                        }
                        else if (a < 255)
                        {
                            scan0[i]     = (byte)((b * a) / 255);
                            scan0[i + 1] = (byte)((gVal * a) / 255);
                            scan0[i + 2] = (byte)((r * a) / 255);
                        }
                    }
                    bmp.UnlockBits(data);
                }

                SetLayeredBitmap(bmp);
            }
        }

        private void SetLayeredBitmap(Bitmap bmp)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);

                var size = new SIZE(bmp.Width, bmp.Height);
                var pointSource = new POINT(0, 0);
                var topPos = new POINT(Left, Top);
                var blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(Handle, screenDc, ref topPos, ref size, memDc, ref pointSource, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    SelectObject(memDc, oldBitmap);
                    DeleteObject(hBitmap);
                }
                DeleteDC(memDc);
            }
        }

        private void Mascot_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                _hasDragged = false;
                _dragStartCursor = Cursor.Position;
                _dragStartForm = Location;
            }
            else if (e.Button == MouseButtons.Right)
            {
                _mascotMenu?.Show(Cursor.Position);
            }
        }

        private void Mascot_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                int dx = Cursor.Position.X - _dragStartCursor.X;
                int dy = Cursor.Position.Y - _dragStartCursor.Y;

                if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3)
                {
                    _hasDragged = true;
                    Location = new Point(_dragStartForm.X + dx, _dragStartForm.Y + dy);
                }
            }
        }

        private void Mascot_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                if (_hasDragged)
                {
                    // Save new position
                    var cfg = AppConfig.Load();
                    cfg.DesktopMascotX = Location.X;
                    cfg.DesktopMascotY = Location.Y;
                    cfg.Save();
                }
                else
                {
                    // Click: Cycle expressions
                    CycleMood();
                }
            }
        }

        private void Mascot_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                SetMood(MascotMood.Jump, 3000);
            }
        }

        private void CycleMood()
        {
            switch (_currentMood)
            {
                case MascotMood.Happy:   SetMood(MascotMood.Wink, 2500); break;
                case MascotMood.Wink:    SetMood(MascotMood.Love, 2500); break;
                case MascotMood.Love:    SetMood(MascotMood.Excited, 2500); break;
                case MascotMood.Excited: SetMood(MascotMood.Jump, 2500); break;
                case MascotMood.Jump:    SetMood(MascotMood.Happy); break;
                default:                 SetMood(MascotMood.Happy); break;
            }
        }

        public static void ToggleVisibility()
        {
            var cfg = AppConfig.Load();
            if (_instance == null || _instance.IsDisposed)
            {
                _instance = new DesktopMascotWindow();
                _instance.Show();
                cfg.ShowDesktopMascot = true;
            }
            else
            {
                if (_instance.Visible)
                {
                    _instance.Hide();
                    cfg.ShowDesktopMascot = false;
                }
                else
                {
                    _instance.Show();
                    _instance.RenderMascot();
                    cfg.ShowDesktopMascot = true;
                }
            }
            cfg.Save();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _floatTimer?.Stop();
                _floatTimer?.Dispose();
                _moodTimer?.Stop();
                _moodTimer?.Dispose();
                _mascotMenu?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
