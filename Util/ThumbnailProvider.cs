using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using NoFences.Win32;

namespace NoFences.Util
{
    /// <summary>
    /// Builds icon-sized thumbnails for image files. Synchronous: it is meant to run on the
    /// IconCache loader thread, never on the UI thread.
    /// </summary>
    public class ThumbnailProvider
    {
        // Supported .NET images as per https://docs.microsoft.com/en-us/dotnet/api/system.drawing.image.fromfile
        private static readonly string[] SupportedExtensions =
        {
            ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".tiff", ".tif"
        };

        // Large enough for the biggest icon size (64px) without upscaling blur
        private const int ThumbSize = 64;

        public bool IsSupported(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var ext in SupportedExtensions)
            {
                if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Returns a 64x64 thumbnail icon (aspect ratio preserved), or null on failure.</summary>
        public Icon LoadThumbnail(string path)
        {
            if (!File.Exists(path)) return null;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var img = Image.FromStream(fs, false, false))
            using (var thumb = new Bitmap(ThumbSize, ThumbSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                float scale = Math.Min((float)ThumbSize / img.Width, (float)ThumbSize / img.Height);
                int w = Math.Max(1, (int)(img.Width * scale));
                int h = Math.Max(1, (int)(img.Height * scale));

                using (var g = Graphics.FromImage(thumb))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.DrawImage(img, (ThumbSize - w) / 2, (ThumbSize - h) / 2, w, h);
                }

                IntPtr hIcon = thumb.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(hIcon).Clone();
                }
                finally
                {
                    IconUtil.DestroyIcon(hIcon);
                }
            }
        }
    }
}
