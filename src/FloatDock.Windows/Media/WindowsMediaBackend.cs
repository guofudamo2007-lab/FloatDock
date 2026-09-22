using System.IO;
using System.Runtime.InteropServices;
using FloatDock.Core.Media;
using global::Windows.Media.Control;
using global::Windows.Storage.Streams;

namespace FloatDock.Windows.Media;

internal sealed class WindowsMediaBackend : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private readonly Dictionary<string, WindowsMediaSession> _sessions = [];
    private bool _disposed;

    public async Task<(IReadOnlyList<IMediaSession> Sessions, string? Current)> GetSessionsAsync(CancellationToken cancellationToken)
    {
        if (_disposed) return ([], null);
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed) return ([], null);
        var native = _manager.GetSessions();
        var result = new List<IMediaSession>();
        foreach (var session in native)
        {
            var id = Identity(session);
            if (!_sessions.TryGetValue(id, out var wrapper)) _sessions[id] = wrapper = new WindowsMediaSession(id, session);
            result.Add(wrapper);
        }
        var active = result.Select(s => s.Id).ToHashSet();
        foreach (var key in _sessions.Keys.Where(k => !active.Contains(k)).ToArray()) _sessions.Remove(key);
        var current = _manager.GetCurrentSession();
        return (result, current == null ? null : Identity(current));
    }

    // IUnknown identity is stable even if WinRT creates another managed projection wrapper.
    private static string Identity(GlobalSystemMediaTransportControlsSession session)
    {
        var inspectable = ((WinRT.IWinRTObject)session).NativeObject.ThisPtr;
        var iid = new Guid("00000000-0000-0000-C000-000000000046");
        Marshal.QueryInterface(inspectable, in iid, out var unknown);
        if (unknown == 0) throw new COMException("Cannot identify media session.");
        try { return $"{session.SourceAppUserModelId}:{unknown:X}"; }
        finally { Marshal.Release(unknown); }
    }
    public void Dispose() { _disposed = true; _sessions.Clear(); _manager = null; }
}

internal sealed class WindowsMediaSession(string id, GlobalSystemMediaTransportControlsSession session) : IMediaSession
{
    private MediaTrackKey? _artworkTrack;
    private byte[]? _artwork;
    public string Id => id;
    public string Name => session.SourceAppUserModelId;

    public async Task<MediaSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var properties = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var track = new MediaTrackKey(Id, properties.Title, properties.Artist, properties.AlbumTitle);
        if (_artworkTrack != track || _artwork == null)
        {
            _artworkTrack = track; _artwork = null;
            if (properties.Thumbnail != null)
            {
                try
                {
                    using var stream = await properties.Thumbnail.OpenReadAsync().AsTask(cancellationToken);
                    if (stream.Size is > 0 and <= 4_000_000)
                    {
                        using var reader = new DataReader(stream.GetInputStreamAt(0));
                        var read = await reader.LoadAsync((uint)stream.Size).AsTask(cancellationToken);
                        var data = new byte[read]; reader.ReadBytes(data); _artwork = data;
                    }
                }
                catch (Exception ex) when (ex is COMException or IOException) { /* Keep transport available without artwork. */ }
            }
        }
        return new MediaSnapshot {
            SessionId = Id, SourceName = Name, Title = properties.Title, Artist = properties.Artist, Album = properties.AlbumTitle,
            IsPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            CanPrevious = playback.Controls.IsPreviousEnabled, CanNext = playback.Controls.IsNextEnabled,
            CanToggle = playback.Controls.IsPlayPauseToggleEnabled,
            CanSeek = playback.Controls.IsPlaybackPositionEnabled,
            Start = timeline.StartTime.TotalSeconds, End = timeline.EndTime.TotalSeconds, Position = timeline.Position.TotalSeconds,
            MinSeek = timeline.MinSeekTime.TotalSeconds, MaxSeek = timeline.MaxSeekTime.TotalSeconds,
            UpdatedAt = timeline.LastUpdatedTime, PlaybackRate = playback.PlaybackRate ?? 1, Artwork = _artwork
        };
    }

    public async Task<bool> SendAsync(MediaCommand command, double position, CancellationToken cancellationToken)
        => command switch {
            MediaCommand.Previous => await session.TrySkipPreviousAsync().AsTask(cancellationToken),
            MediaCommand.TogglePlayback => await session.TryTogglePlayPauseAsync().AsTask(cancellationToken),
            MediaCommand.Next => await session.TrySkipNextAsync().AsTask(cancellationToken),
            MediaCommand.Seek => await session.TryChangePlaybackPositionAsync(TimeSpan.FromSeconds(position).Ticks).AsTask(cancellationToken),
            _ => false
        };
}
