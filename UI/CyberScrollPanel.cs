using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NoFences.UI
{
    /// <summary>
    /// A sleek container that replaces WinForms ugly white AutoScroll scrollbars
    /// with a modern, smooth, floating Cyber-Glass scrollbar.
    /// </summary>
    public class CyberScrollPanel : Panel
    {
        private int _scrollOffset = 0;
        private int _maxScroll = 0;
        private bool _isDraggingScroll = false;
        private int _dragStartY = 0;
        private int _dragStartOffset = 0;
        private bool _isHoveringScrollbar = false;

        public CyberScrollPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            AutoScroll = false;
            BackColor = Color.FromArgb(15, 17, 26);
        }

        public void ScrollToTop()
        {
            _scrollOffset = 0;
            LayoutChildren();
            Invalidate();
        }

        public void UpdateLayout()
        {
            LayoutChildren();
            Invalidate();
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            LayoutChildren();
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            base.OnControlRemoved(e);
            LayoutChildren();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            LayoutChildren();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_maxScroll > 0)
            {
                _scrollOffset -= Math.Sign(e.Delta) * 38;
                _scrollOffset = Math.Max(0, Math.Min(_maxScroll, _scrollOffset));
                LayoutChildren();
                Invalidate();
            }
            base.OnMouseWheel(e);
        }

        private void LayoutChildren()
        {
            int totalHeight = 0;
            foreach (Control c in Controls)
            {
                if (c.Visible)
                {
                    int bottom = c.Location.Y + _scrollOffset + c.Height + c.Margin.Bottom;
                    if (bottom > totalHeight) totalHeight = bottom;
                }
            }

            _maxScroll = Math.Max(0, totalHeight - Height + 40);
            _scrollOffset = Math.Max(0, Math.Min(_maxScroll, _scrollOffset));

            // Shift children based on _scrollOffset
            SuspendLayout();
            foreach (Control c in Controls)
            {
                if (c.Tag is Point originalLoc)
                {
                    c.Location = new Point(originalLoc.X, originalLoc.Y - _scrollOffset);
                }
                else
                {
                    c.Tag = c.Location;
                    c.Location = new Point(c.Location.X, c.Location.Y - _scrollOffset);
                }
            }
            ResumeLayout(true);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool wasHovering = _isHoveringScrollbar;
            _isHoveringScrollbar = _maxScroll > 0 && e.X >= Width - 14;

            if (_isDraggingScroll)
            {
                int trackHeight = Height - 20;
                int thumbHeight = Math.Max(30, (int)((float)Height / (_maxScroll + Height) * trackHeight));
                int availableTrack = trackHeight - thumbHeight;

                if (availableTrack > 0)
                {
                    float delta = e.Y - _dragStartY;
                    float scrollRatio = delta / availableTrack;
                    _scrollOffset = (int)(_dragStartOffset + scrollRatio * _maxScroll);
                    _scrollOffset = Math.Max(0, Math.Min(_maxScroll, _scrollOffset));
                    LayoutChildren();
                    Invalidate();
                }
            }
            else if (wasHovering != _isHoveringScrollbar)
            {
                Invalidate();
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _maxScroll > 0 && e.X >= Width - 14)
            {
                _isDraggingScroll = true;
                _dragStartY = e.Y;
                _dragStartOffset = _scrollOffset;
                Capture = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_isDraggingScroll)
            {
                _isDraggingScroll = false;
                Capture = false;
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_maxScroll <= 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int trackHeight = Height - 20;
            int thumbHeight = Math.Max(32, (int)((float)Height / (_maxScroll + Height) * trackHeight));
            int thumbY = 10 + (int)((float)_scrollOffset / _maxScroll * (trackHeight - thumbHeight));
            int thumbWidth = _isHoveringScrollbar || _isDraggingScroll ? 6 : 4;
            int thumbX = Width - thumbWidth - 4;

            var thumbRect = new Rectangle(thumbX, thumbY, thumbWidth, thumbHeight);

            // Sleek Iridescent / Neon Thumb
            Color thumbColor = _isDraggingScroll ? Color.FromArgb(200, 0, 245, 212) :
                               _isHoveringScrollbar ? Color.FromArgb(160, 139, 92, 246) :
                               Color.FromArgb(90, 80, 95, 130);

            using (var b = new SolidBrush(thumbColor))
            using (var path = CreateRoundRect(thumbRect, thumbWidth / 2))
            {
                g.FillPath(b, path);
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
