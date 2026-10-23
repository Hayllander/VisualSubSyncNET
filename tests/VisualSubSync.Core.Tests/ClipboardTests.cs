using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Tests;

public class ClipboardTests
{
    [Fact]
    public void Vss_format_round_trips_every_field()
    {
        var sub = new Subtitle
        {
            StartMs = 1000, StopMs = 2500, Text = "Olá\r\nmundo", Effect = "fx", Layer = "2", Marked = "Marked=1",
            Actor = "Ana", MarginR = "0010", MarginL = "0020", MarginV = "0030", Style = "Top",
        };
        byte[] data = SubtitleClipboard.Serialize([sub]);
        Assert.Equal(4 + 4 + 4 + 4 + (9 * 4) + (2 * (10 + 2 + 1 + 8 + 3 + 12 + 3)), data.Length);
        var back = Assert.Single(SubtitleClipboard.Deserialize(data));
        Assert.Equal((sub.StartMs, sub.StopMs, sub.Text, sub.Effect, sub.Layer, sub.Marked, sub.Actor),
            (back.StartMs, back.StopMs, back.Text, back.Effect, back.Layer, back.Marked, back.Actor));
        Assert.Equal((sub.MarginR, sub.MarginL, sub.MarginV, sub.Style), (back.MarginR, back.MarginL, back.MarginV, back.Style));
        Assert.Empty(SubtitleClipboard.Deserialize([2, 0, 0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void Text_is_srt_and_pastes_back()
    {
        var a = new Subtitle { StartMs = 1000, StopMs = 2000, Text = "One" };
        var b = new Subtitle { StartMs = 3000, StopMs = 4000, Text = "Two" };
        string text = SubtitleClipboard.ToText([(5, a), (6, b)]);
        Assert.Equal("5\r\n00:00:01,000 --> 00:00:02,000\r\nOne\r\n\r\n6\r\n00:00:03,000 --> 00:00:04,000\r\nTwo", text);
        var pasted = SubtitleClipboard.ParseText(text);
        Assert.Equal([(1000, "One"), (3000, "Two")], pasted.Select(s => (s.StartMs, s.Text)));

        SubtitleClipboard.MoveToCursor(pasted, 500);
        Assert.Equal([(500, 1500), (2500, 3500)], pasted.Select(s => (s.StartMs, s.StopMs)));
    }

    [Fact]
    public void Ssa_events_paste_when_text_is_not_srt()
    {
        var pasted = SubtitleClipboard.ParseText(
            "[Events]\r\nFormat: Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\n" +
            "Dialogue: Marked=0,0:00:19.72,0:00:20.29,Default,,0000,0000,0000,,just a\\Nsample\r\n");
        var sub = Assert.Single(pasted);
        Assert.Equal((19720, 20290, "just a\r\nsample", "Default"), (sub.StartMs, sub.StopMs, sub.Text, sub.Style));
    }

    [Fact]
    public void Ass_events_without_header_keep_the_pasted_layer()
    {
        var pasted = SubtitleClipboard.ParseText(
            "[Events]\r\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\n" +
            "Dialogue: 2,0:00:01.00,0:00:02.00,Default,,0000,0000,0000,,layered\r\n");
        var sub = Assert.Single(pasted);
        Assert.Equal(("2", "Marked=0"), (sub.Layer, sub.Marked));
    }

    [Fact]
    public void Pasted_srt_keeps_the_text_order_for_paste_at_cursor()
    {
        var pasted = SubtitleClipboard.ParseText(
            "1\r\n00:00:05,000 --> 00:00:06,000\r\nLater\r\n\r\n2\r\n00:00:01,000 --> 00:00:02,000\r\nEarlier\r\n");
        Assert.Equal(["Later", "Earlier"], pasted.Select(s => s.Text));

        SubtitleClipboard.MoveToCursor(pasted, 10_000);
        Assert.Equal([10_000, 6_000], pasted.Select(s => s.StartMs));
    }
}
