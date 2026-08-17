using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using NoFences.Model;

namespace NoFences.UI
{
    /// <summary>
    /// High-performance GDI+ rendering engine for Cyber-Glass & Iridescent Bubble theme.
    /// Handles frosted glass backgrounds, specular highlights, iridescent aura borders,
    /// and floating glass bubble cards.
    /// </summary>
    public static class CyberGlassRenderer
    {
        // ── Iridescent Color Palette ──────────────────────────────────────────
        public static readonly Color IridescentCyan    = Color.FromArgb(0, 245, 212);   // #00F5D4
        public static readonly Color IridescentViolet  = Color.FromArgb(139, 92, 246);  // #8B5CF6
        public static readonly Color IridescentPink    = Color.FromArgb(244, 63, 94);   // #F43F5E
        public static readonly Color GlassDarkBg       = Color.FromArgb(215, 13, 16, 24); // Frosted Obsidian Glass
        public static readonly Color GlassHeaderBg     = Color.FromArgb(160, 20, 24, 38);
        public static readonly Color BubbleDefaultFill = Color.FromArgb(14, 255, 255, 255);
        public static readonly Color BubbleHoverFill   = Color.FromArgb(42, 255, 255, 255);
        public static readonly Color BubbleSelectFill  = Color.FromArgb(65, 0, 229, 255);
        public static readonly Color SpecularTopLight  = Color.FromArgb(70, 255, 255, 255);

        /// <summary>
        /// Creates a GraphicsPath with rounded corners for the given rectangle.
        /// </summary>
        public static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int cornerRadius)
        {
            var path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0) return path;

            int d = Math.Min(cornerRadius * 2, Math.Min(rect.Width, rect.Height));
            if (d <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            var arc = new Rectangle(rect.X, rect.Y, d, d);

            // Top-left
            path.AddArc(arc, 180, 90);
            // Top-right
            arc.X = rect.Right - d;
            path.AddArc(arc, 270, 90);
            // Bottom-right
            arc.Y = rect.Bottom - d;
            path.AddArc(arc, 0, 90);
            // Bottom-left
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Creates a LinearGradientBrush with iridescent cyber colors diagonally across bounds.
        /// </summary>
        public static LinearGradientBrush CreateIridescentBrush(Rectangle bounds, float angle = 45f)
        {
            var safeBounds = new Rectangle(
                bounds.X, bounds.Y,
                Math.Max(10, bounds.Width),
                Math.Max(10, bounds.Height)
            );

            var brush = new LinearGradientBrush(safeBounds, IridescentCyan, IridescentPink, angle);
            var colorBlend = new ColorBlend(4)
            {
                Colors = new[] { IridescentCyan, IridescentViolet, IridescentPink, IridescentCyan },
                Positions = new[] { 0.0f, 0.45f, 0.85f, 1.0f }
            };
            brush.InterpolationColors = colorBlend;
            return brush;
        }

        /// <summary>
        /// Renders the complete Cyber-Glass body, specular highlight, and iridescent border.
        /// </summary>
        public static void RenderCyberGlassContainer(Graphics g, Rectangle bounds, bool isHovered, bool isDragActive, int radius = 10)
        {
            if (bounds.Width <= 4 || bounds.Height <= 4) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            var containerRect = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

            using (var path = CreateRoundedRectanglePath(containerRect, radius))
            {
                // 1. Frosted Glass Fill
                using (var bgBrush = new SolidBrush(GlassDarkBg))
                {
                    g.FillPath(bgBrush, path);
                }

                // 2. Top Specular Reflection (Tempered Glass Edge)
                using (var specBrush = new LinearGradientBrush(
                    new Point(containerRect.X, containerRect.Y),
                    new Point(containerRect.Right, containerRect.Y),
                    SpecularTopLight,
                    Color.FromArgb(10, 255, 255, 255)))
                using (var specPen = new Pen(specBrush, 1.2f))
                {
                    g.DrawLine(specPen, containerRect.X + radius, containerRect.Y + 1, containerRect.Right - radius, containerRect.Y + 1);
                }

                // 3. Iridescent Outer Glow & Border
                using (var iriBrush = CreateIridescentBrush(containerRect, 35f))
                {
                    if (isDragActive)
                    {
                        // Pulsing Magnetic Drag Aura
                        using (var auraPen = new Pen(Color.FromArgb(140, IridescentCyan), 3.5f))
                        {
                            g.DrawPath(auraPen, path);
                        }
                        using (var borderPen = new Pen(iriBrush, 1.8f))
                        {
                            g.DrawPath(borderPen, path);
                        }
                    }
                    else if (isHovered)
                    {
                        // Hover Active Glow
                        using (var auraPen = new Pen(Color.FromArgb(70, IridescentViolet), 2.5f))
                        {
                            g.DrawPath(auraPen, path);
                        }
                        using (var borderPen = new Pen(iriBrush, 1.5f))
                        {
                            g.DrawPath(borderPen, path);
                        }
                    }
                    else
                    {
                        // Subtle Resting Iridescent Edge
                        using (var borderPen = new Pen(Color.FromArgb(120, IridescentViolet), 1.0f))
                        {
                            g.DrawPath(borderPen, path);
                        }
                    }
                }
            }
        }

        private static readonly SolidBrush _brushBubbleDefault = new SolidBrush(BubbleDefaultFill);
        private static readonly SolidBrush _brushBubbleHover   = new SolidBrush(BubbleHoverFill);
        private static readonly SolidBrush _brushBubbleSelect  = new SolidBrush(BubbleSelectFill);
        private static readonly SolidBrush _brushGlassDarkBg   = new SolidBrush(GlassDarkBg);
        private static readonly Pen _penNormalBorder           = new Pen(Color.FromArgb(28, 255, 255, 255), 1.0f);
        private static readonly Pen _penSelectBorder           = new Pen(IridescentCyan, 1.4f);

        /// <summary>
        /// Renders a modern floating glass capsule (bubble) for a single file/folder entry.
        /// </summary>
        public static void RenderBubbleCard(Graphics g, Rectangle rect, bool isHovered, bool isSelected, int radius = 8)
        {
            if (rect.Width <= 2 || rect.Height <= 2) return;

            using (var path = CreateRoundedRectanglePath(rect, radius))
            {
                if (isSelected)
                {
                    g.FillPath(_brushBubbleSelect, path);
                    g.DrawPath(_penSelectBorder, path);
                }
                else if (isHovered)
                {
                    g.FillPath(_brushBubbleHover, path);
                    using (var iriBrush = CreateIridescentBrush(rect, 45f))
                    using (var pen = new Pen(iriBrush, 1.2f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
                else
                {
                    g.FillPath(_brushBubbleDefault, path);
                    g.DrawPath(_penNormalBorder, path);
                }
            }
        }

        /// <summary>
        /// Renders a stylish glass pill badge (e.g. for item counters [ 8 items ]).
        /// </summary>
        public static void RenderPillBadge(Graphics g, Rectangle rect, string text, Font font, Color textColor)
        {
            if (rect.Width <= 2 || rect.Height <= 2) return;

            int radius = rect.Height / 2;
            using (var path = CreateRoundedRectanglePath(rect, radius))
            using (var fillBrush = new SolidBrush(Color.FromArgb(35, 255, 255, 255)))
            using (var borderPen = new Pen(Color.FromArgb(60, IridescentCyan), 1.0f))
            using (var textBrush = new SolidBrush(textColor))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
                g.DrawString(text, font, textBrush, rect, sf);
            }
        }
    }
}
