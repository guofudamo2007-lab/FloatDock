namespace FloatDock.Core.Media;

public enum MediaCommand { Previous, TogglePlayback, Next, Seek }
public sealed record MediaTrackKey(string SessionId, string Title, string Artist, string Album);

public sealed record MediaSnapshot
{
    public string SessionId { get; init; } = "";
    public string SourceName { get; init; } = "";
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Album { get; init; } = "";
    public bool IsPlaying { get; init; }
    public bool CanPrevious { get; init; }
    public bool CanToggle { get; init; }
    public bool CanNext { get; init; }
    public bool CanSeek { get; init; }
    public double Start { get; init; }
    public double End { get; init; }
    public double Position { get; init; }
    public double MinSeek { get; init; }
    public double MaxSeek { get; init; }
    public double PlaybackRate { get; init; } = 1;
    public DateTimeOffset UpdatedAt { get; init; }
    public byte[]? Artwork { get; init; }
    public MediaTrackKey Track => new(SessionId, Title, Artist, Album);
    public double Duration => Math.Max(0, End - Start);
    public bool Supports(MediaCommand command) => command switch {
        MediaCommand.Previous => CanPrevious, MediaCommand.TogglePlayback => CanToggle,
        MediaCommand.Next => CanNext, MediaCommand.Seek => CanSeek && MaxSeek > MinSeek, _ => false
    };
}

public static class MediaTimeline
{
    public static double PositionAt(MediaSnapshot snapshot, DateTimeOffset now)
    {
        var elapsed = snapshot.IsPlaying && snapshot.UpdatedAt != default ? Math.Max(0, (now - snapshot.UpdatedAt).TotalSeconds) : 0;
        var rate = double.IsFinite(snapshot.PlaybackRate) ? Math.Max(0, snapshot.PlaybackRate) : 1;
        var position = snapshot.Position + elapsed * rate;
        return snapshot.End > snapshot.Start ? Math.Clamp(position, snapshot.Start, snapshot.End) : Math.Max(snapshot.Start, position);
    }
    public static double ClampSeek(MediaSnapshot snapshot, double requested)
        => snapshot.MaxSeek > snapshot.MinSeek
            ? Math.Clamp(double.IsFinite(requested) ? requested : snapshot.Position, snapshot.MinSeek, snapshot.MaxSeek)
            : snapshot.Position;
}

public interface IMediaSession
{
    string Id { get; }
    string Name { get; }
    Task<MediaSnapshot> ReadAsync(CancellationToken cancellationToken);
    Task<bool> SendAsync(MediaCommand command, double position, CancellationToken cancellationToken);
}
