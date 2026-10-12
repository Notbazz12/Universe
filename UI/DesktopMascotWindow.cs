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

        public enum SlimeAnimationState
        {
            Idle,
            Walk,
            Jump,
            React,
            Sleep
        }

        private SlimeAnimationState _animState = SlimeAnimationState.Idle;
        private int _currentFrame = 0;
        private int _frameTicks = 0;
        private readonly Timer _mainTimer;
        private float _floatAngle = 0f;
        private bool _isDragging = false;
        private Point _dragStartCursor;
        private Point _dragStartForm;
        private bool _hasDragged = false;
        private ContextMenuStrip _mascotMenu;

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
            int size = config.DesktopMascotSize > 0 ? config.DesktopMascotSize : 100;
            Size = new Size(size, size);

            TopMost = config.DesktopMascotTopMost;

            // Restore saved position or default near bottom-right
            if (config.DesktopMascotX >= 0 && config.DesktopMascotY >= 0)
            {
                Location = new Point(config.DesktopMascotX, config.DesktopMascotY);
            }
            else
            {
                var screen = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(screen.Right - size - 45, screen.Bottom - size - 45);
            }

            InitContextMenu();

            // Main loop timer (35ms for smooth 30fps animation & physics)
            _mainTimer = new Timer { Interval = 35 };
            _mainTimer.Tick += MainTimer_Tick;

            MouseDown += Mascot_MouseDown;
            MouseMove += Mascot_MouseMove;
            MouseUp += Mascot_MouseUp;
            MouseDoubleClick += Mascot_MouseDoubleClick;

            _mainTimer.Start();
            RenderMascot();
        }

        private void MainTimer_Tick(object sender, EventArgs e)
        {
            _floatAngle += 0.07f;
            if (_floatAngle > 6.283f) _floatAngle -= 6.283f;

            _frameTicks++;

            // Frame speed per animation state
            int ticksPerFrame = 6; // default (~210ms)
            switch (_animState)
            {
                case SlimeAnimationState.Walk:  ticksPerFrame = 4; break; // ~140ms
                case SlimeAnimationState.Jump:  ticksPerFrame = 4; break; // ~140ms
                case SlimeAnimationState.React: ticksPerFrame = 5; break; // ~175ms
                case SlimeAnimationState.Sleep: ticksPerFrame = 8; break; // ~280ms
                case SlimeAnimationState.Idle:  ticksPerFrame = 7; break; // ~245ms
            }

            if (_frameTicks >= ticksPerFrame)
            {
                _frameTicks = 0;
                _currentFrame++;

                if (_currentFrame >= 5)
                {
                    // Non-looping animations return to Idle
                    if (_animState == SlimeAnimationState.Jump || _animState == SlimeAnimationState.React)
                    {
                        _animState = SlimeAnimationState.Idle;
                        _currentFrame = 0;
                    }
                    else
                    {
                        _currentFrame = 0;
                    }
                }
            }

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
            petItem.Click += (s, e) => PlayAnimation(SlimeAnimationState.React);
            _mascotMenu.Items.Add(petItem);

            var moodMenu = new ToolStripMenuItem("✨ Animations & Poses");
            moodMenu.DropDownItems.Add("💫 Idle Breathing", null, (s, e) => PlayAnimation(SlimeAnimationState.Idle));
            moodMenu.DropDownItems.Add("🚶 Walk / Move", null, (s, e) => PlayAnimation(SlimeAnimationState.Walk));
            moodMenu.DropDownItems.Add("🦘 High Jump", null, (s, e) => PlayAnimation(SlimeAnimationState.Jump));
            moodMenu.DropDownItems.Add("💖 React / Love", null, (s, e) => PlayAnimation(SlimeAnimationState.React));
            moodMenu.DropDownItems.Add("💤 Sleep (Zzz)", null, (s, e) => PlayAnimation(SlimeAnimationState.Sleep));
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

        public void PlayAnimation(SlimeAnimationState state)
        {
            _animState = state;
            _currentFrame = 0;
            _frameTicks = 0;
            RenderMascot();
        }

        private Image GetCurrentFrameImage()
        {
            int frame = Math.Max(0, Math.Min(4, _currentFrame));

            switch (_animState)
            {
                case SlimeAnimationState.Walk:
                    var wf = MascotProvider.WalkFrames;
                    if (wf != null && wf.Length > frame && wf[frame] != null) return wf[frame];
                    break;

                case SlimeAnimationState.Jump:
                    var jf = MascotProvider.JumpFrames;
                    if (jf != null && jf.Length > frame && jf[frame] != null) return jf[frame];
                    break;

                case SlimeAnimationState.React:
                    var rf = MascotProvider.ReactFrames;
                    if (rf != null && rf.Length > frame && rf[frame] != null) return rf[frame];
                    break;

                case SlimeAnimationState.Sleep:
                    var sf = MascotProvider.SleepFrames;
                    if (sf != null && sf.Length > frame && sf[frame] != null) return sf[frame];
                    break;

                case SlimeAnimationState.Idle:
                default:
                    var idf = MascotProvider.IdleFrames;
                    if (idf != null && idf.Length > frame && idf[frame] != null) return idf[frame];
                    break;
            }

            // Fallback to static sprites
            switch (_animState)
            {
                case SlimeAnimationState.Sleep: return MascotProvider.Sleep;
                case SlimeAnimationState.Jump:  return MascotProvider.Jump;
                case SlimeAnimationState.React: return MascotProvider.Love ?? MascotProvider.Excited;
                default:                        return MascotProvider.Happy;
            }
        }

        private void RenderMascot()
        {
            if (!IsHandleCreated || IsDisposed) return;

            var sprite = GetCurrentFrameImage();
            if (sprite == null) return;

            int size = Width;
            int floatY = _animState == SlimeAnimationState.Sleep ? 0 : (int)(Math.Sin(_floatAngle) * 3);

            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    // Subtle celestial aura glow behind the slime
                    int auraPad = 10;
                    var auraRect = new Rectangle(auraPad, auraPad + floatY, size - auraPad * 2, size - auraPad * 2);
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(auraRect);
                        using (var pgb = new PathGradientBrush(path))
                        {
                            pgb.CenterColor = Color.FromArgb(45, 0, 220, 255);
                            pgb.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) };
                            g.FillEllipse(pgb, auraRect);
                        }
                    }

                    // Render Mascot Sprite Frame
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

                    // Switch to walk animation while dragging across screen
                    if (_animState != SlimeAnimationState.Walk)
                    {
                        _animState = SlimeAnimationState.Walk;
                        _currentFrame = 0;
                    }
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

                    // Return to Idle after finishing drag/walk
                    PlayAnimation(SlimeAnimationState.Idle);
                }
                else
                {
                    // Left-click interaction: trigger React or Jump
                    if (_animState == SlimeAnimationState.Idle)
                    {
                        PlayAnimation(SlimeAnimationState.React);
                    }
                    else if (_animState == SlimeAnimationState.React)
                    {
                        PlayAnimation(SlimeAnimationState.Jump);
                    }
                    else
                    {
                        PlayAnimation(SlimeAnimationState.Idle);
                    }
                }
            }
        }

        private void Mascot_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PlayAnimation(SlimeAnimationState.Jump);
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
                _mainTimer?.Stop();
                _mainTimer?.Dispose();
                _mascotMenu?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
