using VisualSubSync.Core.Subtitles.Formats;

namespace VisualSubSync.Core.Tests;

/// <summary>Arquivos reais de samples\ do VisualSubSync original.</summary>
public sealed class SampleFileTests : IDisposable
{
    private static readonly string Sample = Path.Combine(AppContext.BaseDirectory, "Samples", "UnicodeSample.srt");

    private readonly string _dir = Directory.CreateTempSubdirectory("vss-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>Igual ao original, exceto o espaço no fim de uma linha, que o legado também remove (Trim).</summary>
    [Fact]
    public void Unicode_sample_resaves_like_legacy()
    {
        var doc = SubtitleFile.Load(Sample);
        Assert.True(doc.IsUtf8);
        Assert.NotEmpty(doc.Items);

        string copy = Path.Combine(_dir, "copy.srt");
        SubtitleFile.Save(doc, copy);

        string expected = string.Join("\r\n", File.ReadAllText(Sample).Split("\r\n").Select(l => l.TrimEnd(' ')));
        Assert.Equal([0xEF, 0xBB, 0xBF], File.ReadAllBytes(copy)[..3]);
        Assert.Equal(expected, File.ReadAllText(copy));
    }

    /// <summary>Texto e ordem sobrevivem; os tempos perdem os milissegundos (SSA/ASS guarda centésimos).</summary>
    [Fact]
    public void Unicode_sample_survives_ass_and_ssa_conversion()
    {
        var original = SubtitleFile.Load(Sample);
        foreach (string ext in new[] { ".ass", ".ssa" })
        {
            string path = Path.Combine(_dir, "conv" + ext);
            SubtitleFile.Save(original, path, new SubtitleWriteOptions { PreviousExtension = ".srt" });
            var back = SubtitleFile.Load(path);

            Assert.Equal(original.Items.Select(s => s.Text), back.Items.Select(s => s.Text));
            Assert.Equal(original.Items.Select(s => s.StartMs / 10 * 10), back.Items.Select(s => s.StartMs));
            Assert.Equal(original.Items.Select(s => s.StopMs / 10 * 10), back.Items.Select(s => s.StopMs));
        }
    }
}
