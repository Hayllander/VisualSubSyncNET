using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Waveform;

namespace VisualSubSync.Core.Tests;

public class VoReferenceTests
{
    private static Subtitle Sub(int start, int stop, string text = "") => new() { StartMs = start, StopMs = stop, Text = text };

    [Fact]
    public void GetVoText_joins_the_vo_inside_the_subtitle_and_marks_the_neighbours()
    {
        var subs = new List<Subtitle> { Sub(0, 900), Sub(1000, 3000), Sub(5000, 6000) };
        var vo = new List<Subtitle>
        {
            Sub(100, 800, "before"),
            Sub(950, 990, "gap"),
            Sub(1100, 1900, "one\r\ntwo"),
            Sub(2000, 2900, "three|x"),
            Sub(3500, 4000, "after"),
            Sub(5100, 5900, "next sub"),
        };

        Assert.Equal("[<<<] one two three x [>>>]", VoReference.GetVoText(subs, 1, vo, enhanced: false));
        Assert.Equal("gap|one|two|three x|after", VoReference.GetVoText(subs, 1, vo, enhanced: true));
        Assert.Equal(string.Empty, VoReference.GetVoText(subs, 1, [], enhanced: true));
    }

    [Fact]
    public void MemoText_puts_previous_and_next_in_gray_around_the_current_text()
    {
        var memo = VoReference.MemoText("gap|one|two|three x|after");
        // Três linhas no meio, mas só sobram duas: o texto é quebrado de novo.
        Assert.Equal("gap [<<<]\r\none two\r\nthree x\r\n[>>>] after", memo.Text);
        Assert.Equal("gap [<<<]\r\n".Length, memo.CurrentStart);
        Assert.Equal(memo.Text.Length - "[>>>] after".Length, memo.CurrentStop);
    }

    [Fact]
    public void MemoText_rewraps_the_current_text_when_it_has_too_many_rows()
    {
        // Antes e depois ocupam 2 das 4 linhas; as 3 linhas do meio viram 2.
        var memo = VoReference.MemoText("a|aa bb|cc dd|ee ff|z");
        Assert.Equal("a [<<<]\r\naa bb cc\r\ndd ee ff\r\n[>>>] z", memo.Text);
    }

    [Fact]
    public void MemoText_shortens_long_neighbours()
    {
        string longText = new('x', 50);
        var memo = VoReference.MemoText(longText + "|now|" + longText);
        Assert.Equal("[...]" + new string('x', 29) + " [<<<]\r\nnow\r\n[>>>] " + new string('x', 29) + "[...]", memo.Text);
    }

    [Fact]
    public void ShrinkSpace_collapses_separators()
    {
        Assert.Equal("a b c", VoReference.ShrinkSpace("  a |\r\n b   c | "));
    }

    [Fact]
    public void Load_reads_srt_and_returns_empty_for_missing_file()
    {
        string path = Path.Combine(Path.GetTempPath(), $"vss-vo-{Guid.NewGuid():N}.srt");
        File.WriteAllText(path, "1\r\n00:00:01,000 --> 00:00:02,000\r\nHello\r\n\r\n");
        try
        {
            var vo = VoReference.Load(path);
            Assert.Single(vo);
            Assert.Equal((1000, 2000, "Hello"), (vo[0].StartMs, vo[0].StopMs, vo[0].Text));
            Assert.Empty(VoReference.Load(path + ".none"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Translators_follow_the_original()
    {
        Assert.Equal("{\\i1}{\\i0}", Translators.StripText("{\\i1}Hello{\\i0} you"));
        Assert.Equal(string.Empty, Translators.Create(TranslateTextType.Empty, "x")("abc"));
        Assert.Equal("abc", Translators.Create(TranslateTextType.CopyOriginal, "x")("abc"));
        Assert.Equal("<i></i>", Translators.Create(TranslateTextType.CopyTagsOnly, "x")("<i>abc</i>"));
        Assert.Equal("x", Translators.Create(TranslateTextType.CustomText, "x")("abc"));
        Assert.Equal(@"C:\a\movie.translated.srt", Translators.TranslatedFileName(@"C:\a\movie.srt"));

        var vo = new List<Subtitle> { Sub(0, 1000, "a"), Sub(2000, 3000, "b") };
        var missing = Translators.MissingFromVo(vo, t => t == 500, s => s.ToUpperInvariant());
        Assert.Single(missing);
        Assert.Equal((2000, 3000, "B"), (missing[0].StartMs, missing[0].StopMs, missing[0].Text));
    }

    [Fact]
    public void Vo_band_on_top_is_not_editable()
    {
        var m = new WaveformMouse { PositionMs = 0, PageSizeMs = 10_000, LengthMs = 60_000, WidthPx = 1000, WavHeightPx = 200 };
        m.SetRanges([new TimeRange(1000, 2000), new TimeRange(3000, 4000)]);
        m.SetVoRanges([new TimeRange(3000, 4000)]);
        m.SnappingEnabled = false;
        m.ShowVO = true;

        m.MouseMove(WaveformInput.None, 301, 50); // faixa de cima (VO)
        Assert.Equal(DynamicEditMode.None, m.DynamicEditMode);
        Assert.False(m.MinBlank1.Exists);

        m.MouseMove(WaveformInput.None, 301, 150); // faixa de baixo (legendas)
        Assert.Equal(DynamicEditMode.Start, m.DynamicEditMode);

        m.MouseMove(WaveformInput.None, 350, 150);
        m.MouseDown(WaveformInput.Left, 350, 150);
        m.MouseUp();
        m.DoubleClick(50);
        Assert.Equal(WaveformMouse.NoRange, m.SelectedRangeIndex);
        m.DoubleClick(150);
        Assert.Equal(1, m.SelectedRangeIndex);
    }
}
