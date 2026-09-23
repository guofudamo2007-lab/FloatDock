using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using FloatDock.Core;
using FloatDock.Windows.Media;
using FloatDock.Windows.Taskbar;
using Microsoft.Win32;

namespace FloatDock.Windows;

public sealed class DockWindow : Window
{
    private readonly DockSettings _settings;
    private readonly StackPanel _items = new() { Orientation = Orientation.Horizontal };
    private readonly ScrollViewer _scroll;
    private readonly Border _plate;
    private readonly Grid _root;
    private readonly StackPanel _utilities;
    private readonly Button _clock;
    private readonly Border _startArea;
    private DesktopPlacement? _placement;
    private bool _positioning, _hotkey;
    private uint _taskbarCreated;
    private MediaCapsule? _media;
    private readonly Dictionary<string, DockIcon> _icons = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _windowsTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _focusTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private nint _handle, _foreground;
    private bool _refreshing, _closed;
    private bool Reduced => _settings.ReducedMotion || !SystemParameters.ClientAreaAnimation;

    public DockWindow(DockSettings settings)
    {
        _settings = settings;
        Title = "FloatDock"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false;
        ShowActivated = false; Height = 140; Width = 520; UseLayoutRounding = true;
        _root = new Grid { Margin = new(10, 0, 10, 8) };
        _root.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); _root.ColumnDefinitions.Add(new());
        _root.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); _root.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _plate = new Border { CornerRadius = new(19), Height = settings.IconSize + 24, VerticalAlignment = VerticalAlignment.Bottom,
            BorderThickness = new(1), IsHitTestVisible = false, Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = .2 } };
        _scroll = new ScrollViewer { Content = _items, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new(24, 0, 24, 0), CanContentScroll = false };
        Grid.SetColumnSpan(_plate, 4); Grid.SetColumn(_scroll, 1);
        _root.Children.Add(_plate); _root.Children.Add(_scroll);
        var start = DockChrome.Button("", "开始菜单", OpenStart, 40);
        var startGlyph = new System.Windows.Shapes.Path { Width = 17, Height = 17, Stretch = Stretch.Fill, Fill = new SolidColorBrush(Color.FromRgb(190, 216, 244)),
            Data = Geometry.Parse("M0,0 H7 V7 H0 Z M10,0 H17 V7 H10 Z M0,10 H7 V17 H0 Z M10,10 H17 V17 H10 Z") };
        start.Content = startGlyph;
        var startArea = _startArea = new Border { Child = start, Width = 52, Height = _plate.Height, VerticalAlignment = VerticalAlignment.Bottom, BorderThickness = new(0, 0, 1, 0), BorderBrush = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)) };
        _root.Children.Add(startArea);
        _utilities = new StackPanel { Orientation = Orientation.Horizontal, Height = _plate.Height, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(4, 0, 7, 0) };
        _clock = DockChrome.Button(DateTime.Now.ToString("HH:mm"), "日期与时间", () => Launch("ms-settings:dateandtime"), 48); _clock.FontSize = 11;
        var settingsButton = DockChrome.Button("\uE713", "设置", () => OpenSettings(), 32); settingsButton.FontFamily = new("Segoe MDL2 Assets");
        var exit = DockChrome.Button("退出", "退出 FloatDock 并还原系统任务栏", Close, 42); exit.FontSize = 11;
        _utilities.Children.Add(_clock); _utilities.Children.Add(settingsButton); _utilities.Children.Add(exit);
        Grid.SetColumn(_utilities, 3); _root.Children.Add(_utilities); Content = _root;
        ContextMenu = CreateMenu(null); ContextMenuOpening += (_, _) => ContextMenu = CreateMenu(null);
        ConfigureMedia();
        ApplyPlate();
        SourceInitialized += (_, _) => {
            _handle = new WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowLong(_handle, -20, NativeMethods.GetWindowLong(_handle, -20) | 0x80 | 0x08000000);
            HwndSource.FromHwnd(_handle).AddHook(WindowMessage);
            _hotkey = TaskbarNative.RegisterHotKey(_handle, 1, 0x4003, 0x51); // Ctrl+Alt+Q, no repeat.
            _taskbarCreated = TaskbarNative.RegisterWindowMessage("TaskbarCreated");
            ConfigurePlacement();
            Position();
        };
        Loaded += async (_, _) => { Position(); await RefreshWindows(); _windowsTimer.Start(); _focusTimer.Start(); };
        _windowsTimer.Tick += async (_, _) => await RefreshWindows();
        _focusTimer.Tick += (_, _) => RefreshFocus();
        MouseMove += (_, _) => UpdateMotion(); MouseLeave += (_, _) => UpdateMotion();
        _scroll.PreviewMouseWheel += (_, e) => { _scroll.ScrollToHorizontalOffset(_scroll.HorizontalOffset - e.Delta); e.Handled = true; UpdateMotion(); };
        Closed += (_, _) => { _closed = true; _windowsTimer.Stop(); _focusTimer.Stop(); _media?.Dispose();
            if (_hotkey) TaskbarNative.UnregisterHotKey(_handle, 1); _placement?.Dispose(); _placement = null; };
    }

    private async Task RefreshWindows()
    {
        if (_closed || _refreshing) return;
        _refreshing = true;
        try
        {
            var windows = await Task.Run(WindowService.Enumerate);
            if (_closed) return;
            var entries = DockModel.Merge(_settings.Pins, windows);
            var keys = entries.Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var key in _icons.Keys.Where(k => !keys.Contains(k)).ToArray())
            { _items.Children.Remove(_icons[key]); _icons.Remove(key); }
            if (entries.Count > 0)
                foreach (var extra in _items.Children.OfType<Button>().Where(b => b is not DockIcon).ToArray()) _items.Children.Remove(extra);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (!_icons.TryGetValue(entry.Key, out var icon))
                {
                    icon = new DockIcon(entry, _settings.IconSize);
                    icon.Click += (_, _) => Invoke(icon);
                    icon.ContextMenuOpening += (_, _) => icon.ContextMenu = CreateMenu(icon.Entry);
                    icon.ContextMenu = CreateMenu(entry);
                    _icons.Add(entry.Key, icon);
                }
                else icon.Update(entry);
                if (_items.Children.IndexOf(icon) != index) { _items.Children.Remove(icon); _items.Children.Insert(index, icon); }
            }
            if (entries.Count == 0 && _items.Children.Count == 0)
            {
                var add = new Button { Content = "+", FontSize = 26, Width = 64, Height = 56, VerticalAlignment = VerticalAlignment.Bottom,
                    Background = new SolidColorBrush(Color.FromArgb(150, 25, 30, 42)), Foreground = Brushes.White, ToolTip = "添加应用 / 右键设置" };
                add.Click += (_, _) => AddPin(); add.ContextMenu = CreateMenu(null); _items.Children.Add(add);
            }
            Position(); RefreshFocus();
        }
        finally { _refreshing = false; }
    }

    private void Position()
    {
        if (_positioning || _closed) return; _positioning = true;
        try
        {
            _plate.Height = _settings.IconSize + 24; _utilities.Height = _startArea.Height = _plate.Height;
            var work = _placement?.Position(this, _plate.Height + 16) ?? SystemParameters.WorkArea;
            var mediaWidth = _media?.PreferredWidth ?? 0;
            if (_media != null) _media.Width = mediaWidth;
            var fixedWidth = 52 + 20 + 48 + 144 + mediaWidth;
            Width = DockModel.ViewportWidth(Math.Max(1, _icons.Count), _settings.IconSize + 16, Math.Max(0, work.Width - fixedWidth - 24)) + fixedWidth;
            Left = work.Left + (work.Width - Width) / 2;
            Top = work.Bottom - Height;
        }
        finally { _positioning = false; }
    }

    private void RefreshFocus()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        _clock.Content = DateTime.Now.ToString("HH:mm"); _clock.ToolTip = DateTime.Now.ToString("yyyy年M月d日 dddd");
        NativeMethods.GetWindowThreadProcessId(foreground, out var pid);
        if (pid != Environment.ProcessId) _foreground = WindowService.Representative(foreground, _icons.Values.SelectMany(icon => icon.Entry.Windows));
        // Keep the HWND alive to observe when a fullscreen application exits.
        var hidden = WindowService.IsFullscreen(foreground, _handle);
        if (hidden) _media?.ClosePopup();
        Opacity = hidden ? 0 : 1; IsHitTestVisible = !hidden;
        UpdateMotion();
    }

    private void UpdateMotion()
    {
        var pointer = Mouse.GetPosition(_items);
        var icons = _items.Children.OfType<DockIcon>().ToArray();
        var centers = icons.Select(icon => icon.TranslatePoint(new Point(icon.Width / 2, 0), _items).X).ToArray();
        var shifts = DockModel.NeighborShifts(centers, _scroll.IsMouseOver ? pointer.X : double.PositiveInfinity, _settings.IconSize, Reduced);
        for (var i = 0; i < icons.Length; i++)
        {
            var icon = icons[i];
            var center = centers[i];
            var distance = _scroll.IsMouseOver ? Math.Abs(pointer.X - center) : double.PositiveInfinity;
            icon.SetState(distance, icon.Entry.Windows.Any(w => w.Handle == _foreground), Reduced, shifts[i]);
        }
    }

    private void Invoke(DockIcon icon)
    {
        icon.Bounce(Reduced);
        var target = DockModel.NextWindow(icon.Entry.Windows, _foreground);
        if (target != 0)
        {
            if (!WindowService.Activate(target))
                MessageBox.Show("Windows 未允许切换到这个窗口。请先使用 Alt+Tab 切换，或稍后重试。", "FloatDock");
        }
        else if (icon.Entry.Path != null) Launch(icon.Entry.Path);
    }

    private ContextMenu CreateMenu(DockEntry? entry)
    {
        var menu = new ContextMenu();
        if (entry?.Path != null)
        {
            AddMenu(menu, entry.IsPinned ? "取消固定" : "固定到 FloatDock", () => {
                if (entry.IsPinned) _settings.Pins.RemoveAll(p => string.Equals(p.Path, entry.Path, StringComparison.OrdinalIgnoreCase));
                else _settings.Pins.Add(new(Path.GetFileNameWithoutExtension(entry.Path), entry.Path));
                Save(); _ = RefreshWindows();
            });
            AddMenu(menu, "打开新窗口", () => Launch(entry.Path));
            menu.Items.Add(new Separator());
        }
        if (entry?.Windows.Count > 1)
        {
            foreach (var window in entry.Windows) AddMenu(menu, window.Title, () => WindowService.Activate(window.Handle));
            menu.Items.Add(new Separator());
        }
        AddMenu(menu, "添加应用…", AddPin);
        AddMenu(menu, "接管底部任务栏（退出自动还原）", () => { _settings.ReplaceTaskbar = !_settings.ReplaceTaskbar; ConfigurePlacement(); Position(); Save(); }, _settings.ReplaceTaskbar);
        AddMenu(menu, "显示圆角底板", () => { _settings.ShowPlate = !_settings.ShowPlate; ApplyPlate(); Save(); }, _settings.ShowPlate);
        AddMenu(menu, "减少动画", () => { _settings.ReducedMotion = !_settings.ReducedMotion; UpdateMotion(); Save(); }, _settings.ReducedMotion);
        AddMenu(menu, "媒体胶囊", () => { _settings.ShowMedia = !_settings.ShowMedia; ConfigureMedia(); Position(); Save(); }, _settings.ShowMedia);
        var sizes = new MenuItem { Header = "图标大小" };
        foreach (var size in new[] { 32, 40, 48, 56, 64 })
        {
            var item = new MenuItem { Header = $"{size} px", IsCheckable = true, IsChecked = _settings.IconSize == size };
            item.Click += (_, _) => { _settings.IconSize = size; _icons.Clear(); _items.Children.Clear(); Save(); _ = RefreshWindows(); };
            sizes.Items.Add(item);
        }
        menu.Items.Add(sizes); menu.Items.Add(new Separator());
        AddMenu(menu, "Windows 任务栏设置…", () => Launch("ms-settings:taskbar"));
        AddMenu(menu, "关于 FloatDock", () => MessageBox.Show("FloatDock 0.3 · GPL-3.0-or-later\n原生任务栏融合与可选独立 Dock\n\n右侧【退出】会还原独立 Dock 接管的任务栏。\nCtrl+Alt+Q 可关闭 Dock（快捷键未被占用时）。\n媒体区点击展开，滚轮切换来源。", "FloatDock"));
        AddMenu(menu, "退出并还原系统任务栏", Close);
        return menu;
    }

    private static void AddMenu(ContextMenu menu, string title, Action action, bool? check = null)
    {
        var item = new MenuItem { Header = title, IsCheckable = check.HasValue, IsChecked = check ?? false };
        item.Click += (_, _) => action(); menu.Items.Add(item);
    }

    private void AddPin()
    {
        var dialog = new OpenFileDialog { Title = "选择要固定的应用", Filter = "应用程序 (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        if (!_settings.Pins.Any(p => string.Equals(p.Path, dialog.FileName, StringComparison.OrdinalIgnoreCase)))
            _settings.Pins.Add(new(Path.GetFileNameWithoutExtension(dialog.FileName), dialog.FileName));
        Save(); _ = RefreshWindows();
    }

    private void ApplyPlate()
    {
        _plate.Background = _settings.ShowPlate ? new SolidColorBrush(Color.FromArgb(242, 29, 34, 44)) : new SolidColorBrush(Color.FromArgb(180, 29, 34, 44));
        _plate.BorderBrush = new SolidColorBrush(Color.FromArgb(45, 210, 226, 255));
    }

    private void ConfigureMedia()
    {
        if (_media != null) { _media.Dispose(); _root.Children.Remove(_media); _media = null; }
        if (!_settings.ShowMedia) return;
        _media = new MediaCapsule(_settings, Save) { VerticalAlignment = VerticalAlignment.Bottom, Margin = new(4, 0, 4, 8) };
        _media.PresentationChanged += (_, _) => Position();
        Grid.SetColumn(_media, 2); _root.Children.Add(_media);
    }

    private void OpenSettings() { var menu = CreateMenu(null); menu.PlacementTarget = _utilities; menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top; menu.IsOpen = true; }
    private static void OpenStart()
    { var taskbar = TaskbarNative.FindWindow("Shell_TrayWnd", null); NativeMethods.SendMessageTimeout(taskbar, 0x112, 0xF130, 0, 2, 1000, out _); }
    private void ConfigurePlacement()
    {
        _placement?.Dispose(); _placement = null;
        if (!_settings.ReplaceTaskbar || _handle == 0) return;
        try { _placement = new DesktopPlacement(_handle); }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException or UnauthorizedAccessException)
        { _settings.ReplaceTaskbar = false; MessageBox.Show("无法接管任务栏，已还原系统设置。\n" + ex.Message, "FloatDock"); }
    }
    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x84 && IsLoaded)
        {
            var packed = lParam.ToInt64(); var point = PointFromScreen(new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
            var plateBounds = _plate.TransformToAncestor(this).TransformBounds(new Rect(_plate.RenderSize));
            if (!plateBounds.Contains(point) && !_icons.Values.Any(icon => icon.ImageBounds(this).Contains(point)))
            { handled = true; return -1; } // HTTRANSPARENT: spare animation space must not swallow application clicks.
        }
        else if (message == 0x312 && wParam == 1) { handled = true; Close(); }
        else if ((uint)message == DesktopPlacement.Callback && wParam == 1 || message is 0x7E or 0x2E0)
        { Dispatcher.BeginInvoke(() => Position()); }
        else if ((uint)message == _taskbarCreated) { Dispatcher.BeginInvoke(() => { if (!_closed) { ConfigurePlacement(); Position(); } }); }
        return 0;
    }

    private void Save()
    {
        try { SettingsStore.Save(_settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { MessageBox.Show("设置暂时生效，但保存失败：\n" + ex.Message, "FloatDock"); }
    }

    private static void Launch(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
        { MessageBox.Show("无法打开应用：\n" + ex.Message, "FloatDock"); }
    }
}
