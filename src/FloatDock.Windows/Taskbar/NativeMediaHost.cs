// Taskbar child-window hosting is adapted from AF Media Bar's TaskbarDockService,
// itself ported from ManualDinosaur/FluentFlyout (GPL-3.0-or-later).
// Copyright (c) 2026 AmorFate and the FluentFlyout contributors. See THIRD-PARTY-NOTICES.md.
using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FloatDock.Core;
using FloatDock.Windows.Media;

namespace FloatDock.Windows.Taskbar;

internal sealed class NativeMediaHost : Window
{
    private readonly MediaCapsule _media;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private nint _handle, _parent;
    private int _originalStyle;
    private bool _closed, _attached;
    private Task<ProbeResult>? _probe;
    private ProbeResult? _lastProbe;
    private DateTime _nextProbe;
    private TaskbarMotionState _motion;
    private TaskbarRange _range;
    private sealed record ProbeResult(nint Parent, PixelRect Bar, IReadOnlyList<TaskbarRange> Occupied, bool Valid, DateTime At);
    internal event Action<string>? StatusChanged;
    private string _status = "";

    internal NativeMediaHost(DockSettings settings, Action showSettings)
    {
        Title = "FloatDock Media"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowActivated = false; ShowInTaskbar = false;
        Width = 100; Height = 36; Opacity = 0;
        _media = new(settings, () => {
            try { SettingsStore.Save(settings); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SetStatus("设置保存失败：" + ex.Message); }
        }) { Height = 32, Padding = new(2), VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_media);
        var settingsButton = DockChrome.Button("\uE713", "打开 FloatDock 设置", showSettings, 24); settingsButton.FontFamily = new("Segoe MDL2 Assets");
        settingsButton.Height = 30; settingsButton.FontSize = 12; row.Children.Add(settingsButton);
        var close = DockChrome.Button("×", "停止媒体栏", Close, 24); close.Height = 30; row.Children.Add(close);
        Content = new Border { CornerRadius = new(10), Padding = new(4, 0, 4, 0), Child = row,
            Background = new SolidColorBrush(Color.FromArgb(240, 29, 34, 44)), BorderBrush = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)), BorderThickness = new(1) };
        SourceInitialized += (_, _) => {
            _handle = new WindowInteropHelper(this).Handle; _originalStyle = NativeMethods.GetWindowLong(_handle, -16);
            NativeMethods.SetWindowLong(_handle, -20, NativeMethods.GetWindowLong(_handle, -20) | 0x80 | 0x08000000);
            _timer.Start(); UpdatePlacement();
        };
        _timer.Tick += (_, _) => UpdatePlacement();
        Closing += (_, _) => { _closed = true; _timer.Stop(); _media.Dispose(); Detach(); };
    }

    private void UpdatePlacement()
    {
        if (_closed) return;
        var parent = TaskbarNative.FindWindow("Shell_TrayWnd", null);
        if (parent == 0 || !NativeMethods.GetWindowRect(parent, out var raw)) { HideSurface("等待系统任务栏"); return; }
        if (!NativeMethods.IsWindowVisible(parent)) { HideSurface("系统任务栏隐藏，媒体面板已关闭"); return; }
        var bar = new PixelRect(raw.Left, raw.Top, raw.Right, raw.Bottom);
        if (!bar.Valid || bar.Height > bar.Width) { HideSurface("当前融合媒体栏仅支持横向任务栏"); return; }
        if (parent != _parent)
        {
            Detach(); _parent = parent; _motion = default; _range = default; _lastProbe = null;
            NativeMethods.SetWindowLong(_handle, -16, (_originalStyle & ~unchecked((int)0x80000000)) | 0x40000000);
            SetParent(_handle, parent);
            _attached = GetParent(_handle) == parent;
            if (!_attached) { NativeMethods.SetWindowLong(_handle, -16, _originalStyle); _parent = 0; HideSurface("任务栏挂载失败，可在设置中停止重试"); return; }
        }
        var monitor = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(NativeMethods.MonitorFromWindow(parent, 2), ref monitor)) { HideSurface("无法读取屏幕范围"); return; }
        _motion = NativeTaskbarPolicy.Observe(_motion, bar, new(monitor.Monitor.Left, monitor.Monitor.Top, monitor.Monitor.Right, monitor.Monitor.Bottom));
        if (_probe?.IsCompleted == true)
        {
            _lastProbe = _probe.IsCompletedSuccessfully ? _probe.Result : null;
            _ = _probe.Exception; _probe = null; _nextProbe = DateTime.UtcNow.AddMilliseconds(700);
        }
        if (_probe == null && DateTime.UtcNow >= _nextProbe) _probe = Task.Run(() => Probe(parent, bar));
        var ready = _lastProbe is { Valid: true } result && result.Parent == parent && result.Bar == bar && DateTime.UtcNow - result.At < TimeSpan.FromSeconds(2);
        var scale = GetDpiForWindow(parent) / 96d;
        if (scale <= 0) { HideSurface("无法读取任务栏缩放"); return; }
        var width = (int)Math.Ceiling((_media.PreferredWidth + 64) * scale);
        var free = ready ? NativeTaskbarPolicy.FreeRanges(bar.Width, _lastProbe!.Occupied, (int)(8 * scale), (int)(32 * scale)) : [];
        _range = NativeTaskbarPolicy.Select(free, width, _range);
        if (!NativeTaskbarPolicy.CanShow(_motion, ready, _range, width))
        { HideSurface(ready ? "任务栏收起、移动或空间不足时自动隐藏媒体栏" : "正在确认任务栏图标与托盘位置"); return; }
        Width = _media.PreferredWidth + 64; Height = Math.Min(36, bar.Height / scale);
        var height = (int)Math.Round(Height * scale);
        var point = new NativePoint { X = bar.Left + _range.Start, Y = bar.Top + Math.Max(0, (bar.Height - height) / 2) };
        if (!ScreenToClient(parent, ref point) || !SetWindowPos(_handle, 0, point.X, point.Y, width, height, 0x14))
        { HideSurface("任务栏定位失败，媒体栏已隐藏"); return; }
        Opacity = 1; IsHitTestVisible = true; NativeMethods.ShowWindowAsync(_handle, 8);
        SetStatus("原生媒体栏已挂载；样式与动画由 Windhawk 方案负责");
    }

    private static ProbeResult Probe(nint parent, PixelRect bar)
    {
        try
        {
            var root = AutomationElement.FromHandle(parent);
            var controls = root.FindAll(TreeScope.Descendants, new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)));
            var occupied = new List<TaskbarRange>();
            foreach (AutomationElement control in controls)
            {
                var value = control.Current;
                if (value.ProcessId == Environment.ProcessId || value.IsOffscreen) continue;
                var rect = value.BoundingRectangle;
                if (!rect.IsEmpty && rect.Width > 0 && rect.Bottom > bar.Top && rect.Top < bar.Bottom)
                    occupied.Add(new((int)Math.Floor(rect.Left - bar.Left), (int)Math.Ceiling(rect.Right - bar.Left)));
            }
            // Include the complete native tray, not just its currently exposed buttons.
            var tray = FindWindowEx(parent, 0, "TrayNotifyWnd", null);
            if (tray != 0 && NativeMethods.GetWindowRect(tray, out var trayRect)) occupied.Add(new(trayRect.Left - bar.Left, trayRect.Right - bar.Left));
            return new(parent, bar, occupied, controls.Count > 0 && occupied.Count > 0, DateTime.UtcNow);
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or UnauthorizedAccessException)
        { return new(parent, bar, [], false, DateTime.UtcNow); }
    }
    private void HideSurface(string status) { Opacity = 0; IsHitTestVisible = false; _media.ClosePopup(); SetStatus(status); }
    private void SetStatus(string status) { if (_status == status) return; _status = status; StatusChanged?.Invoke(status); }
    private void Detach()
    {
        if (_handle != 0 && NativeMethods.IsWindow(_handle) && _attached)
        {
            SetWindowPos(_handle, 0, 0, 0, 0, 0, 0x97);
            SetParent(_handle, 0); NativeMethods.SetWindowLong(_handle, -16, _originalStyle);
        }
        _attached = false; _parent = 0;
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetParent(nint child, nint parent);
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool ScreenToClient(nint window, ref NativePoint point);
    [DllImport("user32.dll")] private static extern nint GetParent(nint window);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string name, string? title);
}
