using System.Drawing;
using System.Windows.Forms;

namespace NoFences.UI
{
    public class FenceMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly Color backgroundColor = Color.FromArgb(240, 30, 30, 30); // Dark background
        private readonly Color textColor = Color.White;
        private readonly Color selectionColor = Color.FromArgb(100, 100, 100, 100); // Semi-transparent selection
        private readonly Color separatorColor = Color.FromArgb(80, 255, 255, 255);

        public FenceMenuRenderer() : base(new FenceColorTable())
        {
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(backgroundColor), e.AffectedBounds);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // Do not render the default light image margin
            e.Graphics.FillRectangle(new SolidBrush(backgroundColor), e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                e.Graphics.FillRectangle(new SolidBrush(selectionColor), new Rectangle(Point.Empty, e.Item.Size));
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
            e.Graphics.FillRectangle(new SolidBrush(separatorColor), new Rectangle(30, 3, e.Item.Width - 35, 1));
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
