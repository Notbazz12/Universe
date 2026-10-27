using System.Drawing;
using System.Windows.Forms;

namespace NoFences.UI
{
    public class FenceMenuRenderer : ToolStripProfessionalRenderer
    {
        private static readonly Color backgroundColor = Color.FromArgb(23, 26, 40); // settings card colour
        private static readonly Color textColor = Color.White;
        private static readonly Color selectionColor = Color.FromArgb(70, 139, 92, 246); // violet accent tint
        private static readonly Color separatorColor = Color.FromArgb(40, 255, 255, 255);

        // Cached brushes to prevent allocating GDI objects on every menu item paint
        private static readonly SolidBrush bgBrush = new SolidBrush(backgroundColor);
        private static readonly SolidBrush selectionBrush = new SolidBrush(selectionColor);
        private static readonly SolidBrush separatorBrush = new SolidBrush(separatorColor);

        public FenceMenuRenderer() : base(new FenceColorTable())
        {
            RoundedEdges = false;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Image> glyphCache = new System.Collections.Generic.Dictionary<string, Image>();
        private static readonly Font glyphFont = new Font("Segoe MDL2 Assets", 10f);

        /// <summary>
        /// Menu icon drawn from a Segoe MDL2 Assets glyph (emoji in menu text render inconsistently).
        /// </summary>
        public static Image Glyph(string glyph)
        {
            if (glyphCache.TryGetValue(glyph, out var img)) return img;
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.DrawString(glyph, glyphFont, glyphBrush, new RectangleF(0, 0, 16, 16), sf);
            }
            glyphCache[glyph] = bmp;
            return bmp;
        }

        private static readonly SolidBrush glyphBrush = new SolidBrush(Color.FromArgb(0, 245, 212));

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.FillRectangle(bgBrush, e.AffectedBounds);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            e.Graphics.FillRectangle(bgBrush, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected && e.Item.Enabled)
            {
                e.Graphics.FillRectangle(selectionBrush, new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2));
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = textColor;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            e.Graphics.FillRectangle(separatorBrush, new Rectangle(30, 3, e.Item.Width - 35, 1));
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = textColor;
            base.OnRenderArrow(e);
        }
    }

    public class FenceColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(60, 255, 255, 255);
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuBorder => Color.FromArgb(70, 80, 115);
        public override Color ToolStripDropDownBackground => Color.FromArgb(23, 26, 40);
        public override Color ImageMarginGradientBegin => Color.FromArgb(23, 26, 40);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(23, 26, 40);
        public override Color ImageMarginGradientEnd => Color.FromArgb(23, 26, 40);
    }
}
