using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Tests;

public class KaraokeTests
{
    [Fact]
    public void Split_separates_words_and_times()
    {
        var (words, times) = Karaoke.Split("{\\k10}Hel{\\K20}lo");
        Assert.Equal(["", "Hel", "lo"], words);
        Assert.Equal([10, 20], times);
    }

    [Fact]
    public void Split_keeping_tags_leaves_room_for_the_numbers()
    {
        var (words, times) = Karaoke.SplitKeepingTags("{\\k10}Hel{\\k20}lo");
        Assert.Equal(["{\\k", "}Hel{\\k", "}lo"], words);
        Assert.Equal([10, 20], times);
    }

    [Fact]
    public void Sub_times_come_from_the_text_without_the_last_one()
    {
        var sub = new Subtitle { StartMs = 1000, StopMs = 2000, Text = "{\\k10}a {\\k20}b {\\k70}c" };
        Assert.True(Karaoke.UpdateSubTimesFromText(sub));
        Assert.Equal([1100, 1300], sub.SubTimes);
        Assert.False(Karaoke.UpdateSubTimesFromText(sub));
        sub.Text = "abc";
        Assert.True(Karaoke.UpdateSubTimesFromText(sub));
        Assert.Empty(sub.SubTimes);
    }

    [Fact]
    public void Create_splits_on_spaces_and_slashes_by_letter_count()
    {
        var sub = new Subtitle { StartMs = 0, StopMs = 1000, Text = "ab c/d" };
        Karaoke.Create(sub);
        // 5 caracteres (o espaço conta): 200 ms cada.
        Assert.Equal("{\\k60}ab {\\k20}c{\\k20}d", sub.Text);
        Assert.Equal([600, 800], sub.SubTimes);
    }

    [Fact]
    public void Clear_removes_tags_and_markers()
    {
        var sub = new Subtitle { Text = "{\\k50}ab {\\k25}c", SubTimes = [500] };
        Karaoke.Clear(sub);
        Assert.Equal("ab c", sub.Text);
        Assert.Empty(sub.SubTimes);
    }

    [Fact]
    public void Text_is_rewritten_from_markers()
    {
        var sub = new Subtitle { StartMs = 0, StopMs = 1000, Text = "{\\k50}ab {\\k25}c{\\k25}d", SubTimes = [400, 800] };
        Assert.Equal("{\\k40}ab {\\k40}c{\\k20}d", Karaoke.TextFromSubTimes(sub));
        sub.SubTimes = [400];
        Assert.Equal(sub.Text, Karaoke.TextFromSubTimes(sub));
    }

    [Fact]
    public void Cursors_are_compressed_at_the_end()
    {
        var sub = new Subtitle { StartMs = 0, StopMs = 1000, Text = "{\\k50}ab {\\k25}c{\\k25}d", SubTimes = [500, 750] };
        var result = Karaoke.PlaceCursorsAtEnd(sub);
        Assert.NotNull(result);
        Assert.Equal([800, 900], result.Value.SubTimes);
        Assert.Equal("{\\k80}ab {\\k10}c{\\k10}d", result.Value.Text);
        Assert.Null(Karaoke.PlaceCursorsAtEnd(new Subtitle { StartMs = 0, StopMs = 150, SubTimes = [50, 100] }));
    }

    [Fact]
    public void Sub_time_ranges_cover_start_markers_and_stop()
    {
        int[] subs = [400, 800];
        Assert.Equal(0, Karaoke.GetSubTimeRange(0, 1000, subs, 100, out int s, out int e));
        Assert.Equal((0, 400), (s, e));
        Assert.Equal(2, Karaoke.GetSubTimeRange(0, 1000, subs, 900, out s, out e));
        Assert.Equal((800, 1000), (s, e));
        Assert.Equal(-1, Karaoke.GetSubTimeRange(0, 1000, subs, 1100, out _, out _));
        Assert.True(Karaoke.GetSubTimeRangeAt(0, 1000, subs, 1, out s, out e));
        Assert.Equal((400, 800), (s, e));
    }

    [Fact]
    public void Clone_copies_the_markers()
    {
        var sub = new Subtitle { SubTimes = [1, 2] };
        var clone = sub.Clone();
        clone.SubTimes[0] = 9;
        Assert.Equal(1, sub.SubTimes[0]);
    }
}
