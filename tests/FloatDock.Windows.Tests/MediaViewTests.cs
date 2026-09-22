using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FloatDock.Core.Media;
using FloatDock.Windows;
using FloatDock.Windows.Media;

internal static class MediaViewTests
{
    internal static void Run()
    {
        using var capsule = new MediaCapsule(new DockSettings(), () => { });
        var fixture = new MediaSnapshot { SessionId = "fixture", SourceName = "FloatDock test player", Title = "FloatDock · Media capsule preview",
            Artist = "Synthetic UI fixture — not live playback", CanToggle = true, CanNext = true, CanPrevious = false,
            CanSeek = true, Start = 0, End = 185, MaxSeek = 185, Position = 42, IsPlaying = true, UpdatedAt = DateTimeOffset.UtcNow };
        capsule.ApplySnapshot(fixture);
        capsule.ApplyLyrics(LrcDocument.Parse("[00:40.00]<00:40.00>Float <00:42.00>into <00:45.00>your flow\n[00:48.00]A quieter place for your music"), "合成歌词示例 · 非真实播放");
        Layout(capsule, 260, 64); Layout(capsule.Details, 360, 650);
        var buttons = Descendants(capsule.Details).OfType<Button>().ToArray();
        Check(buttons.Single(b => AutomationProperties.GetName(b) == "暂停").IsEnabled, "Play/pause must be enabled for a capable source.");
        Check(!buttons.Single(b => AutomationProperties.GetName(b) == "上一首").IsEnabled, "Unsupported previous must remain disabled.");
        var slider = Descendants(capsule.Details).OfType<Slider>().Single();
        Check(slider.IsEnabled && slider.Value >= 42 && slider.Value < 44, "Timeline must reflect the media snapshot.");
        Check(capsule.Details.DesiredSize.Height <= 650, "Media details should fit a conventional desktop height.");
        Check(Descendants(capsule).OfType<TextBlock>().Any(t => t.Text == "Float into your flow"), "Compact capsule must show the current synced lyric.");
        if (Environment.GetEnvironmentVariable("FLOATDOCK_RENDER_DIR") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            Render(capsule, Path.Combine(output, "media-capsule.png")); Render(capsule.Details, Path.Combine(output, "media-card.png"));
        }
        capsule.ApplySnapshot(null);
        Check(!Descendants(capsule).OfType<TextBlock>().Any(t => t.Text == "Float into your flow"), "Changing tracks must clear stale lyrics.");
        Check(!slider.IsEnabled, "Controls must disable when the session disappears.");
        Check(buttons.Where(b => AutomationProperties.GetName(b) is "播放" or "上一首" or "下一首").All(b => !b.IsEnabled), "No transport controls may stay enabled without a session.");
        capsule.ApplySnapshot(fixture with { Position = 1, IsPlaying = false });
        capsule.ApplyLyrics(LrcDocument.Parse("[00:01]Repeat\n[00:02]Repeat\n[00:03]Next"), "Repeated-line regression");
        capsule.ApplySnapshot(fixture with { Position = 2, IsPlaying = false });
        Check(Descendants(capsule.Details).OfType<TextBlock>().Any(t => t.Text == "Next"), "Entering a repeated lyric must still update the next-line preview.");
        capsule.ApplySnapshot(fixture with { Position = 1, IsPlaying = false });
        Check(!Descendants(capsule.Details).OfType<TextBlock>().Any(t => t.Text == "Next"), "Seeking backward through repeated lyrics must restore the next-line preview.");
        capsule.Dispose(); capsule.Dispose();
        Console.WriteLine("PASS Media capsule/card rendering, capability gating, timeline and empty state");
    }
    private static void Layout(FrameworkElement element, double width, double height)
    { element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height)); element.UpdateLayout(); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
    }
    private static void Render(FrameworkElement element, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); encoder.Save(file);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
