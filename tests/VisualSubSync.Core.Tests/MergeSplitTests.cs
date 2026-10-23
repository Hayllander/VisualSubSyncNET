using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Tests;

public class MergeSplitTests
{
    private static List<Subtitle> Subs() =>
    [
        new() { StartMs = 1000, StopMs = 3000, Text = "Hello ", Style = "Top", Actor = "Bob" },
        new() { StartMs = 2000, StopMs = 2500, Text = "  big" },
        new() { StartMs = 4000, StopMs = 5000, Text = "world\r\n" },
    ];

    [Fact]
    public void Merge_keeps_the_first_subtitle_and_the_latest_stop()
    {
        var merged = SubtitleEditRules.Merge(Subs(), MergeType.Normal);
        Assert.Equal((1000, 5000), (merged.StartMs, merged.StopMs));
        Assert.Equal("Hello \r\n  big\r\nworld", merged.Text);
        Assert.Equal(("Top", "Bob"), (merged.Style, merged.Actor));
    }

    [Fact]
    public void Merge_dialog_and_one_line_follow_MergeSubtitles()
    {
        Assert.Equal("- Hello \r\n-   big\r\n- world", SubtitleEditRules.Merge(Subs(), MergeType.Dialog).Text);
        Assert.Equal("Hello big world", SubtitleEditRules.Merge(Subs(), MergeType.OneLine).Text);
    }

    [Theory]
    [InlineData(1000, 3000, 2000, 120, 1880, 2000)] // com o espaço mínimo
    [InlineData(1000, 3000, 1050, 120, 1049, 1050)] // sem espaço: 1 ms antes do corte
    [InlineData(1000, 3000, 2000, 0, 1999, 2000)] // nunca se sobrepõem
    public void SplitTimes_follow_SplitSubtitle(int start, int stop, int split, int blank, int stop1, int start2) =>
        Assert.Equal((stop1, start2), SubtitleEditRules.SplitTimes(start, stop, split, blank));
}
