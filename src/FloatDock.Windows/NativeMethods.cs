using System.Runtime.InteropServices;
using System.Text;

namespace FloatDock.Windows;

internal static class NativeMethods
{
    internal delegate bool EnumWindowsProc(nint handle, nint parameter);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint handle);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint handle);
    [DllImport("user32.dll")] internal static extern bool IsZoomed(nint handle);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern nint GetShellWindow();
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint handle, uint command);
    [DllImport("user32.dll")] internal static extern nint GetLastActivePopup(nint handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(nint handle, StringBuilder text, int capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(nint handle, StringBuilder text, int capacity);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] internal static extern int GetWindowLong(nint handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] internal static extern int SetWindowLong(nint handle, int index, int value);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint handle, out uint processId);
    [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(nint handle, int command);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint handle);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint handle, out Rect rect);
    [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint handle, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint handle, int attribute, out int value, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint SendMessageTimeout(nint handle, uint message, nint wParam, nint lParam, uint flags, uint timeout, out nint result);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(nint icon);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern nint SHGetFileInfo(string path, uint attributes, out ShellFileInfo info, uint size, uint flags);

    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct ShellFileInfo
    {
        public nint Icon; public int IconIndex; public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
}
