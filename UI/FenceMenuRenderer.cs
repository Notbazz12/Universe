using System.Drawing;
using System.Windows.Forms;

namespace NoFences.UI
{
    public class FenceMenuRenderer : ToolStripProfessionalRenderer
    {
        private static readonly Color backgroundColor = Color.FromArgb(240, 30, 30, 30);
        private static readonly Color textColor = Color.White;
        private static readonly Color selectionColor = Color.FromArgb(100, 100, 100, 100);
        private static readonly Color separatorColor = Color.FromArgb(80, 255, 255, 255);

        // Cached brushes to prevent allocating GDI objects on every menu item paint
        private static readonly SolidBrush bgBrush = new SolidBrush(backgroundColor);
        private static readonly SolidBrush selectionBrush = new SolidBrush(selectionColor);
        private static readonly SolidBrush separatorBrush = new SolidBrush(separatorColor);

        public FenceMenuRenderer() : base(new FenceColorTable())
        {
        }

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
            if (e.Item.Selected)
            {
                e.Graphics.FillRectangle(selectionBrush, new Rectangle(Point.Empty, e.Item.Size));
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
        public override Color MenuBorder => Color.FromArgb(100, 100, 100);
        public override Color ToolStripDropDownBackground => Color.FromArgb(30, 30, 30);
        public override Color ImageMarginGradientBegin => Color.FromArgb(30, 30, 30);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 30, 30);
        public override Color ImageMarginGradientEnd => Color.FromArgb(30, 30, 30);
    }
}
