using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using FloatDock.Core;
using FloatDock.Windows;
using FloatDock.Windows.Media;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Window? host = null;
        try
        {
            if (!args.Contains("--desktop"))
            {
                if (args.Any(a => a is "--media-probe" or "--media-integration"))
                    throw new ArgumentException("Live media checks require explicit --desktop.");
                MediaViewTests.Run();
                DockViewTests.Run();
                FusionViewTests.Run();
                Console.WriteLine("PASS Offline-only suite: no Window.Show, taskbar mutation or live media access");
                return 0;
            }
            // The largest supported icon must not be cut off when hover and launch motion overlap.
            var icon = new DockIcon(new DockEntry("fixture", "Fixture", null, false, []), 64);
            host = new Window { Content = icon, Width = 160, Height = 220, Left = -10000, Top = -10000,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            host.Show();
            icon.Measure(new Size(icon.Width, icon.Height));
            icon.Arrange(new Rect(0, 0, icon.Width, icon.Height));
            icon.UpdateLayout();
            icon.SetState(0, false, false);
            icon.Bounce(false);
            Pump(TimeSpan.FromMilliseconds(140));
            var image = ((Grid)icon.Content).Children.OfType<Image>().Single();
            var bounds = image.TransformToAncestor(icon).TransformBounds(new Rect(0, 0, image.ActualWidth, image.ActualHeight));
            Check(bounds.Height > 64 && bounds.Top < 40, $"The test must observe live hover/bounce animation: {bounds}.");
            Check(bounds.Top >= 0, $"Largest icon clips above its viewport (top={bounds.Top:F2}).");
            Check(bounds.Bottom <= icon.ActualHeight, "Icon clips below its viewport.");
            var render = new RenderTargetBitmap((int)icon.Width, (int)icon.Height, 96, 96, PixelFormats.Pbgra32);
            render.Render(icon);
            var pixels = new byte[(int)icon.Width * (int)icon.Height * 4];
            render.CopyPixels(pixels, (int)icon.Width * 4, 0);
            Check(pixels.Where((_, i) => i % 4 == 3).Any(alpha => alpha > 200), "WPF must render visible icon pixels.");
            icon.SetState(0, true, true);
            var reducedBounds = image.TransformToAncestor(icon).TransformBounds(new Rect(0, 0, image.ActualWidth, image.ActualHeight));
            Check(Math.Abs(reducedBounds.Height - 64) < .01, "Reduced motion must cancel magnification immediately.");
            var popup = new Window { Owner = host, Width = 100, Height = 100, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                popup.Show();
                var ownerHandle = new WindowInteropHelper(host).Handle;
                var popupHandle = new WindowInteropHelper(popup).Handle;
                Check(WindowService.Representative(popupHandle, [new AppWindow(ownerHandle, "Owner", null)]) == ownerHandle,
                    "Foreground dialogs must keep their represented owner active.");
                Check(WindowService.Representative(popupHandle, []) == popupHandle, "Unrepresented windows must retain their own handle.");
            }
            finally { popup.Close(); }
            Console.WriteLine("PASS WPF largest-icon hover/bounce bounds, raster rendering and reduced-motion reset");
            Console.WriteLine("PASS Win32 owned-dialog foreground mapping");
            MediaViewTests.Run();
            DockViewTests.Run();
            if (args.Contains("--media-probe")) ProbeMedia();
            if (args.Contains("--media-integration")) MediaIntegrationTests.Run(new WindowInteropHelper(host).Handle, Pump);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex.Message); return 1; }
        finally { host?.Close(); }
    }

    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }

    private static void ProbeMedia()
    {
        using var backend = new WindowsMediaBackend();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = backend.GetSessionsAsync(timeout.Token);
        while (!pending.IsCompleted) Pump(TimeSpan.FromMilliseconds(50));
        var (sessions, current) = pending.GetAwaiter().GetResult();
        Console.WriteLine($"PASS Live GSMTC manager request: sessions={sessions.Count}, currentPresent={current != null}");
        foreach (var session in sessions.Take(3))
        {
            var read = session.ReadAsync(timeout.Token);
            while (!read.IsCompleted) Pump(TimeSpan.FromMilliseconds(50));
            var state = read.GetAwaiter().GetResult();
            Console.WriteLine($"PASS Live session metadata: titlePresent={!string.IsNullOrEmpty(state.Title)}, artworkPresent={state.Artwork != null}, canToggle={state.CanToggle}, canSeek={state.CanSeek}");
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
