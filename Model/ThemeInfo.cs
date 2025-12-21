using System;
using System.Drawing;

namespace NoFences.Model
{
    public class ThemeInfo
    {
        public string Name { get; set; }
        public int TitleColor { get; set; }
        public int BackgroundColor { get; set; }
        public int TitleTextColor { get; set; }
        public bool ShowHeader { get; set; } = true;
        public int TitleAlignment { get; set; } = 1; // 0=Left, 1=Center, 2=Right
        public bool ChameleonMode { get; set; } = false;
        public int IconSize { get; set; } = 32;

        public ThemeInfo() { }

        public ThemeInfo(string name)
        {
            Name = name;
        }

        // Predefined themes
        public static ThemeInfo Light => new ThemeInfo("Light")
        {
            TitleColor = Color.FromArgb(230, 255, 255, 255).ToArgb(),
            BackgroundColor = Color.FromArgb(250, 255, 255, 255).ToArgb(),
            TitleTextColor = Color.FromArgb(50, 50, 50).ToArgb(),
            ShowHeader = true,
            ChameleonMode = false
        };

        public static ThemeInfo Dark => new ThemeInfo("Dark")
        {
            TitleColor = Color.FromArgb(200, 30, 30, 30).ToArgb(),
            BackgroundColor = Color.FromArgb(220, 20, 20, 20).ToArgb(),
            TitleTextColor = Color.FromArgb(240, 240, 240).ToArgb(),
            ShowHeader = true,
            ChameleonMode = false
        };

        public static ThemeInfo Glass => new ThemeInfo("Glass")
        {
            TitleColor = Color.FromArgb(100, 255, 255, 255).ToArgb(),
            BackgroundColor = Color.FromArgb(120, 255, 255, 255).ToArgb(),
            TitleTextColor = Color.White.ToArgb(),
            ShowHeader = true,
            ChameleonMode = false
        };

        public static ThemeInfo Minimal => new ThemeInfo("Minimal")
        {
            TitleColor = Color.FromArgb(50, Color.Black).ToArgb(),
            BackgroundColor = Color.FromArgb(100, Color.Black).ToArgb(),
            TitleTextColor = Color.White.ToArgb(),
            ShowHeader = false,
            ChameleonMode = true
        };

        public static ThemeInfo[] GetPredefinedThemes()
        {
            return new[] { Light, Dark, Glass, Minimal };
        }

        /// <summary>
        /// Apply this theme to a FenceInfo
        /// </summary>
        public void ApplyTo(FenceInfo fence)
        {
            fence.TitleColor = TitleColor;
            fence.BackgroundColor = BackgroundColor;
            fence.TitleTextColor = TitleTextColor;
            fence.ShowHeader = ShowHeader;
            fence.TitleAlignment = TitleAlignment;
            fence.ChameleonMode = ChameleonMode;
            fence.IconSize = IconSize > 0 ? IconSize : fence.IconSize;
        }
    }
}
