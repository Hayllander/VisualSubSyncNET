using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Tests;

public class TextSearchTests
{
    [Theory]
    [InlineData(1, "Hello world", "world", false, false, 7)]
    [InlineData(1, "Hello World", "world", false, false, 0)]
    [InlineData(1, "Hello World", "world", true, false, 7)]
    [InlineData(8, "Hello world", "world", false, false, 0)]
    [InlineData(1, "cat concat cat.", "cat", false, true, 1)]
    [InlineData(2, "cat concat cat.", "cat", false, true, 12)]
    [InlineData(1, "", "a", false, false, 0)]
    [InlineData(1, "abc", "", false, false, 1)]
    public void Find_follows_WideStringFind(int offset, string text, string pattern, bool ignoreCase, bool wholeWord, int expected) =>
        Assert.Equal(expected, TextSearch.Find(offset, text, pattern, ignoreCase, wholeWord));

    [Fact]
    public void Count_overlaps_like_WideStringCount_but_replace_does_not()
    {
        Assert.Equal(2, TextSearch.Count("aaa", "aa", false, false));
        Assert.Equal("ba", TextSearch.ReplaceAll("aaa", "aa", "b", false, false));
        Assert.Equal("dog concat dog.", TextSearch.ReplaceAll("cat concat Cat.", "CAT", "dog", true, true));
        Assert.Equal(2, TextSearch.Count("cat concat Cat.", "CAT", true, true));
    }

    [Fact]
    public void Regex_uses_TRegExpr_defaults()
    {
        var regex = TextSearch.CreateRegex("a.b", ignoreCase: true);
        Assert.Equal((3, 3), TextSearch.RegexFind(regex, "xxA\nB", 1));
        Assert.Equal((0, 0), TextSearch.RegexFind(regex, "xxA\nB", 4));
        Assert.Equal("[b-a] [d-c]", TextSearch.RegexReplace(TextSearch.CreateRegex(@"(\w)(\w)", false), "ab cd", "[$2-$1]", out int count));
        Assert.Equal(2, count);
    }
}
