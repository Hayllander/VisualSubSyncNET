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
}
