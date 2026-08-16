using System;
using System.Runtime.InteropServices;

namespace NoFences.Win32
{
    public class DesktopUtil
    {
        private const int GWL_STYLE = -16;
        private const int GWL_HWNDPARENT = -8;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_MINIMIZEBOX = 0x00020000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpWindowClass, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        public static void PreventMinimize(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            IntPtr currentStyle = WindowUtil.GetWindowLong(handle, GWL_STYLE);
            long style = currentStyle.ToInt64();
            style &= ~WS_MAXIMIZEBOX;
            style &= ~WS_MINIMIZEBOX;
            WindowUtil.SetWindowLong(handle, GWL_STYLE, new IntPtr(style));
        }

        public static void GlueToDesktop(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            IntPtr nWinHandle = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
            if (nWinHandle != IntPtr.Zero)
            {
                WindowUtil.SetWindowLong(handle, GWL_HWNDPARENT, nWinHandle);
            }
        }
    }
}