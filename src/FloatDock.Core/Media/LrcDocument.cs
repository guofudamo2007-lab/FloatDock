using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FloatDock.Core.Media;

public sealed record LyricWord(double Time, string Text);
public sealed record LyricLine(double Time, string Text, IReadOnlyList<LyricWord> Words);

public sealed class LrcDocument
{
    private static readonly Regex LineTag = new(@"\[(\d{1,4}):([0-5]\d)(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);
    private static readonly Regex WordTag = new(@"<(\d{1,4}):([0-5]\d)(?:[.:](\d{1,3}))?>", RegexOptions.Compiled);
    private static readonly Regex OffsetTag = new(@"\[offset:([+-]?\d+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public IReadOnlyList<LyricLine> Lines { get; private init; } = [];
    public static LrcDocument Parse(string text)
    {
        if (text.Length > 1_000_000) throw new ArgumentException("LRC file is too large.", nameof(text));
        var offsetMatch = OffsetTag.Match(text);
        var offset = offsetMatch.Success && int.TryParse(offsetMatch.Groups[1].Value, out var ms) ? ms / 1000d : 0;
        var lines = new List<LyricLine>();
        foreach (var raw in text.Split('\n'))
        {
            var value = raw.Trim();
            var tags = LineTag.Matches(value);
            if (tags.Count == 0 || tags[0].Index != 0) continue;
            var end = 0;
            var times = new List<double>();
            foreach (Match tag in tags)
            {
                if (tag.Index != end) break;
                times.Add(Time(tag)); end = tag.Index + tag.Length;
            }
            var content = value[end..].Trim();
            var words = new List<LyricWord>();
            var wordTags = WordTag.Matches(content);
            var plain = new StringBuilder();
            if (wordTags.Count == 0) plain.Append(content);
            else
            {
                if (wordTags[0].Index > 0)
                {
                    var prefix = content[..wordTags[0].Index]; plain.Append(prefix); words.Add(new(times[0] - offset, prefix));
                }
                for (var i = 0; i < wordTags.Count; i++)
                {
                    var tag = wordTags[i];
                    var word = content[(tag.Index + tag.Length)..(i + 1 < wordTags.Count ? wordTags[i + 1].Index : content.Length)];
                    plain.Append(word); words.Add(new(Time(tag) - offset, word));
                }
            }
            foreach (var time in times)
                lines.Add(new(time - offset, plain.ToString(), words.Select(w => w with { Time = w.Time + time - times[0] }).ToArray()));
        }
        return new LrcDocument { Lines = lines.OrderBy(l => l.Time).GroupBy(l => l.Time).Select(group =>
            group.Count() == 1 ? group.First() : new LyricLine(group.Key, string.Join('\n', group.Select(l => l.Text)), [])).ToArray() };
    }
    public int IndexAt(double seconds)
    {
        if (!double.IsFinite(seconds)) return -1;
        var left = 0; var right = Lines.Count - 1; var result = -1;
        while (left <= right)
        {
            var middle = left + (right - left) / 2;
            if (Lines[middle].Time <= seconds) { result = middle; left = middle + 1; }
            else right = middle - 1;
        }
        return result;
    }
    public int HighlightLength(int index, double seconds)
    {
        if (index < 0 || index >= Lines.Count || seconds < Lines[index].Time) return 0;
        var line = Lines[index];
        if (line.Words.Count == 0) return line.Text.Length;
        var count = 0;
        foreach (var word in line.Words) { if (word.Time > seconds) break; count += word.Text.Length; }
        return Math.Min(count, line.Text.Length);
    }
    private static double Time(Match match) => double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 60
        + double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
        + (match.Groups[3].Success ? double.Parse("0." + match.Groups[3].Value, CultureInfo.InvariantCulture) : 0);
}
