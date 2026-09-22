using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FloatDock.Core;
using FloatDock.Windows.Media;
using Microsoft.Win32;

namespace FloatDock.Windows;

public sealed class DockWindow : Window
{
    private readonly DockSettings _settings;
    private readonly StackPanel _items = new() { Orientation = Orientation.Horizontal };
    private readonly ScrollViewer _scroll;
    private readonly Border _plate;
    private readonly Grid _root;
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
        ShowActivated = false; Height = 152; Width = 240; UseLayoutRounding = true;
        _root = new Grid { Margin = new(12, 0, 12, 0) };
        _root.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); _root.ColumnDefinitions.Add(new());
        _plate = new Border { CornerRadius = new(22), Height = 68, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new(0, 0, 0, 2), BorderThickness = new(1), IsHitTestVisible = false };
        _scroll = new ScrollViewer { Content = _items, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new(12, 0, 12, 0), CanContentScroll = false };
        Grid.SetColumn(_plate, 1); Grid.SetColumn(_scroll, 1);
        _root.Children.Add(_plate); _root.Children.Add(_scroll); Content = _root;
        ConfigureMedia();
        ApplyPlate();
        SourceInitialized += (_, _) => {
            _handle = new WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowLong(_handle, -20, NativeMethods.GetWindowLong(_handle, -20) | 0x80 | 0x08000000);
        };
        Loaded += async (_, _) => { await RefreshWindows(); _windowsTimer.Start(); _focusTimer.Start(); };
        _windowsTimer.Tick += async (_, _) => await RefreshWindows();
        _focusTimer.Tick += (_, _) => RefreshFocus();
        MouseMove += (_, _) => UpdateMotion(); MouseLeave += (_, _) => UpdateMotion();
        _scroll.PreviewMouseWheel += (_, e) => { _scroll.ScrollToHorizontalOffset(_scroll.HorizontalOffset - e.Delta); e.Handled = true; UpdateMotion(); };
        Closed += (_, _) => { _closed = true; _windowsTimer.Stop(); _focusTimer.Stop(); _media?.Dispose(); };
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
        var work = SystemParameters.WorkArea;
        if (_media != null) _media.Width = Math.Min(260, work.Width * .45);
        var mediaWidth = _media == null ? 0 : _media.Width + 12;
        Width = DockModel.ViewportWidth(Math.Max(1, _icons.Count), _settings.IconSize + 24, Math.Max(0, work.Width - 80 - mediaWidth)) + 48 + mediaWidth;
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Bottom - Height - 12;
    }

    private void RefreshFocus()
    {
        var foreground = NativeMethods.GetForegroundWindow();
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
        foreach (var icon in _icons.Values)
        {
            var center = icon.TranslatePoint(new Point(icon.Width / 2, 0), _items).X;
            var distance = _scroll.IsMouseOver ? Math.Abs(pointer.X - center) : double.PositiveInfinity;
            icon.SetState(distance, icon.Entry.Windows.Any(w => w.Handle == _foreground), Reduced);
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
        AddMenu(menu, "显示圆角底板", () => { _settings.ShowPlate = !_settings.ShowPlate; ApplyPlate(); Save(); }, _settings.ShowPlate);
        AddMenu(menu, "减少动画", () => { _settings.ReducedMotion = !_settings.ReducedMotion; UpdateMotion(); Save(); }, _settings.ReducedMotion);
        AddMenu(menu, "媒体胶囊", () => { _settings.ShowMedia = !_settings.ShowMedia; ConfigureMedia(); Position(); Save(); }, _settings.ShowMedia);
        var sizes = new MenuItem { Header = "图标大小" };
        foreach (var size in new[] { 32, 44, 56, 64 })
        {
            var item = new MenuItem { Header = $"{size} px", IsCheckable = true, IsChecked = _settings.IconSize == size };
            item.Click += (_, _) => { _settings.IconSize = size; _icons.Clear(); _items.Children.Clear(); Save(); _ = RefreshWindows(); };
            sizes.Items.Add(item);
        }
        menu.Items.Add(sizes); menu.Items.Add(new Separator());
        AddMenu(menu, "Windows 任务栏设置…", () => Launch("ms-settings:taskbar"));
        AddMenu(menu, "关于 FloatDock", () => MessageBox.Show("FloatDock 0.2 · MIT 开源\n浮动图标与媒体控制\n\n图标区滚轮横向浏览，媒体区滚轮切换来源。\n点击媒体胶囊展开播放进度和同步歌词。", "FloatDock"));
        AddMenu(menu, "退出 FloatDock", Close);
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
        _plate.Background = _settings.ShowPlate ? new SolidColorBrush(Color.FromArgb(210, 24, 29, 41)) : Brushes.Transparent;
        _plate.BorderBrush = _settings.ShowPlate ? new SolidColorBrush(Color.FromArgb(60, 210, 226, 255)) : Brushes.Transparent;
    }

    private void ConfigureMedia()
    {
        if (_media != null) { _media.Dispose(); _root.Children.Remove(_media); _media = null; }
        if (!_settings.ShowMedia) return;
        _media = new MediaCapsule(_settings, Save) { VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 12, 6) };
        Grid.SetColumn(_media, 0); _root.Children.Add(_media);
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
