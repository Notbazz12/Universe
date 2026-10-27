using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NoFences.Model;
using NoFences.Util;
using NoFences.Win32;
using static NoFences.Services.LocalizationManager;

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

        // Rendered (and alpha-premultiplied) frames, keyed by state/frame/float offset.
        // Rendering is the expensive part, so each distinct frame is drawn only once.
        private readonly System.Collections.Generic.Dictionary<int, Bitmap> _frameCache = new System.Collections.Generic.Dictionary<int, Bitmap>();
        private int _lastKey = -1;

        // Autonomous behaviour
        private readonly Random _rng = new Random();
        private int _facing = 1;            // 1 = right, -1 = left (sprite is mirrored when walking left)
        private int _walkRemaining;         // pixels left to wander
        private int _ticksToNextIdea = 200; // countdown until the pet decides to do something on its own
        private int _jumpBaseY = int.MinValue;
        private bool _autoSlept;            // fell asleep because the user went idle

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private static uint UserIdleMs()
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            return GetLastInputInfo(ref info) ? unchecked((uint)Environment.TickCount - info.dwTime) : 0;
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

            MascotProvider.SkinChanged += MascotProvider_SkinChanged;

            // The timer only runs while the window is visible (see OnVisibleChanged)
        }

        private void MascotProvider_SkinChanged(object sender, EventArgs e)
        {
            ClearFrameCache();
            RenderMascot();
        }

        private void ClearFrameCache()
        {
            foreach (var cached in _frameCache.Values) cached.Dispose();
            _frameCache.Clear();
            _lastKey = -1;
        }

        /// <summary>Happy bounce, e.g. when files are dropped into a fence.</summary>
        public static void Cheer()
        {
            var pet = _instance;
            if (pet == null || pet.IsDisposed || !pet.Visible) return;
            pet._autoSlept = false;
            pet.PlayAnimation(SlimeAnimationState.React);
        }

        /// <summary>Wandering, hopping, idle-sleep: the pet acts on its own between clicks.</summary>
        private void UpdateBehavior()
        {
            if (_isDragging) return;

            // Fall asleep when the user is away for 3 minutes; wake up as soon as they are back
            uint idleMs = UserIdleMs();
            if (idleMs > 180000 && _animState == SlimeAnimationState.Idle)
            {
                _autoSlept = true;
                PlayAnimation(SlimeAnimationState.Sleep);
                return;
            }
            if (_autoSlept && _animState == SlimeAnimationState.Sleep && idleMs < 1500)
            {
                _autoSlept = false;
                PlayAnimation(SlimeAnimationState.React);
                return;
            }

            // Real hop: lift the window along a parabola while the jump frames play
            if (_animState == SlimeAnimationState.Jump && _jumpBaseY != int.MinValue)
            {
                float t = Math.Min(1f, (_currentFrame * 4 + _frameTicks) / 20f);
                Top = _jumpBaseY - (int)(Math.Sin(t * Math.PI) * Height * 0.45);
            }

            if (_animState == SlimeAnimationState.Walk && _walkRemaining > 0)
            {
                var area = Screen.FromControl(this).WorkingArea;
                int next = Left + _facing * 2;
                if (next < area.Left || next + Width > area.Right)
                {
                    _facing = -_facing; // bump into the edge and turn around
                    next = Left + _facing * 2;
                }
                Left = next;
                _walkRemaining -= 2;
                if (_walkRemaining <= 0)
                {
                    SavePosition();
                    PlayAnimation(SlimeAnimationState.Idle);
                }
                return;
            }

            if (_animState != SlimeAnimationState.Idle || !AppConfig.Load().MascotWander) return;
            if (--_ticksToNextIdea > 0) return;
            _ticksToNextIdea = _rng.Next(170, 430); // next idea in ~6-15 s

            int roll = _rng.Next(100);
            if (roll < 45)
            {
                _facing = _rng.Next(2) == 0 ? -1 : 1;
                _walkRemaining = _rng.Next(60, 220);
                PlayAnimation(SlimeAnimationState.Walk);
            }
            else if (roll < 65) PlayAnimation(SlimeAnimationState.Jump);
            else if (roll < 80) PlayAnimation(SlimeAnimationState.React);
            // otherwise: keep floating
        }

        private void SavePosition()
        {
            var cfg = AppConfig.Load();
            cfg.DesktopMascotX = Location.X;
            cfg.DesktopMascotY = _jumpBaseY != int.MinValue ? _jumpBaseY : Location.Y;
            cfg.Save();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (_mainTimer == null) return;

            if (Visible)
            {
                _lastKey = -1;
                _mainTimer.Start();
                RenderMascot();
            }
            else
            {
                _mainTimer.Stop();
            }
        }

        private void MainTimer_Tick(object sender, EventArgs e)
        {
            // Sleeping needs far fewer wake-ups (no floating, slow frames)
            int wantedInterval = _animState == SlimeAnimationState.Sleep ? 70 : 35;
            if (_mainTimer.Interval != wantedInterval) _mainTimer.Interval = wantedInterval;

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
                case SlimeAnimationState.Sleep: ticksPerFrame = 4; break; // ~280ms (70ms ticks)
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
                        if (_animState == SlimeAnimationState.Jump) Land();
                        _animState = SlimeAnimationState.Idle;
                        _currentFrame = 0;
                    }
                    else
                    {
                        _currentFrame = 0;
                    }
                }
            }

            UpdateBehavior();
            RenderMascot();
        }

        private void Land()
        {
            if (_jumpBaseY == int.MinValue) return;
            Top = _jumpBaseY;
            _jumpBaseY = int.MinValue;
        }

        private void InitContextMenu()
        {
            _mascotMenu = new ContextMenuStrip();
            _mascotMenu.Renderer = new FenceMenuRenderer();

            var titleItem = new ToolStripMenuItem(MascotName()) { Enabled = false };
            titleItem.Font = new Font(_mascotMenu.Font, FontStyle.Bold);
            _mascotMenu.Items.Add(titleItem);
            _mascotMenu.Items.Add(new ToolStripSeparator());

            var petItem = new ToolStripMenuItem(T("Pet it", "Acariciar"), FenceMenuRenderer.Glyph(""));
            petItem.Click += (s, e) => { _autoSlept = false; PlayAnimation(SlimeAnimationState.React); };
            _mascotMenu.Items.Add(petItem);

            var moodMenu = new ToolStripMenuItem(T("Actions", "Acciones"), FenceMenuRenderer.Glyph(""));
            moodMenu.DropDownItems.Add(T("Float", "Flotar"), null, (s, e) => PlayAnimation(SlimeAnimationState.Idle));
            moodMenu.DropDownItems.Add(T("Take a walk", "Dar un paseo"), null, (s, e) =>
            {
                _facing = _rng.Next(2) == 0 ? -1 : 1;
                _walkRemaining = 160;
                PlayAnimation(SlimeAnimationState.Walk);
            });
            moodMenu.DropDownItems.Add(T("Jump", "Saltar"), null, (s, e) => PlayAnimation(SlimeAnimationState.Jump));
            moodMenu.DropDownItems.Add(T("Sleep", "Dormir"), null, (s, e) => PlayAnimation(SlimeAnimationState.Sleep));
            _mascotMenu.Items.Add(moodMenu);

            // Pick a mascot (colour variants of the slime)
            var skinMenu = new ToolStripMenuItem(T("Choose mascot", "Elegir mascota"), FenceMenuRenderer.Glyph(""));
            foreach (var skin in MascotProvider.Skins)
            {
                var id = skin.Id;
                var item = new ToolStripMenuItem(T(skin.NameEn, skin.NameEs)) { Tag = id };
                item.Click += (s, e) =>
                {
                    MascotProvider.SetSkin(id);
                    titleItem.Text = MascotName();
                    foreach (Form f in Application.OpenForms) f.Invalidate(); // fence headers show the mascot too
                    PlayAnimation(SlimeAnimationState.React);
                };
                skinMenu.DropDownItems.Add(item);
            }
            skinMenu.DropDownOpening += (s, e) =>
            {
                foreach (ToolStripMenuItem it in skinMenu.DropDownItems)
                    it.Checked = (string)it.Tag == MascotProvider.CurrentSkin.Id;
            };
            _mascotMenu.Items.Add(skinMenu);

            var sizeMenu = new ToolStripMenuItem(T("Size", "Tamaño"), FenceMenuRenderer.Glyph(""));
            foreach (var opt in new[] { Tuple.Create(72, T("Small", "Pequeña")), Tuple.Create(96, T("Medium", "Mediana")), Tuple.Create(128, T("Large", "Grande")) })
            {
                int px = opt.Item1;
                var item = new ToolStripMenuItem(opt.Item2) { Tag = px };
                item.Click += (s, e) => SetPetSize(px);
                sizeMenu.DropDownItems.Add(item);
            }
            sizeMenu.DropDownOpening += (s, e) =>
            {
                foreach (ToolStripMenuItem it in sizeMenu.DropDownItems) it.Checked = (int)it.Tag == Width;
            };
            _mascotMenu.Items.Add(sizeMenu);

            var wanderItem = new ToolStripMenuItem(T("Wander on its own", "Pasear por su cuenta"), FenceMenuRenderer.Glyph("")) { Checked = AppConfig.Load().MascotWander };
            wanderItem.Click += (s, e) =>
            {
                var cfg = AppConfig.Load();
                cfg.MascotWander = !cfg.MascotWander;
                cfg.Save();
                wanderItem.Checked = cfg.MascotWander;
            };
            _mascotMenu.Items.Add(wanderItem);

            _mascotMenu.Items.Add(new ToolStripSeparator());

            var topMostItem = new ToolStripMenuItem(T("Always on top", "Siempre visible"), FenceMenuRenderer.Glyph("")) { Checked = TopMost };
            topMostItem.Click += (s, e) =>
            {
                TopMost = !TopMost;
                topMostItem.Checked = TopMost;
                var cfg = AppConfig.Load();
                cfg.DesktopMascotTopMost = TopMost;
                cfg.Save();
            };
            _mascotMenu.Items.Add(topMostItem);

            var hideItem = new ToolStripMenuItem(T("Hide mascot", "Ocultar mascota"), FenceMenuRenderer.Glyph(""));
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

        private static string MascotName()
        {
            var skin = MascotProvider.CurrentSkin;
            return T(skin.NameEn, skin.NameEs);
        }

        private void SetPetSize(int px)
        {
            if (px == Width) return;
            int bottom = Bottom; // grow upwards so it keeps standing on the same spot
            Land();
            Size = new Size(px, px);
            Top = bottom - px;
            ClearFrameCache();
            RenderMascot();
            var cfg = AppConfig.Load();
            cfg.DesktopMascotSize = px;
            cfg.Save();
            SavePosition();
        }

        public void PlayAnimation(SlimeAnimationState state)
        {
            if (state != SlimeAnimationState.Jump) Land();
            else if (_jumpBaseY == int.MinValue) _jumpBaseY = Top;
            if (state != SlimeAnimationState.Walk) _walkRemaining = 0;
            _animState = state;
            _currentFrame = 0;
            _frameTicks = 0;
            if (_mainTimer != null && Visible && !_mainTimer.Enabled) _mainTimer.Start();
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
            if (!IsHandleCreated || IsDisposed || !Visible) return;

            int floatY = _animState == SlimeAnimationState.Sleep ? 0 : (int)Math.Round(Math.Sin(_floatAngle) * 3);
            int frame = Math.Max(0, Math.Min(4, _currentFrame));
            bool flip = _facing < 0;
            int key = (flip ? 1 << 24 : 0) | ((int)_animState << 16) | (frame << 8) | (floatY + 3);

            // Nothing visible changed since the last frame: skip all drawing work
            if (key == _lastKey) return;

            if (!_frameCache.TryGetValue(key, out var bmp))
            {
                var sprite = GetCurrentFrameImage();
                if (sprite == null) return;

                bmp = RenderFrame(sprite, floatY, flip);
                _frameCache[key] = bmp;
            }

            _lastKey = key;
            SetLayeredBitmap(bmp); // the cache owns the bitmap: do not dispose it here
        }

        private Bitmap RenderFrame(Image sprite, int floatY, bool flip)
        {
            int size = Width;
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);

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

                // Soft contact shadow so the pet sits on the desktop (not while airborne)
                int shadowW = _animState == SlimeAnimationState.Jump ? 0 : (int)(size * 0.55) - Math.Abs(floatY) * 2;
                using (var shadow = new SolidBrush(Color.FromArgb(45, 0, 0, 0)))
                    g.FillEllipse(shadow, (size - shadowW) / 2, size - 14, shadowW, 8);

                // Render Mascot Sprite Frame (mirrored when walking left)
                if (flip)
                {
                    g.TranslateTransform(size, 0);
                    g.ScaleTransform(-1, 1);
                }
                g.DrawImage(sprite, 4, 4 + floatY, size - 8, size - 8);
                g.ResetTransform();
            }

            // Pre-multiply alpha for UpdateLayeredWindow (once per cached frame)
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

            return bmp;
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
                Land();
                _walkRemaining = 0;
                _autoSlept = false;
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
                MascotProvider.SkinChanged -= MascotProvider_SkinChanged;
                foreach (var cached in _frameCache.Values) cached.Dispose();
                _frameCache.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
