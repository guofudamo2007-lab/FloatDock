using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FloatDock.Core.Media;
using Microsoft.Win32;
using MediaTimeline = FloatDock.Core.Media.MediaTimeline;

namespace FloatDock.Windows.Media;

internal sealed class MediaCapsule : Border, IDisposable
{
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(148, 205, 255));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(160, 173, 194));
    private readonly DockSettings _settings;
    private readonly Action _save;
    private readonly WindowsMediaBackend _backend = new();
    private readonly MediaCoordinator _coordinator = new();
    private readonly LyricsClient _lyricsClient = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Popup _popup;
    private readonly Image _cover = new() { Width = 40, Height = 40, Stretch = Stretch.UniformToFill };
    private readonly Image _cardCover = new() { Width = 64, Height = 64, Stretch = Stretch.UniformToFill };
    private readonly TextBlock _compactTitle = Text("播放你的音乐", 12, Brushes.White);
    private readonly TextBlock _compactSubtitle = Text("媒体控制 · 点击展开", 10, Muted);
    private readonly TextBlock _title = Text("没有正在播放的媒体", 17, Brushes.White);
    private readonly TextBlock _artist = Text("打开支持系统媒体控制的播放器", 12, Muted);
    private readonly TextBlock _lyric = Text("", 19, Accent);
    private readonly TextBlock _nextLyric = Text("", 12, Muted);
    private readonly TextBlock _status = Text("", 10, Muted);
    private readonly TextBlock _elapsed = Text("0:00", 10, Muted);
    private readonly TextBlock _duration = Text("0:00", 10, Muted);
    private readonly Slider _seek = new() { Minimum = 0, Maximum = 1, IsEnabled = false, Margin = new(0, 12, 0, 0) };
    private readonly Button _source;
    private readonly CheckBox _online;
    private readonly StackPanel _compactInfo = new();
    private readonly Grid _compactLayout;
    private readonly StackPanel _hoverControls;
    private readonly List<(Button Button, MediaCommand Command)> _buttons = [];
    private readonly TranslateTransform _lyricShift = new();
    private CancellationTokenSource? _lyricRequest;
    private MediaTrackKey? _track, _seekTrack;
    private MediaSnapshot? _display;
    private LrcDocument? _lyrics;
    private byte[]? _lastArtwork;
    private string? _lastLyricText;
    private int _lastHighlight = -1;
    private int _lastLyricIndex = -2;
    private bool _polling, _disposed, _started, _commandBusy, _seeking;
    private bool Reduced => _settings.ReducedMotion || !SystemParameters.ClientAreaAnimation;
    internal Border Details => (Border)_popup.Child;
    internal double PreferredWidth => _display == null ? 36 : 190;
    internal event EventHandler? PresentationChanged;

    public MediaCapsule(DockSettings settings, Action save)
    {
        _settings = settings; _save = save;
        Width = 36; Height = 48; CornerRadius = new(12); Padding = new(4);
        Background = Brushes.Transparent; BorderThickness = new(0);
        Cursor = Cursors.Hand;
        var compact = _compactLayout = new Grid(); compact.ColumnDefinitions.Add(new() { Width = new(36) }); compact.ColumnDefinitions.Add(new());
        _cover.Width = 28; _cover.Height = 28;
        _cover.Source = FallbackArtwork(); _cardCover.Source = _cover.Source;
        compact.Children.Add(_cover);
        _compactInfo.VerticalAlignment = VerticalAlignment.Center;
        _compactInfo.Children.Add(_compactTitle); _compactInfo.Children.Add(_compactSubtitle);
        Grid.SetColumn(_compactInfo, 1); compact.Children.Add(_compactInfo);
        _hoverControls = Controls(30); _hoverControls.Opacity = 0; _hoverControls.IsHitTestVisible = false;
        Grid.SetColumn(_hoverControls, 1); compact.Children.Add(_hoverControls);
        Child = compact;

        var card = new StackPanel();
        var heading = new Grid(); heading.ColumnDefinitions.Add(new()); heading.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _source = Button("媒体来源 ▾", "选择媒体来源", () => OpenSources());
        _source.FontSize = 11; _source.MaxWidth = 260; _source.HorizontalAlignment = HorizontalAlignment.Left;
        heading.Children.Add(_source);
        var close = Button("×", "关闭媒体面板", () => ClosePopup()); Grid.SetColumn(close, 1); heading.Children.Add(close); card.Children.Add(heading);
        var metadata = new Grid { Margin = new(0, 12, 0, 4) };
        metadata.ColumnDefinitions.Add(new() { Width = new(80) }); metadata.ColumnDefinitions.Add(new());
        metadata.Children.Add(_cardCover);
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(_title); labels.Children.Add(_artist); Grid.SetColumn(labels, 1); metadata.Children.Add(labels); card.Children.Add(metadata);
        _title.TextWrapping = TextWrapping.Wrap; _title.MaxHeight = 52;
        _artist.Margin = new(0, 5, 0, 0);
        card.Children.Add(_seek);
        var times = new Grid(); times.Children.Add(_elapsed); _duration.HorizontalAlignment = HorizontalAlignment.Right; times.Children.Add(_duration); card.Children.Add(times);
        card.Children.Add(Controls(44));
        _lyric.Margin = new(0, 12, 0, 6); _lyric.TextWrapping = TextWrapping.Wrap; _lyric.MaxHeight = 90; _lyric.RenderTransform = _lyricShift;
        _nextLyric.TextWrapping = TextWrapping.Wrap; _nextLyric.MaxHeight = 42;
        card.Children.Add(_lyric); card.Children.Add(_nextLyric);
        _status.Margin = new(0, 12, 0, 8); _status.TextWrapping = TextWrapping.Wrap; card.Children.Add(_status);
        var lyricActions = new StackPanel { Orientation = Orientation.Horizontal };
        lyricActions.Children.Add(Button("导入 LRC", "为当前歌曲导入本地同步歌词", LoadLocalLyrics));
        lyricActions.Children.Add(Button("清除歌词", "清除当前歌曲的歌词", () => { CancelLyrics(); ApplyLyrics(null, "已清除当前歌词"); }));
        card.Children.Add(lyricActions);
        _online = new CheckBox { Content = "在线匹配歌词（LRCLIB）", Foreground = Brushes.White, IsChecked = settings.OnlineLyrics, Margin = new(0, 12, 0, 4), FontSize = 12 };
        _online.Click += (_, _) => {
            _settings.OnlineLyrics = _online.IsChecked == true; _save(); CancelLyrics();
            if (_settings.OnlineLyrics && _coordinator.Snapshot is { } snapshot) _ = FetchLyrics(snapshot);
            else _status.Text = "在线匹配已关闭，可导入本地 LRC";
        };
        card.Children.Add(_online);
        var privacy = Text("开启后将曲名、歌手、专辑和时长发送至 lrclib.net。", 10, Muted); privacy.TextWrapping = TextWrapping.Wrap; card.Children.Add(privacy);
        var surface = new Border { Width = 360, Padding = new(20), CornerRadius = new(24), Background = new SolidColorBrush(Color.FromArgb(249, 22, 28, 41)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(65, 163, 191, 230)), BorderThickness = new(1),
            Child = new ScrollViewer { Content = card, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
        _popup = new Popup { Child = surface, PlacementTarget = this, Placement = PlacementMode.Top, VerticalOffset = -12, AllowsTransparency = true, StaysOpen = false, PopupAnimation = PopupAnimation.Fade };
        MouseLeftButtonUp += (_, e) => { if (!e.Handled) { OpenPopup(); e.Handled = true; } };
        MouseEnter += (_, _) => Hover(true); MouseLeave += (_, _) => Hover(false);
        PreviewMouseWheel += (_, e) => { CycleSource(e.Delta > 0 ? -1 : 1); e.Handled = true; };
        _seek.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => BeginSeek()));
        _seek.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(async (_, _) => await CommitSeek()));
        _seek.PreviewMouseLeftButtonDown += (_, _) => BeginSeek();
        _seek.PreviewMouseLeftButtonUp += async (_, _) => await CommitSeek();
        _seek.PreviewKeyDown += (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown) BeginSeek(); };
        _seek.KeyUp += async (_, _) => await CommitSeek();
        _poll.Tick += async (_, _) => await Poll(); _clock.Tick += (_, _) => Tick();
        Loaded += async (_, _) => {
            if (_started || _disposed) return; _started = true;
            _poll.Start(); _clock.Start(); await Poll();
        };
        ApplySnapshot(null);
    }

    private async Task Poll()
    {
        if (_disposed || _polling) return;
        _polling = true;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var (sessions, current) = await _backend.GetSessionsAsync(timeout.Token);
            if (_disposed) return;
            _coordinator.SetSessions(sessions, current);
            await _coordinator.RefreshAsync(timeout.Token);
            if (!_disposed) ApplySnapshot(_coordinator.Snapshot);
        }
        catch (OperationCanceledException)
        { if (!_disposed) { _coordinator.SetSessions([]); ApplySnapshot(null); _status.Text = "读取媒体信息超时，稍后自动重试"; } }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            if (!_disposed) { _coordinator.SetSessions([]); ApplySnapshot(null); _status.Text = "媒体会话暂不可用，稍后自动重试"; }
        }
        finally { _polling = false; }
    }

    internal void ApplySnapshot(MediaSnapshot? snapshot)
    {
        var oldWidth = PreferredWidth;
        _display = snapshot;
        Width = PreferredWidth;
        _compactInfo.Visibility = _hoverControls.Visibility = snapshot == null ? Visibility.Collapsed : Visibility.Visible;
        _compactLayout.ColumnDefinitions[0].Width = new(snapshot == null ? 28 : 36);
        if (oldWidth != PreferredWidth) PresentationChanged?.Invoke(this, EventArgs.Empty);
        var track = snapshot?.Track;
        if (_track != track)
        {
            CancelLyrics(); _lyrics = null; _lastLyricText = null; _lastArtwork = null; _track = track; _seeking = false;
            _status.Text = snapshot == null ? "请先在播放器中开始播放。需要播放器支持 Windows 系统媒体控制。" : "可导入 LRC 歌词，或开启在线匹配";
            if (_settings.OnlineLyrics && snapshot != null) _ = FetchLyrics(snapshot);
        }
        _title.Text = string.IsNullOrWhiteSpace(snapshot?.Title) ? snapshot == null ? "没有正在播放的媒体" : "未命名媒体" : snapshot.Title;
        _artist.Text = snapshot == null ? "打开支持系统媒体控制的播放器" : string.IsNullOrWhiteSpace(snapshot.Artist) ? snapshot.SourceName : snapshot.Artist;
        _compactTitle.Text = snapshot == null ? "播放你的音乐" : _title.Text;
        _compactSubtitle.Text = snapshot == null ? "媒体控制 · 点击展开" : _artist.Text;
        _source.Content = snapshot == null ? "媒体来源 ▾" : ShortSource(snapshot.SourceName) + " ▾";
        ToolTip = snapshot == null ? "媒体控制 · 点击展开" : $"{snapshot.Title}\n{snapshot.Artist}\n{snapshot.SourceName}";
        foreach (var (button, command) in _buttons)
        {
            button.IsEnabled = !_commandBusy && snapshot?.Supports(command) == true;
            if (command == MediaCommand.TogglePlayback)
            {
                button.Content = snapshot?.IsPlaying == true ? "\uE769" : "\uE768";
                AutomationProperties.SetName(button, snapshot?.IsPlaying == true ? "暂停" : "播放");
                button.ToolTip = snapshot?.IsPlaying == true ? "暂停" : "播放";
            }
        }
        _seek.IsEnabled = snapshot?.Supports(MediaCommand.Seek) == true;
        if (!ReferenceEquals(_lastArtwork, snapshot?.Artwork) || snapshot?.Artwork == null)
        {
            _lastArtwork = snapshot?.Artwork;
            var artwork = DecodeArtwork(_lastArtwork) ?? FallbackArtwork(); _cover.Source = artwork; _cardCover.Source = artwork;
        }
        Tick();
    }

    private void Tick()
    {
        if (_disposed) return;
        var snapshot = _display;
        var position = snapshot == null ? 0 : MediaTimeline.PositionAt(snapshot, DateTimeOffset.UtcNow);
        if (!_seeking)
        {
            _seek.Minimum = snapshot?.MinSeek ?? 0; _seek.Maximum = Math.Max(_seek.Minimum + .001, snapshot?.MaxSeek ?? 1);
            _seek.Value = position;
        }
        _elapsed.Text = FormatTime(Math.Max(0, position - (snapshot?.Start ?? 0))); _duration.Text = FormatTime(snapshot?.Duration ?? 0);
        var lyricTime = position - (snapshot?.Start ?? 0);
        var index = _lyrics?.IndexAt(lyricTime) ?? -1;
        var line = index >= 0 ? _lyrics!.Lines[index] : null;
        var text = line?.Text ?? "";
        var highlight = _lyrics?.HighlightLength(index, lyricTime) ?? 0;
        if (_lastLyricIndex != index || _lastLyricText != text || _lastHighlight != highlight)
        {
            var lineChanged = _lastLyricIndex != index || _lastLyricText != text;
            _lastLyricIndex = index; _lastLyricText = text; _lastHighlight = highlight;
            _lyric.Inlines.Clear();
            _lyric.Inlines.Add(new Run(text[..highlight]) { Foreground = Accent });
            _lyric.Inlines.Add(new Run(text[highlight..]) { Foreground = Muted });
            _nextLyric.Text = _lyrics != null && index + 1 < _lyrics.Lines.Count ? _lyrics.Lines[index + 1].Text : "";
            if (lineChanged && !Reduced)
            {
                _lyric.BeginAnimation(OpacityProperty, new DoubleAnimation(.3, 1, TimeSpan.FromMilliseconds(180)));
                _lyricShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(5, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            }
        }
        _compactTitle.Text = string.IsNullOrWhiteSpace(text) ? snapshot == null ? "播放你的音乐" : _title.Text : text.Replace('\n', ' ');
    }

    private async Task Send(MediaCommand command, double position = 0)
    {
        if (_commandBusy || _disposed) return;
        _commandBusy = true; ApplySnapshot(_coordinator.Snapshot);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var success = await _coordinator.SendAsync(command, position, timeout.Token);
            if (!_disposed) _status.Text = success ? "" : "播放器不支持或未接受这个操作";
        }
        catch (OperationCanceledException) { if (!_disposed) _status.Text = "播放器响应超时，请重试"; }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
        { if (!_disposed) _status.Text = "操作未完成，播放器可能已关闭"; }
        finally { _commandBusy = false; if (!_disposed) { ApplySnapshot(_coordinator.Snapshot); await Poll(); } }
    }

    private void BeginSeek() { if (_seek.IsEnabled) { _seeking = true; _seekTrack = _coordinator.Snapshot?.Track; } }
    private async Task CommitSeek()
    {
        if (!_seeking) return;
        _seeking = false;
        if (_seekTrack == _coordinator.Snapshot?.Track) await Send(MediaCommand.Seek, _seek.Value);
    }

    private void OpenSources()
    {
        var menu = new ContextMenu();
        foreach (var session in _coordinator.Sessions)
        {
            var item = new MenuItem { Header = session.Name, IsCheckable = true, IsChecked = session.Id == _coordinator.Selected?.Id };
            item.Click += async (_, _) => { _coordinator.Select(session.Id); ApplySnapshot(null); await Poll(); };
            menu.Items.Add(item);
        }
        if (menu.Items.Count == 0) menu.Items.Add(new MenuItem { Header = "未发现媒体来源", IsEnabled = false });
        menu.PlacementTarget = _source; menu.IsOpen = true;
    }
    private void CycleSource(int direction)
    {
        var sessions = _coordinator.Sessions;
        if (sessions.Count < 2) return;
        var current = sessions.ToList().FindIndex(s => s.Id == _coordinator.Selected?.Id);
        _coordinator.Select(sessions[(current + direction + sessions.Count) % sessions.Count].Id);
        ApplySnapshot(null); _ = Poll();
    }
    internal void OpenPopup()
    {
        if (_disposed) return;
        Details.Width = Math.Min(360, Math.Max(220, SystemParameters.WorkArea.Width - 24));
        Details.MaxHeight = Math.Max(180, SystemParameters.WorkArea.Height - 32);
        _popup.PopupAnimation = Reduced ? PopupAnimation.None : PopupAnimation.Fade; _popup.IsOpen = true;
    }
    public void ClosePopup() => _popup.IsOpen = false;

    private async Task FetchLyrics(MediaSnapshot snapshot)
    {
        CancelLyrics(); var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); _lyricRequest = request;
        _status.Text = "正在匹配歌词…";
        try
        {
            var lyrics = await _lyricsClient.FindAsync(snapshot, request.Token);
            if (_disposed || request.IsCancellationRequested || _track != snapshot.Track || !_settings.OnlineLyrics) return;
            ApplyLyrics(lyrics, lyrics?.Lines.Count > 0 ? "歌词：LRCLIB" : "暂未匹配到同步歌词，可导入本地 LRC");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or ArgumentException)
        { if (!_disposed && !request.IsCancellationRequested && _track == snapshot.Track) _status.Text = "歌词匹配失败，可导入本地 LRC"; }
        finally { if (ReferenceEquals(_lyricRequest, request)) _lyricRequest = null; request.Dispose(); }
    }
    private void LoadLocalLyrics()
    {
        var track = _track;
        if (track == null) { _status.Text = "请先选择正在播放的媒体"; return; }
        var dialog = new OpenFileDialog { Title = "导入当前歌曲的同步歌词", Filter = "LRC 歌词 (*.lrc)|*.lrc", CheckFileExists = true };
        if (dialog.ShowDialog() != true || _track != track) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 1_000_000) throw new IOException("歌词文件超过 1 MB");
            var lyrics = LrcDocument.Parse(File.ReadAllText(dialog.FileName));
            CancelLyrics(); ApplyLyrics(lyrics, lyrics.Lines.Count == 0 ? "文件没有可识别的 LRC 时间戳" : "已加载本地同步歌词");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { _status.Text = "无法读取歌词：" + ex.Message; }
    }
    private void CancelLyrics() { _lyricRequest?.Cancel(); _lyricRequest = null; }
    internal void ApplyLyrics(LrcDocument? lyrics, string status)
    { _lyrics = lyrics; _lastLyricText = null; _status.Text = status; Tick(); }

    private void Hover(bool hovered)
    {
        hovered &= _display != null;
        _hoverControls.IsHitTestVisible = hovered;
        var duration = TimeSpan.FromMilliseconds(Reduced ? 0 : 140);
        _hoverControls.BeginAnimation(OpacityProperty, new DoubleAnimation(hovered ? 1 : 0, duration));
        _compactInfo.BeginAnimation(OpacityProperty, new DoubleAnimation(hovered ? 0 : 1, duration));
    }
    private StackPanel Controls(double size)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (command, glyph, name) in new[] { (MediaCommand.Previous, "\uE892", "上一首"), (MediaCommand.TogglePlayback, "\uE768", "播放"), (MediaCommand.Next, "\uE893", "下一首") })
        {
            var button = Button(glyph, name, async () => await Send(command));
            button.FontFamily = new FontFamily("Segoe MDL2 Assets"); button.Width = size; button.Height = size; button.FontSize = size > 30 ? 18 : 14;
            _buttons.Add((button, command)); panel.Children.Add(button);
        }
        return panel;
    }
    private static Button Button(string text, string label, Action action)
    {
        var button = new Button { Content = text, ToolTip = label, Foreground = Brushes.White, Background = Brushes.Transparent, BorderThickness = new(0), Padding = new(7, 5, 7, 5), Cursor = Cursors.Hand, FontSize = 11 };
        var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8)); border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new(RelativeSourceMode.TemplatedParent) });
        var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); content.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 5, 6, 5)); border.AppendChild(content);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 163, 198, 255)))); template.Triggers.Add(hover);
        var disabled = new Trigger { Property = IsEnabledProperty, Value = false }; disabled.Setters.Add(new Setter(OpacityProperty, .3)); template.Triggers.Add(disabled); button.Template = template;
        AutomationProperties.SetName(button, label); button.Click += (_, _) => action(); return button;
    }
    private static TextBlock Text(string value, double size, Brush color) => new() { Text = value, FontSize = size, Foreground = color, TextTrimming = TextTrimming.CharacterEllipsis };
    private static string ShortSource(string value) => value.Split('!')[0];
    private static string FormatTime(double seconds) => seconds >= 3600 ? TimeSpan.FromSeconds(seconds).ToString(@"h\:mm\:ss") : TimeSpan.FromSeconds(seconds).ToString(@"m\:ss");
    private static ImageSource? DecodeArtwork(byte[]? data)
    {
        if (data == null) return null;
        try { using var stream = new MemoryStream(data); var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 128; bitmap.DecodePixelHeight = 128; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap; }
        catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException or FormatException or COMException) { return null; }
    }
    private static ImageSource FallbackArtwork()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(new LinearGradientBrush(Color.FromRgb(75, 100, 169), Color.FromRgb(112, 71, 130), 40), null, new RectangleGeometry(new Rect(0, 0, 48, 48), 12, 12)));
        group.Children.Add(new GeometryDrawing(Brushes.White, null, Geometry.Parse("M20,13 L34,10 34,29 C34,36 24,36 24,31 C24,27 29,26 31,27 L31,16 23,18 23,33 C23,40 13,40 13,35 C13,31 18,30 20,31 Z")));
        var image = new DrawingImage(group); image.Freeze(); return image;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _poll.Stop(); _clock.Stop(); ClosePopup(); CancelLyrics(); _lifetime.Cancel(); _coordinator.Dispose(); _backend.Dispose(); _lyricsClient.Dispose(); _lifetime.Dispose();
    }
}
