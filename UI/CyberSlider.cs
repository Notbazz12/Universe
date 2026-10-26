using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NoFences.UI
{
    /// <summary>
    /// Dark Cyber-Glass slider replacing the white WinForms TrackBar. Same Minimum / Maximum /
    /// Value / ValueChanged surface, with drag, click, mouse wheel and arrow key support.
    /// </summary>
    public class CyberSlider : Control
    {
        private int _min, _max = 100, _value;
        private bool _dragging, _hover;

        private const int ThumbRadius = 8;
        private const int TrackHeight = 4;

        public event EventHandler ValueChanged;

        /// <summary>Draws the track as a hue rainbow (for hue pickers) instead of the accent gradient.</summary>
        public bool RainbowTrack { get; set; }

        public CyberSlider()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 28;
        }

        public int Minimum
        {
            get => _min;
            set { _min = value; if (_max < _min) _max = _min; Value = _value; Invalidate(); }
        }

        public int Maximum
        {
            get => _max;
            set { _max = Math.Max(value, _min); Value = _value; Invalidate(); }
        }

        public int Value
        {
            get => _value;
            set
            {
                int v = Math.Max(_min, Math.Min(_max, value));
                if (v == _value) return;
                _value = v;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private Rectangle TrackRect =>
            new Rectangle(ThumbRadius + 1, Height / 2 - TrackHeight / 2, Math.Max(1, Width - 2 * ThumbRadius - 2), TrackHeight);

        private int ThumbX
        {
            get
            {
                var t = TrackRect;
                float ratio = _max == _min ? 0f : (float)(_value - _min) / (_max - _min);
                return t.Left + (int)Math.Round(ratio * t.Width);
            }
        }

        private void SetFromX(int x)
        {
            var t = TrackRect;
            float ratio = Math.Max(0f, Math.Min(1f, (float)(x - t.Left) / t.Width));
            Value = _min + (int)Math.Round(ratio * (_max - _min));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            _dragging = true;
            Capture = true;
            SetFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
            Capture = false;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Value += Math.Sign(e.Delta) * Math.Max(1, (_max - _min) / 50);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int step = Math.Max(1, (_max - _min) / 100);
            if (e.KeyCode == Keys.Left) Value -= step;
            else if (e.KeyCode == Keys.Right) Value += step;
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var track = TrackRect;
            int thumbX = ThumbX;

            using (var bg = new SolidBrush(Color.FromArgb(40, 47, 72)))
                g.FillRectangle(bg, track);

            if (RainbowTrack)
            {
                using (var rainbow = new LinearGradientBrush(track, Color.Red, Color.Red, 0f))
                {
                    rainbow.InterpolationColors = new ColorBlend
                    {
                        Colors = new[] { Color.Red, Color.Yellow, Color.Lime, Color.Cyan, Color.Blue, Color.Magenta, Color.Red },
                        Positions = new[] { 0f, 1f / 6, 2f / 6, 3f / 6, 4f / 6, 5f / 6, 1f }
                    };
                    g.FillRectangle(rainbow, track);
                }
            }
            else if (thumbX > track.Left)
            {
                var filled = new Rectangle(track.Left, track.Top, thumbX - track.Left, track.Height);
                using (var fill = new LinearGradientBrush(new Rectangle(track.Left, track.Top, Math.Max(1, track.Width), track.Height),
                    SettingsWindow.AccentNeon, SettingsWindow.AccentVio, 0f))
                    g.FillRectangle(fill, filled);
            }

            int r = (_dragging || _hover || Focused) ? ThumbRadius : ThumbRadius - 1;
            var thumb = new Rectangle(thumbX - r, Height / 2 - r, r * 2, r * 2);

            if (_dragging || Focused)
            {
                var glow = thumb;
                glow.Inflate(4, 4);
                using (var glowBrush = new SolidBrush(Color.FromArgb(50, SettingsWindow.AccentNeon)))
                    g.FillEllipse(glowBrush, glow);
            }

            using (var thumbBrush = new SolidBrush(SettingsWindow.TxtHigh))
                g.FillEllipse(thumbBrush, thumb);
            using (var ring = new Pen(SettingsWindow.AccentVio, 2f))
                g.DrawEllipse(ring, thumb);
        }
    }
}
