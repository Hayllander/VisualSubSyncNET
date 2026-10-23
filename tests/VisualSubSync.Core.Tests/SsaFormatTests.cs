using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Subtitles.Formats;

namespace VisualSubSync.Core.Tests;

public class SsaFormatTests
{
    private const string AssFormatLine =
        "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic,  Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding";

    private const string SsaFormatLine =
        "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, TertiaryColour, BackColour, Bold, Italic, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, AlphaLevel, Encoding";

    private static readonly SubtitleWriteOptions Legacy = new() { ApplicationVersion = "1.0.1.0" };

    private static string Lines(params string[] lines) => string.Concat(lines.Select(l => l + "\r\n"));

    private static readonly string AssSample = Lines(
        "[Script Info]",
        "; comentário",
        "Title: Teste",
        "ScriptType: v4.00+",
        "PlayResX: 640",
        "",
        "[V4+ Styles]",
        "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding",
        "Style: Default,Arial,28,&H00FFFFFF,&H000000FF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,2.5,0,2,10,10,10,0",
        "",
        "[Events]",
        "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
        "Comment: 0,0:00:00.00,0:00:01.00,Default,,0,0,0,,ignorado",
        "Dialogue: 1,0:00:03.10,0:00:04.00,Default,,0010,0020,0030,Karaoke,{\\i1}x{\\i0}",
        "Dialogue: 0,0:00:01.00,0:00:02.50,Default,Ana,0000,0000,0000,,Olá, mundo\\Nsegunda",
        "",
        "[Fonts]",
        "fontname: a.ttf");

    private static readonly string SsaSample = Lines(
        "[Script Info]",
        "ScriptType: v4.00",
        "",
        "[V4 Styles]",
        SsaFormatLine,
        "Style: Default,Arial,28,65535,255,16744448,-2147483640,-1,0,1,3,0,9,30,30,30,0,128",
        "",
        "[Events]",
        "Format: Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
        "Dialogue: Marked=0,0:00:05.00,0:00:07.00,Default,,0000,0000,0000,,toto tata");

    [Fact]
    public void Parses_ass_events_styles_header_and_footer()
    {
        var (doc, isAss) = SsaFormat.Parse(AssSample);

        Assert.True(isAss);
        Assert.Equal("Title: Teste\r\nScriptType: v4.00+\r\nPlayResX: 640\r\n", doc.Header);
        Assert.Equal("[Fonts]\r\nfontname: a.ttf", doc.Footer);
        Assert.Equal(2, doc.Items.Count);

        var first = doc.Items[0];
        Assert.Equal((1000, 2500), (first.StartMs, first.StopMs));
        Assert.Equal("Olá, mundo\r\nsegunda", first.Text);
        Assert.Equal(("0", "Ana", "Default"), (first.Layer, first.Actor, first.Style));

        var second = doc.Items[1];
        Assert.Equal(("1", "0010", "0020", "0030", "Karaoke"), (second.Layer, second.MarginL, second.MarginR, second.MarginV, second.Effect));

        var style = Assert.Single(doc.Styles);
        Assert.Equal((28.0, -1, 2.5, 2), (style.FontSize, style.Bold, style.Outline, style.Alignment));
        Assert.Equal((0x00FFFFFFu, 0x000000FFu, 0x80000000u), (style.PrimaryColor, style.SecondaryColor, style.BackColor));
    }

    [Fact]
    public void Ass_roundtrip_matches_legacy_output()
    {
        var doc = SsaFormat.Parse(AssSample).Document;

        string expected = Lines(
            "[Script Info]",
            "; Written by VisualSubSync 1.0.1.0",
            "Title: Teste",
            "ScriptType: v4.00+",
            "PlayResX: 640",
            "",
            "[v4+ Styles]",
            AssFormatLine,
            "Style: Default,Arial,28,&H00FFFFFF,&H000000FF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,2.5,0,2,10,10,10,0",
            "",
            "[Events]",
            "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
            "Dialogue: 0,0:00:01.00,0:00:02.50,Default,Ana,0000,0000,0000,,Olá, mundo\\Nsegunda",
            "Dialogue: 1,0:00:03.10,0:00:04.00,Default,,0010,0020,0030,Karaoke,{\\i1}x{\\i0}",
            "",
            "[Fonts]",
            "fontname: a.ttf");
        Assert.Equal(expected, SsaFormat.Format(doc, ass: true, Legacy));
    }

