using FloatDock.Core;

internal static class NativeTaskbarTests
{
    internal static void Run()
    {
        var free = NativeTaskbarPolicy.FreeRanges(1000, [new(400, 600), new(550, 680), new(900, 1100)]);
        Equal(new TaskbarRange(8, 392), free[0]); Equal(new TaskbarRange(688, 892), free[1]);
        Equal(new TaskbarRange(8, 392), NativeTaskbarPolicy.Select(free, 300));
        Equal(default(TaskbarRange), NativeTaskbarPolicy.Select(free, 400));
        Equal(new TaskbarRange(688, 850), NativeTaskbarPolicy.Select(free, 150, new(688, 850)));
        Equal(0, NativeTaskbarPolicy.FreeRanges(0, []).Count);
        var screen = new PixelRect(0, 0, 1920, 1080); var visible = new PixelRect(0, 1032, 1920, 1080);
        var state = NativeTaskbarPolicy.Observe(default, visible, screen);
        Equal(true, NativeTaskbarPolicy.CanShow(state, true, new(0, 200), 190));
        state = NativeTaskbarPolicy.Observe(state, new(0, 1078, 1920, 1126), screen);
        Equal(false, NativeTaskbarPolicy.CanShow(state, true, new(0, 200), 190));
        state = NativeTaskbarPolicy.Observe(state, state.Last, screen); state = NativeTaskbarPolicy.Observe(state, state.Last, screen);
        Equal(true, state.Hidden);
        state = NativeTaskbarPolicy.Observe(state, visible, screen);
        state = NativeTaskbarPolicy.Observe(state, visible, screen); Equal(true, state.Moving);
        state = NativeTaskbarPolicy.Observe(state, visible, screen); Equal(false, state.Moving); Equal(false, state.Hidden);
        Equal(false, NativeTaskbarPolicy.CanShow(state, false, new(0, 200), 190));
    }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
}
