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
        public string Path { get; }

        public EntryType Type { get; }

        public string Name => Type == EntryType.Folder 
            ? System.IO.Path.GetFileName(Path) 
            : System.IO.Path.GetFileNameWithoutExtension(Path);

        private Icon _cachedIcon;

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

        public Icon ExtractIcon(ThumbnailProvider thumbnailProvider)
        {
            if (Type == EntryType.Folder)
            {
                return IconUtil.FolderLarge;
            }

            if (thumbnailProvider != null && thumbnailProvider.IsSupported(Path))
            {
                return thumbnailProvider.GenerateThumbnail(Path);
            }

            // Cache standard file icons to prevent disk/registry I/O on every frame
            if (_cachedIcon == null)
            {
                try
                {
                    if (File.Exists(Path))
                        _cachedIcon = Icon.ExtractAssociatedIcon(Path);
                }
                catch
                {
                    // Fallback
                }
            }

            return _cachedIcon;
        }

        public void InvalidateIcon()
        {
            _cachedIcon = null;
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
