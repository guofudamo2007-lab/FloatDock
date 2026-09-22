using FloatDock.Core;

var tests = new (string Name, Action Run)[]
{
    ("LRC handles repeated timestamps, translations, invalid rows and backward seeking", LyricTests.Parse),
    ("Enhanced LRC uses supplied word times instead of fabricated timing", LyricTests.Words),
    ("Media timeline handles pause, speed, future timestamps and seek limits", MediaTests.Timeline),
    ("Media commands target only the selected capable session and preserve refusal", MediaTests.Routing),
    ("Late media reads cannot overwrite selection or survive disposal", MediaTests.Stale),
    ("Pins and windows merge case-insensitively without duplicate pins", () => {
        var entries = DockModel.Merge([new("Editor", @"C:\Apps\edit.exe"), new("Duplicate", @"c:\apps\EDIT.exe")],
            [new(1, "Document A", @"c:\apps\edit.exe"), new(2, "Document B", @"C:\Apps\EDIT.exe")]);
        Equal(1, entries.Count); Equal(2, entries[0].Windows.Count); Equal("Editor", entries[0].Name); Equal(true, entries[0].IsPinned);
    }),
    ("Pins stay in user order and stay present after their windows close", () => {
        var entries = DockModel.Merge([new("Z", "z.exe"), new("A", "a.exe")], [new(1, "B", "b.exe")]);
        Equal("Z,A,B", string.Join(',', entries.Select(e => e.Name)));
        Equal(2, DockModel.Merge([new("Z", "z.exe"), new("A", "a.exe")], []).Count);
    }),
    ("Windows without accessible executable paths remain distinct", () => {
        var entries = DockModel.Merge([], [new(4, "One", null), new(5, "Two", null)]);
        Equal(2, entries.Count); Equal(false, entries[0].Key == entries[1].Key);
    }),
    ("Clicking cycles from the active window and wraps", () => {
        AppWindow[] windows = [new(11, "One", null), new(22, "Two", null)];
        Equal((nint)22, DockModel.NextWindow(windows, 11)); Equal((nint)11, DockModel.NextWindow(windows, 22));
        Equal((nint)11, DockModel.NextWindow(windows, 99)); Equal((nint)0, DockModel.NextWindow([], 0));
    }),
    ("Hover lift decays with distance and active state remains lifted", () => {
        var hover = DockModel.Motion(0, false, false); var neighbor = DockModel.Motion(65, false, false);
        var rest = DockModel.Motion(double.PositiveInfinity, false, false); var active = DockModel.Motion(double.PositiveInfinity, true, false);
        Check(hover.Scale > neighbor.Scale && neighbor.Scale > rest.Scale, "scale falloff");
        Check(hover.Lift > neighbor.Lift && neighbor.Lift > rest.Lift, "lift falloff");
        Check(active.Lift > rest.Lift, "active app remains lifted"); Equal(new MotionTarget(1, 0), DockModel.Motion(0, true, true));
    }),
    ("Viewport stays inside the display and rejects negative sizes", () => {
        Equal(180d, DockModel.ViewportWidth(3, 60, 900)); Equal(400d, DockModel.ViewportWidth(30, 60, 400));
        Equal(0d, DockModel.ViewportWidth(0, 60, 400)); Equal(0d, DockModel.ViewportWidth(3, 60, -100));
    }),
    ("Untitled windows remain identifiable with a process or generic fallback", () => {
        Equal("editor", DockModel.WindowLabel("", "editor"));
        Equal("Application", DockModel.WindowLabel(" ", null));
        Equal("Document", DockModel.WindowLabel("Document", "editor"));
    }),
    ("Fullscreen detection handles maximized borderless apps without hiding for normal maximized apps", () => {
        Equal(false, DockModel.ShouldHideForFullscreen(true, true, true));
        Equal(true, DockModel.ShouldHideForFullscreen(true, true, false));
        Equal(true, DockModel.ShouldHideForFullscreen(true, false, false));
        Equal(false, DockModel.ShouldHideForFullscreen(false, false, false));
    })
};
var failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} passed");
return failed == 0 ? 0 : 1;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