    [Fact]
    public void Ssa_roundtrip_keeps_decimal_colors_and_ssa_alignment()
    {
        var (doc, isAss) = SsaFormat.Parse(SsaSample);

        Assert.False(isAss);
        var style = Assert.Single(doc.Styles);
        Assert.Equal(4, style.Alignment); // SSA 9 = meio à esquerda = ASS 4
        Assert.Equal(16744448u, style.OutlineColor); // lido de TertiaryColour

        string expected = Lines(
            "[Script Info]",
            "; Written by VisualSubSync 1.0.1.0",
            "ScriptType: v4.00",
            "",
            "[v4 Styles]",
            SsaFormatLine,
            "Style: Default,Arial,28,65535,255,16744448,-2147483640,-1,0,1,3,0,9,30,30,30,0,128",
            "",
            "[Events]",
            "Format: Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
            "Dialogue: Marked=0,0:00:05.00,0:00:07.00,Default,,0000,0000,0000,,toto tata");
        Assert.Equal(expected, SsaFormat.Format(doc, ass: false, Legacy));
    }

    [Fact]
    public void Ssa_saved_as_ass_converts_header_colors_and_alignment()
    {
        var doc = SsaFormat.Parse(SsaSample).Document;

        string ass = SsaFormat.Format(doc, ass: true, Legacy);

        Assert.Contains("\r\nScriptType: v4.00+\r\n", ass, StringComparison.Ordinal);
        Assert.Contains("Style: Default,Arial,28,&H0000FFFF,&H000000FF,&H00FF8000,&H80000008,-1,0,0,0,100,100,0,0,1,3,0,4,30,30,30,128\r\n",
            ass, StringComparison.Ordinal);
        Assert.Contains("Dialogue: 0,0:00:05.00,", ass, StringComparison.Ordinal);
    }

    [Fact]
    public void Srt_saved_as_ass_uses_default_header_style_and_tags()
    {
        var doc = SrtFormat.Parse("1\r\n00:00:01,000 --> 00:00:02,345\r\n<i>a</i>\r\nb\r\n");

        string ass = SubtitleFile.Format(doc, ".ass", Legacy with { PreviousExtension = ".srt", VideoWidth = 640, VideoHeight = 480 });

        Assert.StartsWith(Lines(
            "[Script Info]",
            "; Written by VisualSubSync 1.0.1.0",
            "Title: <untitled>",
            "Original Script: <unknown>",
            "ScriptType: v4.00+",
            "PlayResX: 640",
            "PlayResY: 480",
            "PlayDepth: 0",
            "Timer: 100.0",
            "WrapStyle: 0",
            "",
            "[v4+ Styles]",
            AssFormatLine,
            "Style: Default,Arial,24,&H0000FFFF,&H00000000,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,15,15,15,0"), ass);
        Assert.EndsWith("Dialogue: 0,0:00:01.00,0:00:02.34,Default,,0000,0000,0000,,{\\i1}a{\\i0}\\Nb\r\n", ass);

        string ssa = SubtitleFile.Format(doc, ".ssa", Legacy);
        Assert.Contains("ScriptType: v4.00\r\nPlayDepth: 0\r\nTimer: 100.0\r\n\r\n[v4 Styles]", ssa, StringComparison.Ordinal);
        Assert.EndsWith("Dialogue: Marked=0,0:00:01.00,0:00:02.34,Default,,0000,0000,0000,,<i>a</i>\\Nb\r\n", ssa);
    }

    [Fact]
    public void Lines_before_first_section_go_to_footer_and_short_hours_parse()
    {
        var doc = SsaFormat.Parse(Lines(
            "lixo inicial",
            "[Script Info]",
            "[Events]",
            "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
            "Dialogue: 0,10:00:00.00,10:00:01.5,Default,,0,0,0,,x",
            "Dialogue: 0,ruim,0:00:01.00,Default,,0,0,0,,descartada")).Document;

        Assert.Equal("lixo inicial", doc.Footer);
        var sub = Assert.Single(doc.Items);
        Assert.Equal((36_000_000, 36_001_050), (sub.StartMs, sub.StopMs));
    }

    [Theory]
    [InlineData("&H00FFFFFF", 0x00FFFFFFu)]
    [InlineData("&H80112233&", 0x80112233u)]
    [InlineData("&HFF8000", 0x00FF8000u)]
    [InlineData("&HFF", 0x000000FFu)]
    [InlineData("65535", 0x0000FFFFu)]
    [InlineData("-2147483640", 0x80000008u)]
    [InlineData("lixo", 0u)]
    [InlineData("", 0u)]
    public void Color_parsing_matches_AssColorString2ABGR(string text, uint expected) =>
        Assert.Equal(expected, SsaStyle.ParseColor(text));

    [Fact]
    public void Alignment_maps_between_ssa_and_ass()
    {
        int[] ssa = [1, 2, 3, 9, 10, 11, 5, 6, 7];
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9], ssa.Select(SsaStyle.SsaToAssAlignment));
        Assert.Equal(ssa, Enumerable.Range(1, 9).Select(SsaStyle.AssToSsaAlignment));
    }
}
