using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Tests;

public class InsertedFileTests
{
    [Fact]
    public void Lines_split_like_tstrings()
    {
        Assert.Equal(["a", "", "b"], InsertedFile.SplitLines("a\r\n\r\nb\r\n"));
        Assert.Equal(["a", "b", "c"], InsertedFile.SplitLines("a\nb\rc"));
        Assert.Empty(InsertedFile.SplitLines(""));
    }

    [Fact]
    public void Each_line_becomes_a_one_second_subtitle_every_two_seconds()
    {
        var subs = InsertedFile.FromLines("one\r\ntwo", 5000);
        Assert.Equal([(7000, 8000, "one"), (9000, 10000, "two")], subs.Select(s => (s.StartMs, s.StopMs, s.Text)));
    }

    [Fact]
    public void Without_subtitles_the_lines_start_at_99_hours()
    {
        Assert.Equal(InsertedFile.EmptyListStartMs, InsertedFile.FromLines("x", null)[0].StartMs);
    }
}
