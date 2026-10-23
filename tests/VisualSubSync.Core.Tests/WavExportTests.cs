using System.Buffers.Binary;
using System.Text;
using VisualSubSync.Core.Audio;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Web;

namespace VisualSubSync.Core.Tests;

public sealed class WavExportTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("vss-wav-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>WAV PCM mono de 16 bits a 1000 Hz (2000 bytes por segundo), com fmt de 16 bytes.</summary>
    private string WriteWav(int dataBytes, int riffSizeDelta = 0)
    {
        var data = new byte[dataBytes];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)i;
        }

        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
        {
            w.Write("RIFF"u8);
            w.Write(36 + dataBytes + riffSizeDelta);
            w.Write("WAVEfmt "u8);
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(1000);
            w.Write(2000);
            w.Write((short)2);
            w.Write((short)16);
            w.Write("data"u8);
            w.Write(dataBytes);
            w.Write(data);
        }

        string path = Path.Combine(_folder, "audio.wav");
        File.WriteAllBytes(path, ms.ToArray());
        return path;
    }

    [Fact]
    public void Extract_writes_an_aligned_wav_with_an_18_byte_format()
    {
        using var wav = WavFile.Open(WriteWav(2000))!;
        Assert.False(wav.IsDamaged);
        Assert.Equal((1, 1000, 2000, 2, 16), (wav.Channels, wav.SamplesPerSecond, wav.AverageBytesPerSecond, wav.BlockAlign, wav.BitsPerSample));

        using var output = new MemoryStream();
        // 100 ms = byte 200 e 250 ms = byte 500 do áudio; o trecho tem 300 bytes.
        long total = wav.ExtractToStream(100, 250, output);
        byte[] bytes = output.ToArray();
        Assert.Equal(346, total);
        Assert.Equal(346, bytes.Length);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(338u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal("WAVEfmt ", Encoding.ASCII.GetString(bytes, 8, 8));
        Assert.Equal(18u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(36)));
        Assert.Equal("data", Encoding.ASCII.GetString(bytes, 38, 4));
        Assert.Equal(300u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(42)));
        Assert.Equal((byte)200, bytes[46]);
        Assert.Equal((byte)(499 % 256), bytes[^1]);
    }

    [Fact]
    public void Open_flags_a_wrong_riff_size_and_rejects_other_files()
    {
        using (var damaged = WavFile.Open(WriteWav(100, riffSizeDelta: 4)))
        {
            Assert.True(damaged!.IsDamaged);
        }

        string text = Path.Combine(_folder, "x.wav");
        File.WriteAllText(text, "not a wav file");
        Assert.Null(WavFile.Open(text));
        Assert.Null(WavFile.Open(Path.Combine(_folder, "missing.wav")));
    }

    [Fact]
    public void Extract_fails_when_the_audio_ends_before_the_subtitle()
    {
        using var wav = WavFile.Open(WriteWav(200))!;
        Assert.Throws<EndOfStreamException>(() => wav.ExtractToStream(0, 1000, Stream.Null));
    }

    [Fact]
    public void Page_replaces_fields_and_repeats_the_subtitle_block()
    {
        var subs = new List<Subtitle>
        {
            new() { StartMs = 1000, StopMs = 2500, Text = "Ola\r\nmundo" },
            new() { StartMs = 3000, StopMs = 4000, Text = "<i>Dois</i>" },
        };
        var context = new PageContext("1.2.3", Path.Combine(_folder, "proj.vssprj"), subs, 2000);
        string template = "$|project-filename|$ $|version|$ $|unknown|$ $|count|$ $|open\n" +
            "<!--$TMPL_SUBTITLES_LIST$-->[$|index|$ $|index-padded|$ $|time-index|$ $|start|$-$|stop|$ $|html-text8|$ $|stripped-text|$ $|wav-size|$]<!--$TMPL_SUBTITLES_LIST_END$-->\n" +
            "<!--$TMPL_SELECTED_SUB$-->{$|index|$}<!--$TMPL_SELECTED_SUB_END$-->";
        var env = new Dictionary<string, string> { ["count"] = "7", ["idx"] = "2" };

        string page = Encoding.UTF8.GetString(PageProcessor.ProcessPage(Encoding.UTF8.GetBytes(template), context, env));

        Assert.Equal(
            "proj.vssprj 1.2.3 $|unknown|$ 7 $|open\n" +
            "[1 000001 3E8-9C4 00:00:01.000-00:00:02.500 Ola<br>mundo Ola\r\nmundo 2 KB]" +
            "[2 000002 BB8-FA0 00:00:03.000-00:00:04.000 <i>Dois</i> Dois 1 KB]\n" +
            "{2}",
            page);
    }

    [Fact]
    public void Fields_ending_in_8_are_utf8_and_the_others_ansi()
    {
        var context = new PageContext("1", "p.vssprj", [new Subtitle { Text = "é" }]);
        byte[] page = PageProcessor.ProcessPage("<!--$TMPL_SUBTITLES_LIST$-->$|raw-text8|$|$|raw-text|$<!--$TMPL_SUBTITLES_LIST_END$-->"u8.ToArray(), context);
        Assert.Equal([.. Encoding.UTF8.GetBytes("é"), (byte)'|', .. Text.TextFileReader.Ansi.GetBytes("é")], page);
    }

    [Fact]
    public void Page_reports_an_unknown_block()
    {
        var context = new PageContext("1", "p.vssprj", []);
        byte[] page = PageProcessor.ProcessPage("a<!--$TMPL_FOO$-->b<!--$TMPL_FOO_END$-->c"u8.ToArray(), context);
        Assert.Equal("aERROR : Unkown block FOO", Encoding.ASCII.GetString(page));
    }

    [Fact]
    public void Export_writes_one_wav_per_subtitle_and_the_index_page()
    {
        using var wav = WavFile.Open(WriteWav(2000))!;
        string output = Directory.CreateDirectory(Path.Combine(_folder, "out")).FullName;
        var subs = new List<Subtitle> { new() { StartMs = 0, StopMs = 100, Text = "A" }, new() { StartMs = 200, StopMs = 300, Text = "B" } };
        string template = Path.Combine(AppContext.BaseDirectory, "web", WavExport.TemplateName);

        WavExport.Export(wav, output, new PageContext("2.0", "proj.vssprj", subs, 2000), template);

        Assert.Equal(246, new FileInfo(Path.Combine(output, "000001.wav")).Length);
        Assert.True(File.Exists(Path.Combine(output, "000002.wav")));
        byte[] page = File.ReadAllBytes(Path.Combine(output, WavExport.PageName));
        Assert.Equal([0xEF, 0xBB, 0xBF], page[..3]);
        string html = Encoding.UTF8.GetString(page);
        Assert.Contains("<title>VisualSubSync 2.0</title>", html, StringComparison.Ordinal);
        Assert.Contains("Project : proj.vssprj", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"000002.wav\">[play]</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("$|", html, StringComparison.Ordinal);
    }
}
