using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FloatDock.Windows.Taskbar;

namespace FloatDock.Windows;

internal sealed class FusionWindow : Window
{
    private readonly DockSettings _settings;
    private NativeMediaHost? _media;
    private DockWindow? _dock;
    private readonly TextBlock _status = new() { Text = "尚未启用任何桌面效果。", Foreground = Brushes.LightSteelBlue, TextWrapping = TextWrapping.Wrap, Margin = new(0, 16, 0, 0) };
    internal FusionWindow(DockSettings settings)
    {
        _settings = settings;
        Title = "FloatDock · 原生融合"; Width = 580; Height = 510; MinWidth = 500; MinHeight = 470;
        Background = new SolidColorBrush(Color.FromRgb(25, 30, 40)); Foreground = Brushes.White; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var body = new StackPanel { Margin = new(28) };
        body.Children.Add(new TextBlock { Text = "FloatDock", FontSize = 28, FontWeight = FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Text = "原生任务栏 · 浮动动画 · 媒体控制", FontSize = 14, Foreground = Brushes.LightSteelBlue, Margin = new(0, 6, 0, 20) });
        body.Children.Add(Paragraph("1  外观与动画", "在 Windhawk 中安装 Windows 11 Taskbar Styler 和 Taskbar Dock Animation Plus，导入包内配置。源码和版本记录随包提供。"));
        body.Children.Add(ActionButton("打开融合配置目录", OpenProfiles));
        body.Children.Add(Paragraph("2  任务栏媒体", "媒体控件嵌入原任务栏的空闲区域，保留系统图标与托盘。先导入方案，再启动媒体栏。"));
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(ActionButton("启动媒体栏", StartMedia)); actions.Children.Add(ActionButton("停止媒体栏", () => { _media?.Close(); _status.Text = "媒体栏已停止。"; })); body.Children.Add(actions);
        body.Children.Add(_status);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 18, 0, 0) };
        footer.Children.Add(ActionButton("可选：独立 Dock", StartDock)); footer.Children.Add(ActionButton("退出 FloatDock", Close)); body.Children.Add(footer);
        Content = new ScrollViewer { Content = body, Background = Background, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Closing += (_, _) => { _media?.Close(); _dock?.Close(); };
    }
    private void StartMedia()
    {
        _dock?.Close();
        if (_media != null) return;
        _media = new(_settings, () => { Show(); WindowState = WindowState.Normal; Activate(); });
        _media.StatusChanged += text => _status.Text = text;
        _media.Closed += (_, _) => { _media = null; _status.Text = "媒体栏已停止。"; };
        _media.Show();
    }
    private void StartDock()
    {
        _media?.Close(); if (_dock != null) return;
        _dock = new(_settings); _dock.Closed += (_, _) => _dock = null; _dock.Show();
        _status.Text = "独立 Dock 已启用。右端【退出】会还原其接管的系统任务栏。";
    }
    private void OpenProfiles()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "integrations", "windhawk");
        if (!Directory.Exists(path)) { _status.Text = "请使用完整发布包；源码配置位于 integrations/windhawk。"; return; }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    private static StackPanel Paragraph(string title, string text)
    {
        var row = new StackPanel { Margin = new(0, 10, 0, 6) };
        row.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold });
        row.Children.Add(new TextBlock { Text = text, FontSize = 12, Foreground = Brushes.LightSteelBlue, TextWrapping = TextWrapping.Wrap, Margin = new(0, 5, 0, 4) }); return row;
    }
    private static Button ActionButton(string title, Action action)
    { var button = DockChrome.Button(title, title, action, double.NaN); button.Padding = new(12, 4, 12, 4); button.MinWidth = 120; button.Margin = new(0, 3, 10, 3); button.FontSize = 12; button.Background = new SolidColorBrush(Color.FromRgb(46, 57, 74)); return button; }
}
