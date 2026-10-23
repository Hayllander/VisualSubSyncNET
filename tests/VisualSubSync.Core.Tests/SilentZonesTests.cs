using System.Globalization;
using VisualSubSync.Core.Audio;

namespace VisualSubSync.Core.Tests;

public class SilentZonesTests
{
    private static PeakData Peaks(params short[] max) => new()
    {
        LengthMs = max.Length * 10,
        SamplesPerSecond = 1000,
        Channels = 1,
        BitsPerSample = 16,
        SamplesPerPeak = 10,
        Peaks = [.. max.Select(m => new Peak(m, (short)-m))],
    };

    [Fact]
    public void Consecutive_quiet_windows_form_one_zone()
    {
        // Picos de 10 ms e janelas de 100 ms: as janelas fecham nos picos 9, 18, 27, 36 e 45.
        short[] quiet = [.. Enumerable.Repeat((short)30, 20)];
        short[] loud = [.. Enumerable.Repeat((short)1000, 9)];
        short[] quietEnd = [.. Enumerable.Repeat((short)30, 18)];
        var zones = SilentZones.Detect(Peaks([.. quiet, .. loud, .. quietEnd]), 100);
        Assert.Equal(2, zones.Count);
        Assert.Equal((0, 180, 2), (zones[0].StartMs, zones[0].StopMs, zones[0].RmsCount));
        Assert.Equal((360, 450, 1), (zones[1].StartMs, zones[1].StopMs, zones[1].RmsCount));
    }

    [Fact]
    public void Rms_divides_by_one_less_than_the_window_like_the_original()
    {
        // A primeira janela soma 10 picos e as seguintes 9; todas dividem por 9.
        var zones = SilentZones.Detect(Peaks([.. Enumerable.Repeat((short)30, 19)]), 100);
        Assert.Equal(30.0 + (30.0 * Math.Sqrt(10.0 / 9.0)), zones[0].RmsSum, 6);
    }

    [Fact]
    public void Line_shows_times_and_average_rms()
    {
        var zone = new SilentZone(1000, 2500, 50.25, 2);
        Assert.Equal("00:00:01.000 -> 00:00:02.500 (25,1)", zone.Format(CultureInfo.GetCultureInfo("pt-BR")));
    }
}
