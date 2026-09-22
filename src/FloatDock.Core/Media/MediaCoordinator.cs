namespace FloatDock.Core.Media;

public sealed class MediaCoordinator : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private int _revision;
    private bool _disposed;
    public IReadOnlyList<IMediaSession> Sessions { get; private set; } = [];
    public IMediaSession? Selected { get; private set; }
    public MediaSnapshot? Snapshot { get; private set; }
    public void SetSessions(IReadOnlyList<IMediaSession> sessions, string? preferredId = null)
    {
        if (_disposed) return;
        Sessions = sessions;
        SetSelected(sessions.FirstOrDefault(s => s.Id == Selected?.Id) ?? sessions.FirstOrDefault(s => s.Id == preferredId) ?? sessions.FirstOrDefault());
    }
    public void Select(string id)
    {
        if (!_disposed && Sessions.FirstOrDefault(s => s.Id == id) is { } session) SetSelected(session);
    }
    private void SetSelected(IMediaSession? session)
    {
        if (ReferenceEquals(Selected, session)) return;
        Selected = session; Snapshot = null; _revision++;
    }
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || Selected is not { } session) return;
        var revision = _revision;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var snapshot = await session.ReadAsync(linked.Token);
        if (!_disposed && !linked.IsCancellationRequested && revision == _revision && ReferenceEquals(session, Selected)) Snapshot = snapshot;
    }
    public async Task<bool> SendAsync(MediaCommand command, double position = 0, CancellationToken cancellationToken = default)
    {
        if (_disposed || Selected is not { } session || Snapshot is not { } snapshot || snapshot.SessionId != session.Id || !snapshot.Supports(command)) return false;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        return await session.SendAsync(command, command == MediaCommand.Seek ? MediaTimeline.ClampSeek(snapshot, position) : 0, linked.Token);
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _revision++; _lifetime.Cancel(); _lifetime.Dispose(); Sessions = []; Selected = null; Snapshot = null;
    }
}
