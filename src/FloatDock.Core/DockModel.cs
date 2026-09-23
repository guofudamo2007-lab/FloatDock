namespace FloatDock.Core;

public sealed record AppPin(string Name, string Path);
public sealed record AppWindow(nint Handle, string Title, string? Path);
public sealed record DockEntry(string Key, string Name, string? Path, bool IsPinned, IReadOnlyList<AppWindow> Windows);
public readonly record struct MotionTarget(double Scale, double Lift);

public static class DockModel
{
    public static IReadOnlyList<DockEntry> Merge(IEnumerable<AppPin> pins, IEnumerable<AppWindow> windows)
    {
        var entries = new Dictionary<string, DockEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var pin in pins)
            if (!string.IsNullOrWhiteSpace(pin.Path))
                entries.TryAdd(pin.Path, new(pin.Path, pin.Name, pin.Path, true, []));
        foreach (var window in windows.OrderBy(w => w.Path ?? $"window:{w.Handle}", StringComparer.OrdinalIgnoreCase).ThenBy(w => (long)w.Handle))
        {
            var key = string.IsNullOrWhiteSpace(window.Path) ? $"window:{window.Handle}" : window.Path;
            if (!entries.TryGetValue(key, out var entry))
                entry = new(key, window.Title, window.Path, false, []);
            entries[key] = entry with { Windows = [..entry.Windows, window] };
        }
        return entries.Values.ToArray();
    }

    public static nint NextWindow(IReadOnlyList<AppWindow> windows, nint foreground)
    {
        if (windows.Count == 0) return 0;
        for (var i = 0; i < windows.Count; i++)
            if (windows[i].Handle == foreground) return windows[(i + 1) % windows.Count].Handle;
        return windows[0].Handle;
    }

    public static MotionTarget Motion(double distance, bool active, bool reducedMotion)
    {
        if (reducedMotion) return new(1, 0);
        // Cosine falloff ported from Taskbar Dock Animation Plus (incconutwo / Ph0en1x-dev, MIT).
        var influence = !double.IsFinite(distance) || Math.Abs(distance) >= 100 ? 0 : (Math.Cos(Math.Abs(distance) / 100 * Math.PI) + 1) / 2;
        return new(1 + .28 * influence, Math.Max(active ? 5 : 0, 16 * influence));
    }

    // Upstream's centered cumulative expansion, with full spacing to prevent icon overlap.
    public static double[] NeighborShifts(IReadOnlyList<double> centers, double pointer, double iconSize, bool reducedMotion)
    {
        var extra = centers.Select(x => (Motion(x - pointer, false, reducedMotion).Scale - 1) * Math.Max(0, iconSize)).ToArray();
        var offset = -extra.Sum() / 2;
        var shifts = new double[extra.Length];
        for (var i = 0; i < extra.Length; i++) { shifts[i] = offset + extra[i] / 2; offset += extra[i]; }
        return shifts;
    }

    public static double ViewportWidth(int count, double itemWidth, double availableWidth)
        => Math.Max(0, Math.Min(Math.Max(0, count) * Math.Max(0, itemWidth), availableWidth));

    public static string WindowLabel(string title, string? processName)
        => !string.IsNullOrWhiteSpace(title) ? title : !string.IsNullOrWhiteSpace(processName) ? processName : "Application";

    public static bool ShouldHideForFullscreen(bool coversMonitor, bool maximized, bool hasCaption)
        => coversMonitor && !(maximized && hasCaption);
}
