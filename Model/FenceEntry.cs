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
            if (_cachedIcon != null)
                return _cachedIcon;

            if (Type == EntryType.Folder)
            {
                _cachedIcon = IconUtil.FolderLarge;
                return _cachedIcon;
            }

            if (thumbnailProvider != null && thumbnailProvider.IsSupported(Path))
            {
                try
                {
                    _cachedIcon = thumbnailProvider.GenerateThumbnail(Path);
                    if (_cachedIcon != null) return _cachedIcon;
                }
                catch { }
            }

            try
            {
                if (File.Exists(Path))
                    _cachedIcon = Icon.ExtractAssociatedIcon(Path);
            }
            catch
            {
                _cachedIcon = IconUtil.FileLarge;
            }

            return _cachedIcon ?? IconUtil.FileLarge;
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
