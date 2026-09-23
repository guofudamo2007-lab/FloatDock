// Adapted from AF Media Bar (AmorFate, 2026): TaskbarFreeRangeCalculator,
// TaskbarMotionPolicy and TaskbarHostVisibilityPolicy. See THIRD-PARTY-NOTICES.md.
namespace FloatDock.Core;

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool Valid => Width > 0 && Height > 0;
}
public readonly record struct TaskbarRange(int Start, int End) { public int Length => End - Start; }
public readonly record struct TaskbarMotionState(bool Observed, PixelRect Last, int StableSamples, bool Moving, bool Hidden);

public static class NativeTaskbarPolicy
{
    public static IReadOnlyList<TaskbarRange> FreeRanges(int length, IEnumerable<TaskbarRange> occupied, int padding = 8, int gap = 8)
    {
        if (length <= 0) return [];
        padding = Math.Max(0, padding); gap = Math.Max(0, gap);
        var start = Math.Clamp(padding, 0, Math.Max(0, length));
        var end = Math.Clamp(length - padding, start, length);
        var ranges = occupied.Select(r => new TaskbarRange(Math.Clamp(r.Start - gap, start, end), Math.Clamp(r.End + gap, start, end)))
            .Where(r => r.Length > 0).OrderBy(r => r.Start);
        var free = new List<TaskbarRange>(); var cursor = start;
        foreach (var range in ranges)
        {
            if (range.Start > cursor) free.Add(new(cursor, range.Start));
            cursor = Math.Max(cursor, range.End);
        }
        if (cursor < end) free.Add(new(cursor, end));
        return free;
    }
    // Unlike upstream's compressed fallback, never place controls in an undersized gap.
    public static TaskbarRange Select(IReadOnlyList<TaskbarRange> ranges, int requiredWidth, TaskbarRange previous = default)
    {
        if (previous.Length >= requiredWidth && ranges.Any(r => r.Start <= previous.Start && r.End >= previous.End)) return previous;
        return ranges.FirstOrDefault(r => r.Length >= requiredWidth);
    }
    public static TaskbarMotionState Observe(TaskbarMotionState previous, PixelRect bar, PixelRect monitor)
    {
        if (!bar.Valid || !monitor.Valid) return previous;
        var hidden = Math.Min(bar.Bottom, monitor.Bottom) - Math.Max(bar.Top, monitor.Top) <= 4;
        if (!previous.Observed) return new(true, bar, 2, false, hidden);
        var changed = bar != previous.Last;
        var stable = changed ? 0 : Math.Min(2, previous.StableSamples + 1);
        var moving = changed || previous.Moving && stable < 2;
        return new(true, bar, stable, moving, moving ? previous.Hidden : hidden);
    }
    public static bool CanShow(TaskbarMotionState motion, bool probeReady, TaskbarRange range, int requiredWidth)
        => motion.Observed && !motion.Moving && !motion.Hidden && probeReady && requiredWidth > 0 && range.Length >= requiredWidth;
}
