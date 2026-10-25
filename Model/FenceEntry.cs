using System.Drawing;
using System.Threading.Tasks;
using System.Diagnostics;
using System;
using System.IO;
using NoFences.Win32;
using NoFences.Util;

namespace NoFences.Model
{
    public class FenceEntry
    {
        private static readonly ThumbnailProvider Thumbnails = new ThumbnailProvider();

        public string Path { get; }

        public EntryType Type { get; }

        public string Name => Type == EntryType.Folder
            ? System.IO.Path.GetFileName(Path)
            : System.IO.Path.GetFileNameWithoutExtension(Path);

        /// <summary>Measured name height, cached by the window (depends on item width and font).</summary>
        internal int TextHeight = -1;
        internal int TextHeightKey;

        private FenceEntry(string path, EntryType type)
        {
            Path = path;
            Type = type;
        }

        public static FenceEntry FromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (File.Exists(path))
                return new FenceEntry(path, EntryType.File);
            else if (Directory.Exists(path))
                return new FenceEntry(path, EntryType.Folder);
            else return null;
        }

        /// <summary>
        /// Returns the icon if it is already loaded, otherwise a placeholder; the real icon is
        /// loaded on a background thread and IconCache.Loaded fires when it is ready.
        /// Never blocks the caller.
        /// </summary>
        public Icon ExtractIcon()
        {
            if (Type == EntryType.Folder)
                return IconUtil.FolderLarge;

            return IconCache.Shared.GetOrQueue(Path, LoadIconCore, IconUtil.FileLarge);
        }

        /// <summary>Runs on the IconCache loader thread.</summary>
        private Icon LoadIconCore()
        {
            if (Thumbnails.IsSupported(Path))
            {
                try
                {
                    var thumb = Thumbnails.LoadThumbnail(Path);
                    if (thumb != null) return thumb;
                }
                catch { }
            }

            // Extract high-res icon from .url (Steam / Epic) or .lnk shortcuts
            if (Path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            {
                var shortcut = ShellLinkHelper.Resolve(Path);
                if (shortcut != null && !string.IsNullOrEmpty(shortcut.IconPath) && File.Exists(shortcut.IconPath))
                {
                    try
                    {
                        var icon = Icon.ExtractAssociatedIcon(shortcut.IconPath);
                        if (icon != null) return icon;
                    }
                    catch { }
                }
            }
            else if (Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var shortcut = ShellLinkHelper.Resolve(Path);
                if (shortcut != null)
                {
                    if (!string.IsNullOrEmpty(shortcut.IconPath) && File.Exists(shortcut.IconPath))
                    {
                        try
                        {
                            var icon = Icon.ExtractAssociatedIcon(shortcut.IconPath);
                            if (icon != null) return icon;
                        }
                        catch { }
                    }
                    if (!string.IsNullOrEmpty(shortcut.TargetPath) && File.Exists(shortcut.TargetPath))
                    {
                        try
                        {
                            var icon = Icon.ExtractAssociatedIcon(shortcut.TargetPath);
                            if (icon != null) return icon;
                        }
                        catch { }
                    }
                }
            }

            try
            {
                if (File.Exists(Path))
                    return Icon.ExtractAssociatedIcon(Path);
            }
            catch { }

            return IconUtil.FileLarge;
        }

        public void InvalidateIcon()
        {
            IconCache.Shared.Invalidate(Path);
        }

        public void Open()
        {
            Task.Run(() =>
            {
                try
                {
                    if (Type == EntryType.File && File.Exists(Path))
                        Process.Start(new ProcessStartInfo(Path) { UseShellExecute = true });
                    else if (Type == EntryType.Folder && Directory.Exists(Path))
                        Process.Start("explorer.exe", Path);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to start: {e}");
                }
            });
        }
    }
}
