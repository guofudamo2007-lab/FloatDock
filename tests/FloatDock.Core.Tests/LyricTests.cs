using FloatDock.Core.Media;

internal static class LyricTests
{
    public static void Parse()
    {
        var lyrics = LrcDocument.Parse("[ar:Fixture]\n[00:10.50][00:30.500]Repeated\n[00:02]First\ninvalid\n[00:02]Translation\n[00:99]invalid time");
        Equal(3, lyrics.Lines.Count); Equal("First\nTranslation", lyrics.Lines[0].Text);
        Equal(10.5, lyrics.Lines[1].Time); Equal(30.5, lyrics.Lines[2].Time);
        Equal(-1, lyrics.IndexAt(1.99)); Equal(0, lyrics.IndexAt(2)); Equal(1, lyrics.IndexAt(20)); Equal(0, lyrics.IndexAt(3));
        Equal(-1, LrcDocument.Parse("no timestamps").IndexAt(20));
        var early = LrcDocument.Parse("[offset:500]\n[00:02.00]Early");
        Equal(1.5, early.Lines[0].Time); Equal(0, early.IndexAt(1.5));
    }
    public static void Words()
    {
        var lyrics = LrcDocument.Parse("[00:01.00]<00:01.00>One <00:02.00>two\n[00:04.00]Next");
        Equal("One two", lyrics.Lines[0].Text); Equal(2, lyrics.Lines[0].Words.Count);
        Equal(4, lyrics.HighlightLength(0, 1.5)); Equal(7, lyrics.HighlightLength(0, 2.5));
        Equal(0, lyrics.HighlightLength(0, .5)); Equal(4, lyrics.HighlightLength(1, 5));
    }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
}
