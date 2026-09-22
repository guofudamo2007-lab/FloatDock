using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FloatDock.Core.Media;

namespace FloatDock.Windows.Media;

internal sealed class LyricsClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 1_000_000 };
    private readonly Dictionary<MediaTrackKey, LrcDocument?> _cache = [];

    public LyricsClient() => _http.DefaultRequestHeaders.UserAgent.ParseAdd("FloatDock/0.2 (+https://github.com/guofudamo2007-lab/FloatDock)");

    public async Task<LrcDocument?> FindAsync(MediaSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(snapshot.Track, out var cached)) return cached;
        if (string.IsNullOrWhiteSpace(snapshot.Title) || string.IsNullOrWhiteSpace(snapshot.Artist)) return null;
        var query = $"track_name={Uri.EscapeDataString(snapshot.Title)}&artist_name={Uri.EscapeDataString(snapshot.Artist)}";
        if (!string.IsNullOrWhiteSpace(snapshot.Album)) query += "&album_name=" + Uri.EscapeDataString(snapshot.Album);
        if (snapshot.Duration is >= 1 and <= 3600) query += "&duration=" + snapshot.Duration.ToString("0.###", CultureInfo.InvariantCulture);
        using var response = await _http.GetAsync("https://lrclib.net/api/get?" + query, cancellationToken);
        LrcDocument? result = null;
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (json.RootElement.TryGetProperty("syncedLyrics", out var lyrics) && lyrics.ValueKind == JsonValueKind.String)
                result = LrcDocument.Parse(lyrics.GetString()!);
        }
        if (_cache.Count >= 64) _cache.Clear();
        _cache[snapshot.Track] = result;
        return result;
    }
    public void Dispose() => _http.Dispose();
}
