using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NoFences.Win32;

namespace NoFences.Util
{
    public class ThumbnailProvider
    {
        // Supported .NET images as per https://docs.microsoft.com/en-us/dotnet/api/system.drawing.image.fromfile
        private static readonly string[] SupportedExtensions =
        {
            ".bmp",
            ".gif",
            ".jpg",
            ".jpeg",
            ".png",
            ".tiff",
            ".tif"
        };

        private class ThumbnailState
        {
            public Icon icon;
        }

        // Only allow 4 concurrent images to be decoded to try and prevent OOM errors
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(4, 4);
        private readonly ConcurrentDictionary<string, ThumbnailState> iconCache = new ConcurrentDictionary<string, ThumbnailState>(StringComparer.OrdinalIgnoreCase);
        public event EventHandler IconThumbnailLoaded;

        public bool IsSupported(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return SupportedExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        public Icon GenerateThumbnail(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (iconCache.TryGetValue(path, out var state))
            {
                return state.icon;
            }

            return SubmitGeneratorTask(path).icon;
        }

        private ThumbnailState SubmitGeneratorTask(string path)
        {
            Icon fallbackIcon = null;
            try
            {
                if (File.Exists(path))
                    fallbackIcon = Icon.ExtractAssociatedIcon(path);
            }
            catch
            {
                // Ignore fallback extraction errors
            }

            var state = new ThumbnailState() { icon = fallbackIcon };
            iconCache[path] = state;

            Task.Run(async () =>
            {
                bool acquired = false;
                try
                {
                    await semaphore.WaitAsync();
                    acquired = true;

                    if (!File.Exists(path)) return;

                    byte[] fileBytes;
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var ms = new MemoryStream())
                    {
                        await fs.CopyToAsync(ms);
                        fileBytes = ms.ToArray();
                    }

                    using (var ms = new MemoryStream(fileBytes))
                    using (var img = Image.FromStream(ms))
                    {
                        using (var thumb = (Bitmap)img.GetThumbnailImage(32, 32, () => false, IntPtr.Zero))
                        {
                            IntPtr hIcon = thumb.GetHicon();
                            try
                            {
                                var icon = (Icon)Icon.FromHandle(hIcon).Clone();
                                var oldIcon = state.icon;
                                state.icon = icon;

                                // Dispose previous icon if it was our custom clone
                                if (oldIcon != null && oldIcon != fallbackIcon)
                                {
                                    oldIcon.Dispose();
                                }

                                IconThumbnailLoaded?.Invoke(this, EventArgs.Empty);
                            }
                            finally
                            {
                                if (hIcon != IntPtr.Zero)
                                {
                                    IconUtil.DestroyIcon(hIcon);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Fail silently to keep fallback icon
                }
                finally
                {
                    if (acquired)
                    {
                        semaphore.Release();
                    }
                }
            });

            return state;
        }
    }
}
