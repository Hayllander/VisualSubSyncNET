using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Tests;

public class TextStyleTagsTests
{
    [Fact]
    public void Tags_depend_on_the_subtitle_extension()
    {
        Assert.Equal(("<i>", "</i>"), TextStyleTags.For(TextTagType.Italic, @"C:\a\film.SRT"));
        Assert.Equal(("<font color=\"#FFFFFF\">", "</font>"), TextStyleTags.For(TextTagType.Color, "film.srt"));
        Assert.Equal(("{\\b1}", "{\\b0}"), TextStyleTags.For(TextTagType.Bold, "film.ass"));
        Assert.Equal(("{\\fs16}", "{\\fs}"), TextStyleTags.For(TextTagType.Size, "film.ssa"));
        Assert.Null(TextStyleTags.For(TextTagType.Italic, "film.txt"));
    }

    [Fact]
    public void Selection_is_wrapped_and_reselected_with_the_tags()
    {
        var result = TextStyleTags.TagMemo("one two", 4, 3, "<i>", "</i>");
        Assert.Equal(new TaggedMemoText("one <i>two</i>", 4, 10), result);
    }

    [Fact]
    public void Caret_at_start_wraps_the_whole_text()
    {
        Assert.Equal(new TaggedMemoText("<b>abc</b>", 0, 0), TextStyleTags.TagMemo("abc", 0, 0, "<b>", "</b>"));
    }

    [Fact]
    public void Caret_elsewhere_inserts_both_tags_and_stays_inside()
    {
        Assert.Equal(new TaggedMemoText("ab{\\i1}{\\i0}c", 7, 0), TextStyleTags.TagMemo("abc", 2, 0, "{\\i1}", "{\\i0}"));
    }

    [Fact]
    public void Positions_use_an_tags_only_in_srt()
    {
        Assert.Equal(("{\\an1}", ""), TextStyleTags.For(TextTagType.PosBottomLeft, "film.srt"));
        Assert.Equal(("{\\an2}", ""), TextStyleTags.For(TextTagType.PosBottomMiddle, "film.srt"));
        Assert.Equal(("{\\an5}", ""), TextStyleTags.For(TextTagType.PosMiddleMiddle, "film.srt"));
        Assert.Equal(("{\\an7}", ""), TextStyleTags.For(TextTagType.PosTopLeft, "film.srt"));
        Assert.Equal(("{\\an9}", ""), TextStyleTags.For(TextTagType.PosTopRight, "film.srt"));
        Assert.Null(TextStyleTags.For(TextTagType.PosTopLeft, "film.ass"));
    }

    [Fact]
    public void Case_changes_keep_the_tags()
    {
        const string text = "<i>Ele saiu</i> pela PORTA\r\nda frente {\\an8}AGORA";
        Assert.Equal("<i>ele saiu</i> pela porta\r\nda frente {\\an8}agora", TextStyleTags.ChangeCase(text, TextCaseType.Lower));
        Assert.Equal("<i>ELE SAIU</i> PELA PORTA\r\nDA FRENTE {\\an8}AGORA", TextStyleTags.ChangeCase(text, TextCaseType.Upper));
        Assert.Equal("<i>Ele Saiu</i> Pela Porta\r\nDa Frente {\\an8}Agora", TextStyleTags.ChangeCase(text, TextCaseType.CapitalizeWords));
    }
}
