using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FloatDock.Core;

namespace FloatDock.Windows;

internal static class WindowService
{
    public static nint Representative(nint foreground, IEnumerable<AppWindow> windows)
    {
        var represented = windows.Select(w => w.Handle).ToHashSet();
        var visited = new HashSet<nint>();
        for (var current = foreground; current != 0 && visited.Add(current); current = NativeMethods.GetWindow(current, 4))
            if (represented.Contains(current)) return current;
        return foreground;
    }

    public static IReadOnlyList<AppWindow> Enumerate()
    {
        var result = new List<AppWindow>();
        NativeMethods.EnumWindows((handle, _) =>
        {
            if (!NativeMethods.IsWindowVisible(handle) || handle == NativeMethods.GetShellWindow()) return true;
            var style = NativeMethods.GetWindowLong(handle, -20);
            if ((style & 0x80) != 0 || ((style & 0x40000) == 0 && NativeMethods.GetWindow(handle, 4) != 0)) return true;
            NativeMethods.GetWindowThreadProcessId(handle, out var pid);
            if (pid == Environment.ProcessId) return true;
            if (NativeMethods.DwmGetWindowAttribute(handle, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            var title = new StringBuilder(512);
            NativeMethods.GetWindowText(handle, title, title.Capacity);
            // An empty title is valid. Exclude desktop shell surfaces by class, not by caption text.
            var className = new StringBuilder(128);
            NativeMethods.GetClassName(handle, className, className.Capacity);
            if (className.ToString() is "WorkerW" or "Progman" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;
            string? path = null, processName = null;
            try { using var process = Process.GetProcessById((int)pid); processName = process.ProcessName; path = process.MainModule?.FileName; }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException) { }
            result.Add(new(handle, DockModel.WindowLabel(title.ToString(), processName), path));
            return true;
        }, 0);
        return result;
    }

    public static bool Activate(nint handle)
    {
        if (!NativeMethods.IsWindow(handle)) return false;
        if (NativeMethods.IsIconic(handle)) NativeMethods.ShowWindowAsync(handle, 9);
        var popup = NativeMethods.GetLastActivePopup(handle);
        if (popup != 0 && NativeMethods.IsWindowVisible(popup)) handle = popup;
        return NativeMethods.SetForegroundWindow(handle);
    }

    public static bool IsFullscreen(nint foreground, nint dock)
    {
        if (foreground == 0 || foreground == dock || foreground == NativeMethods.GetShellWindow() || NativeMethods.IsIconic(foreground)) return false;
        var className = new StringBuilder(128);
        NativeMethods.GetClassName(foreground, className, className.Capacity);
        if (className.ToString() is "WorkerW" or "Progman" or "Shell_TrayWnd") return false;
        var monitor = NativeMethods.MonitorFromWindow(foreground, 2);
        if (monitor != NativeMethods.MonitorFromWindow(dock, 2)) return false;
        var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info) || !NativeMethods.GetWindowRect(foreground, out var rect)) return false;
        var coversMonitor = rect.Left <= info.Monitor.Left && rect.Top <= info.Monitor.Top && rect.Right >= info.Monitor.Right && rect.Bottom >= info.Monitor.Bottom;
        var hasCaption = (NativeMethods.GetWindowLong(foreground, -16) & 0x00C00000) == 0x00C00000;
        return DockModel.ShouldHideForFullscreen(coversMonitor, NativeMethods.IsZoomed(foreground), hasCaption);
    }

    public static ImageSource Icon(DockEntry entry)
    {
        var handle = entry.Windows.FirstOrDefault()?.Handle ?? 0;
        if (handle != 0)
        {
            NativeMethods.SendMessageTimeout(handle, 0x7F, 1, 0, 2, 40, out var icon);
            if (icon != 0)
            {
                try { return CopyIcon(icon); }
                catch (ArgumentException) { }
            }
        }
        if (entry.Path != null)
        {
            NativeMethods.SHGetFileInfo(entry.Path, 0, out var info, (uint)Marshal.SizeOf<NativeMethods.ShellFileInfo>(), 0x100);
            if (info.Icon != 0)
            {
                try { return CopyIcon(info.Icon); }
                finally { NativeMethods.DestroyIcon(info.Icon); }
            }
        }
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(116, 164, 244)), null, new RectangleGeometry(new System.Windows.Rect(0, 0, 40, 40), 11, 11)));
        drawing.Children.Add(new GeometryDrawing(Brushes.White, null, Geometry.Parse("M11,12 L29,12 29,16 11,16 Z M11,20 L23,20 23,24 11,24 Z M11,28 L19,28 19,31 11,31 Z")));
        var fallback = new DrawingImage(drawing); fallback.Freeze(); return fallback;
    }

    private static BitmapSource CopyIcon(nint handle)
    {
        var bitmap = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        bitmap.Freeze(); return bitmap;
    }
}
