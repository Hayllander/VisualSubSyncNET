using VisualSubSync.Core.Audio;
using VisualSubSync.Core.Settings;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Tests;

public class MainWindowSupportTests
{
    [Fact]
    public void FontSpec_parses_String2Font_format_and_keeps_fallback_otherwise()
    {
        var font = FontSpec.Parse("Tahoma,12,3,0,clRed", VssSettings.DefaultSubTextFont);
        Assert.Equal("Tahoma", font.Name);
        Assert.Equal(12, font.SizePt);
        Assert.True(font.Bold);
        Assert.True(font.Italic);
        Assert.Equal(0xFF0000, font.ToRgb());
        Assert.Null(VssSettings.DefaultSubListFont.ToRgb());
        Assert.Same(VssSettings.DefaultSubListFont, FontSpec.Parse("Arial,8", VssSettings.DefaultSubListFont));
        Assert.Equal("Arial,10,1,0,clWindowText", VssSettings.DefaultSubTextFont.ToString());
    }

    [Fact]
    public void Settings_roundtrip_keeps_unknown_lines()
    {
        string path = Path.Combine(Path.GetTempPath(), $"vss-{Guid.NewGuid():N}.ini");
        try
        {
            File.WriteAllText(path, "[Misc]\r\nSwapSubtitlesList=1\r\nOther=abc\r\n[Fonts]\r\nSubList=Verdana,9,0,0,clWindowText\r\n");
            var settings = VssSettings.Load(path);
            Assert.True(settings.SwapSubtitlesList);
            Assert.Equal("Verdana", settings.SubListFont.Name);
            Assert.Equal(225, settings.PanelTopHeight);
            Assert.Null(settings.MainForm);

            settings.PanelTopHeight = 300;
            settings.MainForm = new WindowPlacement(10, 20, 800, 600, 2);
            settings.Save();

            string text = File.ReadAllText(path);
            Assert.Contains("Other=abc", text, StringComparison.Ordinal);
            var again = VssSettings.Load(path);
            Assert.Equal(300, again.PanelTopHeight);
            Assert.Equal(6000, again.MaximumSubtitleDuration);
            Assert.Equal(new WindowPlacement(10, 20, 800, 600, 2), again.MainForm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Normalize_applies_factor_only_above_1_1()
    {
        var quiet = new PeakData
        {
            LengthMs = 1, SamplesPerSecond = 44100, Channels = 1, BitsPerSample = 16, SamplesPerPeak = 441,
            Peaks = [new Peak(8192, -4096), new Peak(100, -100)],
        };
        Assert.True(PeakFile.Normalize(quiet));
        Assert.Equal(new Peak(-32768, -16384), quiet.Peaks[0]); // 32768 não cabe em SmallInt, como no Delphi
        Assert.Equal(new Peak(400, -400), quiet.Peaks[1]);

        var loud = new PeakData
        {
            LengthMs = 1, SamplesPerSecond = 44100, Channels = 1, BitsPerSample = 16, SamplesPerPeak = 441,
            Peaks = [new Peak(31000, -100)],
        };
        Assert.False(PeakFile.Normalize(loud));
        Assert.Equal(new Peak(31000, -100), loud.Peaks[0]);
    }

    [Fact]
    public void Tags_are_stripped_keeping_new_lines()
    {
        Assert.Equal("Hello\r\nworld", SubtitleTextStats.StripTagsKeepNewLines("<i>Hello</i>\r\n{\\b1}world"));
        Assert.Equal("a\r\nb", SubtitleTextStats.StripTagsKeepNewLines("a<x\r\n>b"));
        Assert.Equal("a<b", SubtitleTextStats.StripTagsKeepNewLines("a<b"));
    }

    [Fact]
    public void Lines_counter_and_status_follow_UpdateLinesCounter()
    {
        string text = "<i>Hello</i>\r\nworld!";
        Assert.Equal("5\r\n6", SubtitleTextStats.LinesCounter(text, 0));
        Assert.Equal("6", SubtitleTextStats.LinesCounter(text, 1));
        Assert.Equal("0", SubtitleTextStats.LinesCounter(string.Empty, 0));
        Assert.Equal("3\r\n0", SubtitleTextStats.LinesCounter("abc\r\n", 0));

        // 11 caracteres + 2 do CRLF em 2 s = 6,5 → 6 (arredonda para par).
        Assert.Equal("Line: 2, Column: 4 | Total: 11, Char/s: 6", SubtitleTextStats.StatusText(text, 1, 3, 0, 2000));
        Assert.Equal("Line: 1, Column: 1 | Total: 0, Char/s: -1", SubtitleTextStats.StatusText(string.Empty, 0, 0, 0, 0));
        Assert.Equal("Line: 1, Column: 1 | Total: 0", SubtitleTextStats.StatusText(string.Empty, 0, 0, 0, null));
    }
}
