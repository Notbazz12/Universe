using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NoFences.UI
{
    /// <summary>
    /// A sleek, modern Cyber-Glass dropdown selector that replaces ugly WinForms ComboBoxes.
    /// </summary>
    public class CyberComboBox : ComboBox
    {
        private bool isHovered = false;

        public CyberComboBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            BackColor = Color.FromArgb(20, 24, 38);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            ItemHeight = 26;
            FlatStyle = FlatStyle.Flat;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            isHovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            
            Color itemBg = isSelected ? Color.FromArgb(42, 48, 75) : Color.FromArgb(20, 24, 38);
            Color itemText = isSelected ? Color.FromArgb(0, 245, 212) : Color.FromArgb(220, 226, 240);

            using (var b = new SolidBrush(itemBg))
            {
                g.FillRectangle(b, e.Bounds);
            }

            if (isSelected)
            {
                using (var indBrush = new SolidBrush(Color.FromArgb(0, 245, 212)))
                {
                    g.FillRectangle(indBrush, e.Bounds.X, e.Bounds.Y + 4, 3, e.Bounds.Height - 8);
                }
            }

            string text = Items[e.Index]?.ToString() ?? "";
            using (var font = new Font(Font.FontFamily, 9.5f, isSelected ? FontStyle.Bold : FontStyle.Regular))
            using (var textBrush = new SolidBrush(itemText))
            {
                var textRect = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height);
                var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                g.DrawString(text, font, textBrush, textRect, sf);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Container Background
            Color bg = isHovered || Focused ? Color.FromArgb(28, 33, 52) : Color.FromArgb(20, 24, 38);
            using (var bgBrush = new SolidBrush(bg))
            using (var path = CreateRoundRect(rect, 6))
            {
                g.FillPath(bgBrush, path);
            }

            // Border
            if (Focused || isHovered)
            {
                using (var brush = new LinearGradientBrush(rect, Color.FromArgb(0, 245, 212), Color.FromArgb(139, 92, 246), 0f))
                using (var pen = new Pen(brush, 1.2f))
                using (var path = CreateRoundRect(rect, 6))
                {
                    g.DrawPath(pen, path);
                }
            }
            else
            {
                using (var pen = new Pen(Color.FromArgb(48, 56, 85), 1f))
                using (var path = CreateRoundRect(rect, 6))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Text
            string selectedText = SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex]?.ToString() : Text;
            if (!string.IsNullOrEmpty(selectedText))
            {
                using (var font = new Font("Segoe UI", 9.5f))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    var textRect = new Rectangle(12, 0, Width - 36, Height);
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                    g.DrawString(selectedText, font, textBrush, textRect, sf);
                }
            }

            // Custom Modern Arrow
            int arrowX = Width - 20;
            int arrowY = Height / 2 - 1;
            Point[] arrowPoints = new Point[]
            {
                new Point(arrowX, arrowY),
                new Point(arrowX + 8, arrowY),
                new Point(arrowX + 4, arrowY + 5)
            };

            using (var arrowBrush = new SolidBrush(isHovered || Focused ? Color.FromArgb(0, 245, 212) : Color.FromArgb(140, 150, 180)))
            {
                g.FillPolygon(arrowBrush, arrowPoints);
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
