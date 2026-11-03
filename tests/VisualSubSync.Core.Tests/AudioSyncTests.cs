using VisualSubSync.Core.Audio;
using VisualSubSync.Core.Plugins;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Tests;

/// <summary>Funcionalidade nova: os níveis do áudio (AudioLevels) e o plugin Audio sync.</summary>
public sealed class AudioSyncTests
{
    private static readonly string PluginPath = Path.Combine(AppContext.BaseDirectory, "jsplugin-enh");

    /// <summary>Um pico a cada 10 ms; voz (pico 10000) nos trechos pedidos, silêncio (pico 100) no resto.</summary>
    private static PeakData Peaks(int lengthMs, params (int Start, int Stop)[] voice)
    {
        var peaks = new Peak[lengthMs / 10];
        for (int i = 0; i < peaks.Length; i++)
        {
            int t = i * 10;
            short v = voice.Any(r => t >= r.Start && t < r.Stop) ? (short)10000 : (short)100;
            peaks[i] = new Peak(v, (short)-v);
        }

        return new PeakData { LengthMs = lengthMs, SamplesPerSecond = 44100, Channels = 1, BitsPerSample = 16, SamplesPerPeak = 441, Peaks = peaks };
    }

    [Fact]
    public void Levels_are_relative_to_the_loudest_peak()
    {
        var audio = new AudioLevels(Peaks(5000, (1000, 2000)));
        Assert.Equal(100, audio.Level(1000, 2000), 3);
        Assert.Equal(1, audio.Level(0, 1000), 3);
        Assert.Equal(1000, audio.Find(0, 5000, 10, above: true));
        Assert.Equal(2000, audio.Find(1000, 5000, 10, above: false));
        // Para trás devolve onde o trecho achado acaba.
        Assert.Equal(2000, audio.Find(4000, 0, 10, above: true));
        Assert.Equal(1000, audio.Find(1500, 0, 10, above: false));
        Assert.Equal(-1, audio.Find(3000, 4000, 10, above: true));
    }

    [Fact]
    public void Audio_sync_reports_early_late_and_missing_voice()
    {
        var subs = new List<Subtitle>
        {
            new() { StartMs = 1000, StopMs = 3000, Text = "Early" },   // voz em 1500
            new() { StartMs = 4500, StopMs = 6000, Text = "Late" },    // voz em 4000
            new() { StartMs = 7000, StopMs = 8000, Text = "Silent" },  // sem voz
            new() { StartMs = 9000, StopMs = 10000, Text = "Ok" },     // voz em 9100
        };
        var context = new PluginContext { Audio = new AudioLevels(Peaks(12000, (1500, 2800), (4000, 5800), (9100, 9900))) };
        var log = new List<string>();
        var settings = PluginSettings.Build(PluginPath, context, log.Add);
        var errors = ErrorChecker.Check(subs, PluginPath, context, settings, log.Add).Where(e => e.PluginName == "Audio sync").ToList();
        Assert.Empty(log);
        Assert.Equal(
            ["Early: starts 500 ms before the voice", "Late: starts 500 ms after the voice", "Silent: no voice found"],
            errors.Select(e => e.Subtitle.Text + ": " + e.ErrorMessage));
        Assert.False(ErrorChecker.CanFixError(errors[0].PluginFileName, context, settings, null));
    }

    [Fact]
    public void Audio_sync_is_quiet_without_a_waveform()
    {
        var subs = new List<Subtitle> { new() { StartMs = 1000, StopMs = 3000, Text = "A" } };
        var context = new PluginContext();
        var settings = PluginSettings.Build(PluginPath, context, null);
        Assert.DoesNotContain(ErrorChecker.Check(subs, PluginPath, context, settings, null), e => e.PluginName == "Audio sync");
    }
}
