using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FloatDock.Windows;
using FloatDock.Windows.Taskbar;

internal static class FusionViewTests
{
    internal static void Run()
    {
        var window = new FusionWindow(new DockSettings());
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(580, 510)); root.Arrange(new Rect(0, 0, 580, 510)); root.UpdateLayout();
        var buttons = Descendants(root).OfType<Button>().ToArray();
        foreach (var label in new[] { "启动媒体栏", "停止媒体栏", "退出 FloatDock" })
        {
            var button = buttons.Single(b => Equals(b.Content, label));
            var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize));
            Check(bounds.Width > 0 && bounds.Bottom <= 510, label + " must be discoverable without scrolling.");
        }
        Check(new WindowInteropHelper(window).Handle == 0, "Control-window layout must not create an HWND.");
        if (Environment.GetEnvironmentVariable("FLOATDOCK_RENDER_DIR") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            var bitmap = new RenderTargetBitmap(580, 510, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, "fusion-control.png")); encoder.Save(stream);
        }
        var media = new NativeMediaHost(new DockSettings(), () => { });
        Check(new WindowInteropHelper(media).Handle == 0 && media.Opacity == 0, "Native host must stay inert until explicitly shown.");
        media.Close(); window.Close();
        Console.WriteLine("PASS Fusion control layout, visible start/stop/exit and inert native host construction");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var item in Descendants(child)) yield return item; } }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
