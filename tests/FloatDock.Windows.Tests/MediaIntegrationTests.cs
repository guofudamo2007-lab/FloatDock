using FloatDock.Core.Media;
using FloatDock.Windows.Media;
using Windows.Media;

// Opt-in desktop integration test. Publishes an owned, silent SMTC fixture and only controls that fixture.
internal static class MediaIntegrationTests
{
    internal static void Run(nint window, Action<TimeSpan> pump)
    {
        var smtc = SystemMediaTransportControlsInterop.GetForWindow(window);
        var uniqueTitle = "FloatDock integration " + Guid.NewGuid().ToString("N");
        var button = new TaskCompletionSource<SystemMediaTransportControlsButton>(TaskCreationOptions.RunContinuationsAsynchronously);
        var seek = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnButton(SystemMediaTransportControls _, SystemMediaTransportControlsButtonPressedEventArgs e) => button.TrySetResult(e.Button);
        void OnSeek(SystemMediaTransportControls _, PlaybackPositionChangeRequestedEventArgs e) => seek.TrySetResult(e.RequestedPlaybackPosition.TotalSeconds);
        smtc.ButtonPressed += OnButton;
        smtc.PlaybackPositionChangeRequested += OnSeek;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var backend = new WindowsMediaBackend();
        try
        {
            smtc.IsEnabled = true; smtc.IsPlayEnabled = true; smtc.IsPauseEnabled = true;
            smtc.IsNextEnabled = true; smtc.IsPreviousEnabled = true;
            smtc.DisplayUpdater.Type = MediaPlaybackType.Music;
            smtc.DisplayUpdater.MusicProperties.Title = uniqueTitle;
            smtc.DisplayUpdater.MusicProperties.Artist = "Synthetic silent test";
            smtc.DisplayUpdater.Update();
            smtc.PlaybackStatus = MediaPlaybackStatus.Playing;
            smtc.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties {
                StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromSeconds(180), Position = TimeSpan.FromSeconds(30),
                MinSeekTime = TimeSpan.Zero, MaxSeekTime = TimeSpan.FromSeconds(180) });
            IMediaSession? fixture = null;
            while (fixture == null)
            {
                timeout.Token.ThrowIfCancellationRequested();
                var list = Await(backend.GetSessionsAsync(timeout.Token));
                foreach (var session in list.Sessions)
                    if (Await(session.ReadAsync(timeout.Token)).Title == uniqueTitle) { fixture = session; break; }
                if (fixture == null) pump(TimeSpan.FromMilliseconds(100));
            }
            var snapshot = Await(fixture.ReadAsync(timeout.Token));
            Check(snapshot.IsPlaying && snapshot.CanToggle && snapshot.CanSeek, "Published capabilities must reach the real GSMTC backend.");
            var repeated = Await(backend.GetSessionsAsync(timeout.Token));
            Check(repeated.Sessions.Any(s => ReferenceEquals(s, fixture)), "Session identity must remain stable across enumerations.");
            Check(Await(fixture.SendAsync(MediaCommand.TogglePlayback, 0, timeout.Token)), "Native toggle command was refused.");
            Check(Await(button.Task) == SystemMediaTransportControlsButton.Pause, "Playing fixture must receive pause.");
            Check(Await(fixture.SendAsync(MediaCommand.Seek, 65, timeout.Token)), "Native seek command was refused.");
            Check(Math.Abs(Await(seek.Task) - 65) < .01, "Native seek must arrive in the correct time units.");
            Console.WriteLine("PASS Native SMTC fixture: discovery, metadata, stable identity, toggle delivery and seek delivery (no audio)");
        }
        finally
        {
            smtc.IsEnabled = false; smtc.PlaybackStatus = MediaPlaybackStatus.Closed;
            smtc.DisplayUpdater.ClearAll(); smtc.DisplayUpdater.Update();
            smtc.ButtonPressed -= OnButton; smtc.PlaybackPositionChangeRequested -= OnSeek;
        }
        T Await<T>(Task<T> task)
        {
            while (!task.IsCompleted) { timeout.Token.ThrowIfCancellationRequested(); pump(TimeSpan.FromMilliseconds(25)); }
            return task.GetAwaiter().GetResult();
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
