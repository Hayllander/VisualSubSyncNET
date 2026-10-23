using VisualSubSync.Core.Subtitles.Formats;

namespace VisualSubSync.Core.Tests;

public class SrtFormatTests
{
    [Fact]
    public void Parse_and_format_roundtrip()
    {
        const string srt = "1\r\n00:00:01,000 --> 00:00:02,500\r\nOlá\r\nmundo\r\n\r\n2\r\n00:00:03,000 --> 00:00:04,000\r\nÇão\r\n\r\n";
        var doc = SrtFormat.Parse(srt);

        Assert.Equal(2, doc.Items.Count);
        Assert.Equal(1000, doc.Items[0].StartMs);
        Assert.Equal("Olá\r\nmundo", doc.Items[0].Text);
        Assert.False(doc.AutoCorrected);
        Assert.Equal(srt, SrtFormat.Format(doc));
    }

    [Fact]
    public void Tolerates_junk_missing_index_and_missing_blank_lines()
    {
        var doc = SrtFormat.Parse("lixo\r\n00:00:01,000 --> 00:00:02,000\r\nA\r\n00:00:03.000-->00:00:04.000\r\n   B   \r\n");

        Assert.Collection(doc.Items,
            s => Assert.Equal((1000, 2000, "A"), (s.StartMs, s.StopMs, s.Text)),
            s => Assert.Equal((3000, 4000, "B"), (s.StartMs, s.StopMs, s.Text)));
    }

    [Fact]
    public void Removes_next_index_but_keeps_number_on_last_subtitle()
    {
        var doc = SrtFormat.Parse(
            "1\r\n00:00:01,000 --> 00:00:02,000\r\nLinha\r\n\r\n2\r\n00:00:05,000 --> 00:00:06,000\r\nFim\r\n42");

        Assert.Equal("Linha", doc.Items[0].Text);
        Assert.Equal("Fim\r\n42", doc.Items[1].Text);
    }

    [Fact]
    public void Empty_subtitle_keeps_empty_text()
    {
        var doc = SrtFormat.Parse("1\r\n00:00:01,000 --> 00:00:02,000\r\n\r\n2\r\n00:00:03,000 --> 00:00:04,000\r\nB\r\n");

        Assert.Equal(string.Empty, doc.Items[0].Text);
        Assert.Equal("B", doc.Items[1].Text);
    }

    [Fact]
    public void Stop_equal_to_next_start_is_autocorrected()
    {
        var doc = SrtFormat.Parse("1\r\n00:00:01,000 --> 00:00:02,000\r\nA\r\n\r\n2\r\n00:00:02,000 --> 00:00:03,000\r\nB\r\n");

        Assert.Equal(1999, doc.Items[0].StopMs);
        Assert.Equal(3000, doc.Items[1].StopMs);
        Assert.True(doc.AutoCorrected);
    }

    [Fact]
    public void Accepts_lf_and_cr_line_endings_and_sorts()
    {
        var doc = SrtFormat.Parse("1\n00:00:05,000 --> 00:00:06,000\nB\n\n2\r00:00:01,000 --> 00:00:02,000\rA\r");

        Assert.Equal(["A", "B"], doc.Items.Select(s => s.Text));
    }

    [Fact]
    public void Ass_tags_become_html_when_converting_from_ass()
    {
        var doc = SrtFormat.Parse("1\r\n00:00:01,000 --> 00:00:02,000\r\n{\\i1}a{\\i0} {\\b1}b{\\b0} {\\u1}c{\\u0}\r\n");

        string srt = SrtFormat.Format(doc, new SubtitleWriteOptions { PreviousExtension = ".ass" });

        Assert.Contains("<i>a</i> <b>b</b> <u>c</u>\r\n", srt, StringComparison.Ordinal);
    }

    [Fact]
    public void Stripped_srt_keeps_only_timings()
    {
        var doc = SrtFormat.Parse("1\r\n00:00:01,000 --> 00:00:02,000\r\ntexto\r\n");

        Assert.Equal("1\r\n00:00:01,000 --> 00:00:02,000\r\n\r\n\r\n",
            SrtFormat.Format(doc, new SubtitleWriteOptions { Translate = SubtitleWriteOptions.StripText }));
    }
}
