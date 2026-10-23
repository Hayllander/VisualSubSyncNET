using VisualSubSync.Core.Audio;

namespace VisualSubSync.Core.Tests;

public class PeakFileTests
{
    [Fact]
    public void Roundtrip_keeps_legacy_binary_layout()
    {
        var data = new PeakData
        {
            LengthMs = 1234,
            SamplesPerSecond = 44_100,
            Channels = 2,
            BitsPerSample = 16,
            SamplesPerPeak = 441,
            Peaks = [new Peak(100, -100), new Peak(short.MaxValue, short.MinValue)],
        };

        using var ms = new MemoryStream();
        PeakFile.Write(data, ms);
        byte[] bytes = ms.ToArray();

        Assert.Equal(PeakFile.HeaderSize + (2 * 4), bytes.Length);
        Assert.Equal("PeakFile"u8.ToArray(), bytes[..8]);
        Assert.Equal(new byte[] { 0x00, 0x01, 0x00, 0x00 }, bytes[8..12]); // versão 0x0100
        Assert.Equal(new byte[] { 0x64, 0x00, 0x9C, 0xFF }, bytes[32..36]); // Max=100, Min=-100

        ms.Position = 0;
        var read = PeakFile.Read(ms);
        Assert.Equal(data.Peaks, read.Peaks);
        Assert.Equal(10.0, read.PeakDurationMs);
    }

    [Fact]
    public void Read_rejects_unknown_signature()
    {
        using var ms = new MemoryStream(new byte[64]);
        Assert.Throws<InvalidDataException>(() => PeakFile.Read(ms));
    }
}
