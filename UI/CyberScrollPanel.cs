using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NoFences.UI
{
    /// <summary>
    /// A sleek, high-performance scroll container that replaces WinForms white scrollbars
    /// with a modern floating Cyber-Glass scrollbar. Supports global mouse wheel over all child controls.
    /// </summary>
    public class CyberScrollPanel : Panel, IMessageFilter
    {
        private readonly Panel _content;
        private int _scrollOffset = 0;
        private int _maxScroll = 0;
        private bool _isDraggingScroll = false;
        private int _dragStartY = 0;
        private int _dragStartOffset = 0;
        private bool _isHoveringScrollbar = false;

        private const int WM_MOUSEWHEEL = 0x020A;
        private const int SCROLLBAR_WIDTH = 8;
        private const int HIT_AREA_WIDTH = 22;

        public CyberScrollPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            AutoScroll = false;
            BackColor = Color.FromArgb(15, 17, 26);

            _content = new Panel
            {
                BackColor = Color.Transparent,
                Location = new Point(0, 0),
                Width = Width,
                Height = Height
            };
            base.Controls.Add(_content);

            Application.AddMessageFilter(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { Application.RemoveMessageFilter(this); } catch { }
            }
            base.Dispose(disposing);
        }

        public Control.ControlCollection HostControls => _content.Controls;

        public void AddHostControl(Control c)
        {
            _content.Controls.Add(c);
            RecalculateLayout();
        }

        public void ClearHostControls()
        {
            _content.Controls.Clear();
            RecalculateLayout();
        }

        public void ScrollToTop()
        {
            _scrollOffset = 0;
            ApplyScroll();
        }

        public void UpdateLayout()
        {
            RecalculateLayout();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            RecalculateLayout();
        }

        public void RecalculateLayout()
        {
            if (Width <= 0 || Height <= 0) return;

            int totalHeight = 0;
            int maxContentWidth = Width - 20;

            foreach (Control c in _content.Controls)
            {
                if (c.Visible)
                {
                    int bottom = c.Location.Y + c.Height + c.Margin.Bottom;
                    if (bottom > totalHeight) totalHeight = bottom;
                }
            }

            totalHeight += 40; // bottom padding
            _content.Width = Width;
            _content.Height = Math.Max(Height, totalHeight);

            _maxScroll = Math.Max(0, totalHeight - Height);
            _scrollOffset = Math.Max(0, Math.Min(_maxScroll, _scrollOffset));

            ApplyScroll();
            Invalidate();
        }

        private void ApplyScroll()
        {
            _content.Top = -_scrollOffset;
            Invalidate();
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_MOUSEWHEEL && IsDisposed == false && Visible && _maxScroll > 0)
            {
                var screenPt = Cursor.Position;
                var clientPt = PointToClient(screenPt);

                if (ClientRectangle.Contains(clientPt))
                {
                    // Extract delta (high word of wParam)
                    int wParam = (int)(long)m.WParam;
                    int delta = (short)((wParam >> 16) & 0xFFFF);

                    int step = 48;
                    _scrollOffset -= Math.Sign(delta) * step;
                    _scrollOffset = Math.Max(0, Math.Min(_maxScroll, _scrollOffset));

                    ApplyScroll();
                    return true; // Message handled
                }
            }
            return false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool wasHovering = _isHoveringScrollbar;
            _isHoveringScrollbar = _maxScroll > 0 && e.X >= Width - HIT_AREA_WIDTH;

            if (_isDraggingScroll)
            {
                int trackHeight = Height - 20;
                int thumbHeight = Math.Max(36, (int)((float)Height / (_maxScroll + Height) * trackHeight));
                int availableTrack = trackHeight - thumbHeight;

                if (availableTrack > 0)
                {
                    float delta = e.Y - _dragStartY;
                    float scrollRatio = delta / availableTrack;
                    _scrollOffset = (int)(_dragStartOffset + scrollRatio * _maxScroll);
                    _scrollOffset = Math.Max(0, Math.Min(_maxScroll, _scrollOffset));
                    ApplyScroll();
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
            if (e.Button == MouseButtons.Left && _maxScroll > 0 && e.X >= Width - HIT_AREA_WIDTH)
            {
                _isDraggingScroll = true;
                _dragStartY = e.Y;
                _dragStartOffset = _scrollOffset;
                Capture = true;
                Invalidate();
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
            int thumbHeight = Math.Max(36, (int)((float)Height / (_maxScroll + Height) * trackHeight));
            int thumbY = 10 + (int)((float)_scrollOffset / _maxScroll * (trackHeight - thumbHeight));
            int thumbWidth = _isHoveringScrollbar || _isDraggingScroll ? 6 : 4;
            int thumbX = Width - thumbWidth - 4;

            var thumbRect = new Rectangle(thumbX, thumbY, thumbWidth, thumbHeight);

            // Sleek Iridescent / Neon Thumb
            Color thumbColor = _isDraggingScroll ? Color.FromArgb(220, 0, 245, 212) :
                               _isHoveringScrollbar ? Color.FromArgb(180, 139, 92, 246) :
                               Color.FromArgb(100, 100, 120, 160);

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
