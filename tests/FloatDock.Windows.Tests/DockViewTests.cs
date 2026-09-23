using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FloatDock.Core;
using FloatDock.Windows;
using FloatDock.Windows.Media;

internal static class DockViewTests
{
    internal static void Run()
    {
        // Render the complete production layout without creating an HWND or changing shell state.
        var window = new DockWindow(new DockSettings { ReplaceTaskbar = false });
        var root = (Grid)window.Content;
        var icons = (StackPanel)root.Children.OfType<ScrollViewer>().Single().Content;
        foreach (var (name, color) in new[] { ("Files", "#E5B95A"), ("Browser", "#6DABFF"), ("Editor", "#79CFBA"), ("Chat", "#AA98E5"), ("Music", "#DB8CA9") })
        {
            var icon = new DockIcon(new DockEntry(name, name, null, false, [new(0, name, null)]), 40);
            var drawing = new DrawingGroup(); var brush = (Brush)new BrushConverter().ConvertFromString(color)!;
            drawing.Children.Add(new GeometryDrawing(brush, null, new RectangleGeometry(new Rect(0, 0, 40, 40), 10, 10)));
            var text = new FormattedText(name[..1], System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 22, Brushes.White, 1);
            drawing.Children.Add(new GeometryDrawing(Brushes.White, null, text.BuildGeometry(new Point(12, 4))));
            ((Grid)icon.Content).Children.OfType<Image>().Single().Source = new DrawingImage(drawing);
            icons.Children.Add(icon);
        }
        root.Measure(new Size(548, 132)); root.Arrange(new Rect(0, 0, 548, 132)); root.UpdateLayout();
        var buttons = Descendants(root).OfType<Button>().ToArray();
        Check(buttons.Any(b => Equals(b.Content, "退出") && b.Visibility == Visibility.Visible && b.ActualWidth > 0), "Exit must be visible directly on the dock.");
        Check(buttons.Any(b => AutomationProperties.GetName(b) == "设置"), "Settings must have a dedicated discoverable button.");
        var media = root.Children.OfType<MediaCapsule>().Single();
        Check(media.Width == 36 && media.ActualWidth == 36, "Empty media must collapse to a small icon.");
        var exit = buttons.Single(b => Equals(b.Content, "退出"));
        var exitBounds = exit.TransformToAncestor(root).TransformBounds(new Rect(exit.RenderSize));
        Check(exitBounds.Right <= root.ActualWidth && exitBounds.Bottom <= root.ActualHeight, "Exit must not be cropped.");
        if (Environment.GetEnvironmentVariable("FLOATDOCK_RENDER_DIR") is { Length: > 0 } output)
        {
            var bitmap = new RenderTargetBitmap(548, 132, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            Directory.CreateDirectory(output); using var file = File.Create(Path.Combine(output, "dock-v0.3.png")); encoder.Save(file);
        }
        media.Dispose(); window.Close();
        Console.WriteLine("PASS Complete dock construction/layout, visible exit/settings and collapsed empty media");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var item in Descendants(child)) yield return item; } }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
