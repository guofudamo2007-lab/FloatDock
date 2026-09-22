using FloatDock.Core.Media;

internal static class MediaTests
{
    public static void Timeline()
    {
        var time = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var snapshot = new MediaSnapshot { Start = 5, End = 125, Position = 15, UpdatedAt = time, IsPlaying = true, PlaybackRate = 2, MinSeek = 10, MaxSeek = 120, CanSeek = true };
        Equal(25d, MediaTimeline.PositionAt(snapshot, time.AddSeconds(5)));
        Equal(15d, MediaTimeline.PositionAt(snapshot with { IsPlaying = false }, time.AddSeconds(5)));
        Equal(125d, MediaTimeline.PositionAt(snapshot, time.AddHours(1)));
        Equal(15d, MediaTimeline.PositionAt(snapshot, time.AddSeconds(-5)));
        Equal(10d, MediaTimeline.ClampSeek(snapshot, -30)); Equal(120d, MediaTimeline.ClampSeek(snapshot, 999));
        Equal(15d, MediaTimeline.ClampSeek(snapshot, double.NaN));
    }

    public static void Routing() => RoutingAsync().GetAwaiter().GetResult();
    private static async Task RoutingAsync()
    {
        var a = new TestSession("a"); var b = new TestSession("b");
        using var coordinator = new MediaCoordinator();
        coordinator.SetSessions([a, b]); coordinator.Select("b"); await coordinator.RefreshAsync();
        Equal("b", coordinator.Snapshot?.SessionId);
        Equal(true, await coordinator.SendAsync(MediaCommand.Next)); Equal(0, a.Commands.Count); Equal(1, b.Commands.Count);
        Equal(false, await coordinator.SendAsync(MediaCommand.Previous)); Equal(1, b.Commands.Count);
        Equal(true, await coordinator.SendAsync(MediaCommand.Seek, 999)); Equal(100d, b.Commands.Last().Position);
        b.AcceptCommands = false; Equal(false, await coordinator.SendAsync(MediaCommand.Next));
        coordinator.SetSessions([b, a], "a"); Equal("b", coordinator.Selected?.Id);
        coordinator.SetSessions([]); Equal<MediaSnapshot?>(null, coordinator.Snapshot);
        Equal(false, await coordinator.SendAsync(MediaCommand.Next));
    }

    public static void Stale() => StaleAsync().GetAwaiter().GetResult();
    private static async Task StaleAsync()
    {
        var wait = new TaskCompletionSource<MediaSnapshot>();
        var a = new TestSession("a") { Pending = wait.Task }; var b = new TestSession("b");
        using var coordinator = new MediaCoordinator(); coordinator.SetSessions([a, b]);
        var old = coordinator.RefreshAsync(); coordinator.Select("b"); await coordinator.RefreshAsync();
        wait.SetResult(new MediaSnapshot { SessionId = "a", Title = "stale" }); await old;
        Equal("b", coordinator.Snapshot?.SessionId);
        coordinator.Dispose(); Equal(false, await coordinator.SendAsync(MediaCommand.Next));
    }

    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
    private sealed class TestSession(string id) : IMediaSession
    {
        public string Id => id; public string Name => id;
        public List<(MediaCommand Command, double Position)> Commands { get; } = [];
        public bool AcceptCommands { get; set; } = true;
        public Task<MediaSnapshot>? Pending { get; init; }
        public Task<MediaSnapshot> ReadAsync(CancellationToken cancellationToken) => Pending ?? Task.FromResult(new MediaSnapshot { SessionId = id, CanToggle = true, CanNext = true, CanSeek = true, End = 100, MaxSeek = 100 });
        public Task<bool> SendAsync(MediaCommand command, double position, CancellationToken cancellationToken) { Commands.Add((command, position)); return Task.FromResult(AcceptCommands); }
    }
}
