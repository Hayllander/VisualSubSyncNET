using System.Text;
using VisualSubSync.Core.Audio;

namespace VisualSubSync.Core.Tests;

public sealed class WavPeakCompatibilityTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("vss-wav-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void Generates_legacy_metadata_and_normalized_silent_peaks(int bits)
    {
        string path = Path.Combine(_folder, "silent.wav");
        using (var writer = new BinaryWriter(File.Create(path), Encoding.ASCII))
        {
            int length = 20 * (bits / 8); // 20 ms, two complete 10 ms blocks
            writer.Write("RIFF"u8); writer.Write(36 + length); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)(bits == 32 ? 3 : 1)); writer.Write((short)1);
            writer.Write(1000); writer.Write(1000 * (bits / 8)); writer.Write((short)(bits / 8)); writer.Write((short)bits);
            writer.Write("data"u8); writer.Write(length);
            var bytes = new byte[length];
            if (bits == 8) Array.Fill(bytes, (byte)128);
            writer.Write(bytes);
        }

        using var wav = WavFile.Open(path);
        Assert.NotNull(wav);
        var peaks = wav.CreatePeaks();
        Assert.Equal(20, peaks.LengthMs);
        Assert.Equal((uint)10, peaks.SamplesPerPeak);
        Assert.Equal((ushort)bits, peaks.BitsPerSample);
        Assert.Equal([new Peak(0, 0), new Peak(0, 0)], peaks.Peaks);
        PeakFile.Save(peaks, Path.ChangeExtension(path, ".peak"));
        Assert.Equal(peaks.Peaks, PeakFile.Load(Path.ChangeExtension(path, ".peak")).Peaks);
    }

    [Fact]
    public void Interleaved_channels_are_combined_and_partial_block_is_zero_padded()
    {
        string path = Path.Combine(_folder, "stereo.wav");
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write("RIFF"u8); writer.Write(36 + 44); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)2); writer.Write(1000);
            writer.Write(4000); writer.Write((short)4); writer.Write((short)16); writer.Write("data"u8); writer.Write(44);
            for (int i = 0; i < 11; i++) { writer.Write(short.MinValue); writer.Write(short.MaxValue); }
        }

        using var wav = WavFile.Open(path);
        Assert.NotNull(wav);
        var peaks = wav.CreatePeaks();
        Assert.Equal(11, peaks.LengthMs);
        Assert.Equal([new Peak(short.MaxValue, short.MinValue), new Peak(short.MaxValue, short.MinValue)], peaks.Peaks);
    }

    [Fact]
    public void Reads_ffmpeg_list_metadata_and_odd_sized_chunks_before_audio()
    {
        string path = Path.Combine(_folder, "metadata.wav");
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write("RIFF"u8); writer.Write(78); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(1000);
            writer.Write(2000); writer.Write((short)2); writer.Write((short)16);
            writer.Write("JUNK"u8); writer.Write(1); writer.Write((byte)42); writer.Write((byte)0);
            writer.Write("LIST"u8); writer.Write(4); writer.Write("INFO"u8);
            writer.Write("data"u8); writer.Write(20); writer.Write(new byte[20]);
        }

        using var wav = WavFile.Open(path);
        Assert.NotNull(wav);
        Assert.Equal((uint)20, wav.DataSize);
        Assert.Single(wav.CreatePeaks().Peaks);
    }
}
