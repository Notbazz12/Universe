using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace NoFences.Win32
{
    /// <summary>
    /// Lightweight native helper to resolve Windows Shortcuts (.lnk) and Internet Shortcuts (.url).
    /// </summary>
    public static class ShellLinkHelper
    {
        public class ShortcutInfo
        {
            public string TargetPath { get; set; }
            public string Arguments { get; set; }
            public string WorkingDirectory { get; set; }
            public string IconPath { get; set; }
            public int IconIndex { get; set; }
            public string Url { get; set; }
        }

        public static ShortcutInfo Resolve(string shortcutPath)
        {
            if (string.IsNullOrEmpty(shortcutPath) || !File.Exists(shortcutPath))
                return null;

            string ext = Path.GetExtension(shortcutPath).ToLowerInvariant();

            if (ext == ".url")
            {
                return ResolveUrlShortcut(shortcutPath);
            }

            if (ext == ".lnk")
            {
                return ResolveLnkShortcut(shortcutPath);
            }

            return null;
        }

        private static ShortcutInfo ResolveUrlShortcut(string urlFilePath)
        {
            try
            {
                var info = new ShortcutInfo();
                string[] lines = File.ReadAllLines(urlFilePath);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    {
                        info.Url = trimmed.Substring(4).Trim();
                    }
                    else if (trimmed.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                    {
                        info.IconPath = trimmed.Substring(9).Trim();
                    }
                    else if (trimmed.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(trimmed.Substring(10).Trim(), out int idx))
                            info.IconIndex = idx;
                    }
                }
                return info;
            }
            catch
            {
                return null;
            }
        }

        private static ShortcutInfo ResolveLnkShortcut(string lnkFilePath)
        {
            try
            {
                var link = (IShellLinkW)new ShellLink();
                var file = (IPersistFile)link;
                file.Load(lnkFilePath, 0); // STGM_READ

                var targetBuilder = new StringBuilder(260);
                var findData = new WIN32_FIND_DATAW();
                link.GetPath(targetBuilder, targetBuilder.Capacity, out findData, 2); // SLGP_UNCPRIORITY

                var argsBuilder = new StringBuilder(1024);
                link.GetArguments(argsBuilder, argsBuilder.Capacity);

                var dirBuilder = new StringBuilder(260);
                link.GetWorkingDirectory(dirBuilder, dirBuilder.Capacity);

                var iconBuilder = new StringBuilder(260);
                int iconIndex = 0;
                link.GetIconLocation(iconBuilder, iconBuilder.Capacity, out iconIndex);

                return new ShortcutInfo
                {
                    TargetPath = targetBuilder.ToString(),
                    Arguments = argsBuilder.ToString(),
                    WorkingDirectory = dirBuilder.ToString(),
                    IconPath = iconBuilder.ToString(),
                    IconIndex = iconIndex
                };
            }
            catch
            {
                return null;
            }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] string ppszFileName);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WIN32_FIND_DATAW
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }
    }
}
