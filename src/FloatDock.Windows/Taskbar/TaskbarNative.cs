using System.Runtime.InteropServices;

namespace FloatDock.Windows.Taskbar;

internal static class TaskbarNative
{
    [StructLayout(LayoutKind.Sequential)] internal struct AppBarData
    { public uint Size; public nint Window; public uint Callback, Edge; public NativeMethods.Rect Bounds; public nint Parameter; }
    [DllImport("shell32.dll")] internal static extern nuint SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint FindWindow(string className, string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint window, int id);
    internal static AppBarData Data(nint window) => new() { Size = (uint)Marshal.SizeOf<AppBarData>(), Window = window };
    internal static uint State()
    { var data = Data(FindWindow("Shell_TrayWnd", null)); return (uint)SHAppBarMessage(4, ref data); }
    internal static void AutoHide(bool enabled)
    { var data = Data(FindWindow("Shell_TrayWnd", null)); data.Parameter = (nint)(enabled ? State() | 1u : State() & ~1u); SHAppBarMessage(10, ref data); }
    internal static void Remove(nint window) { var data = Data(window); SHAppBarMessage(1, ref data); }
}
