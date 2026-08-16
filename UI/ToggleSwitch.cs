using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NoFences.UI
{
    /// <summary>
    /// A sleek Cyber-Glass iridescent toggle switch.
    /// </summary>
    public class ToggleSwitch : Control
    {
        private bool _checked;
        private bool _hover;

        public bool Checked
        {
            get => _checked;
            set { _checked = value; Invalidate(); }
        }

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint  |
                     ControlStyles.UserPaint, true);
            Size        = new Size(46, 24);
            Cursor      = Cursors.Hand;
            BackColor   = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var trackRect = new RectangleF(0, 2, 44, 20);

            if (_checked)
            {
                // Glowing Iridescent gradient track
                using (var brush = new LinearGradientBrush(trackRect, Color.FromArgb(0, 245, 212), Color.FromArgb(139, 92, 246), 0f))
                using (var path = RoundRect(trackRect, 10))
                {
                    var cb = new ColorBlend(3)
                    {
                        Colors = new[] { Color.FromArgb(0, 245, 212), Color.FromArgb(139, 92, 246), Color.FromArgb(244, 63, 94) },
                        Positions = new[] { 0.0f, 0.5f, 1.0f }
                    };
                    brush.InterpolationColors = cb;
                    g.FillPath(brush, path);
                }

                // Inner highlight
                using (var glowPen = new Pen(Color.FromArgb(180, 255, 255, 255), 1.0f))
                using (var path = RoundRect(trackRect, 10))
                {
                    g.DrawPath(glowPen, path);
                }
            }
            else
            {
                // Dark obsidian track
                Color offBg = _hover ? Color.FromArgb(38, 44, 66) : Color.FromArgb(26, 30, 46);
                Color offBorder = _hover ? Color.FromArgb(70, 80, 115) : Color.FromArgb(48, 56, 82);

                using (var b = new SolidBrush(offBg))
                using (var p = new Pen(offBorder, 1.2f))
                using (var path = RoundRect(trackRect, 10))
                {
                    g.FillPath(b, path);
                    g.DrawPath(p, path);
                }
            }

            // Thumb
            float thumbX = _checked ? 24 : 3;
            var thumbRect = new RectangleF(thumbX, 4, 16, 16);

            // Thumb soft shadow
            using (var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            {
                g.FillEllipse(shadowBrush, thumbX + 0.5f, 5.0f, 16, 16);
            }

            // Thumb pure white disk
            using (var thumbBrush = new SolidBrush(Color.White))
            {
                g.FillEllipse(thumbBrush, thumbRect);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true;  Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            _checked = !_checked;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
            base.OnClick(e);
        }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
