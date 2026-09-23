using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FloatDock.Windows.Taskbar;

internal sealed class DesktopPlacement : IDisposable
{
    internal static readonly uint Callback = TaskbarNative.RegisterWindowMessage("FloatDock.AppBar.Position");
    private readonly nint _window;
    private readonly TaskbarGuard _guard;
    private bool _disposed, _placing;
    private NativeMethods.Rect _lastBounds;
    internal Rect Reserved { get; private set; }
    internal DesktopPlacement(nint window)
    {
        _window = window; _guard = new TaskbarGuard(window);
        var data = TaskbarNative.Data(window); data.Callback = Callback;
        if (TaskbarNative.SHAppBarMessage(0, ref data) == 0) { _guard.Dispose(); throw new InvalidOperationException("Windows could not reserve the dock area."); }
    }
    internal Rect Position(Window dock, double height)
    {
        if (_placing || _disposed) return Reserved;
        _placing = true;
        try
        {
            var monitor = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
            // A zero HWND with MONITOR_DEFAULTTOPRIMARY always selects the primary monitor.
            NativeMethods.GetMonitorInfo(NativeMethods.MonitorFromWindow(0, 1), ref monitor);
            var source = HwndSource.FromHwnd(_window);
            var scale = source.CompositionTarget.TransformToDevice.M22;
            var pixels = (int)Math.Ceiling(height * scale);
            var data = TaskbarNative.Data(_window); data.Edge = 3; data.Bounds = monitor.Monitor;
            TaskbarNative.SHAppBarMessage(2, ref data);
            data.Bounds.Top = data.Bounds.Bottom - pixels;
            if (data.Bounds.Left != _lastBounds.Left || data.Bounds.Top != _lastBounds.Top || data.Bounds.Right != _lastBounds.Right || data.Bounds.Bottom != _lastBounds.Bottom)
            { TaskbarNative.SHAppBarMessage(3, ref data); _lastBounds = data.Bounds; }
            var transform = source.CompositionTarget.TransformFromDevice;
            var topLeft = transform.Transform(new Point(data.Bounds.Left, data.Bounds.Top));
            var bottomRight = transform.Transform(new Point(data.Bounds.Right, data.Bounds.Bottom));
            Reserved = new Rect(topLeft, bottomRight); return Reserved;
        }
        finally { _placing = false; }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; TaskbarNative.Remove(_window); _guard.Dispose(); }
}
