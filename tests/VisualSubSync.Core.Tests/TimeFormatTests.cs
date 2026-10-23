using VisualSubSync.Core.Timing;

namespace VisualSubSync.Core.Tests;

public class TimeFormatTests
{
    [Theory]
    [InlineData(0, "00:00:00.000")]
    [InlineData(3_006_969, "00:50:06.969")]
    [InlineData(36_000_001, "10:00:00.001")]
    public void ToVss_matches_legacy_format(int ms, string expected) => Assert.Equal(expected, TimeFormat.ToVss(ms));

    [Theory]
    [InlineData("00:50:06,969", 3_006_969)]
    [InlineData("00:50:06.969", 3_006_969)]
    [InlineData("0:50:06.969", -1)]
    [InlineData("abc", -1)]
    public void ParseVss_matches_TimeStringToMs(string text, int expected) => Assert.Equal(expected, TimeFormat.ParseVss(text));

    [Fact]
    public void ToSsa_uses_centiseconds() => Assert.Equal("1:02:03.45", TimeFormat.ToSsa(3_723_456));
}
