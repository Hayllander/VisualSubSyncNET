using System.Runtime.InteropServices;
using System.Text;

namespace VisualSubSync.Core.Audio;

/// <summary>
/// Arquivo .peak do legado (TWAVDisplayer.LoadWAV / SavePeakFile), little-endian:
/// <code>
/// "PeakFile"        8 bytes ASCII
/// versão            uint32 (0x0100)
/// LengthMs          int32
/// nSamplesPerSec    uint32
/// nChannels         uint16
/// wBitsPerSample    uint16
/// SamplesPerPeak    uint32
/// PeakTabSize       uint32
/// PeakTab           PeakTabSize × (int16 Max, int16 Min)
/// </code>
/// </summary>
public static class PeakFile
{
    public const string Extension = ".peak";
    public const uint Version = 0x0100;
    public const int HeaderSize = 8 + 4 + 4 + 4 + 2 + 2 + 4 + 4;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PeakFile");

    public static PeakData Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static PeakData Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        // O legado só exige tamanho maior que o cabeçalho; aqui também validamos a assinatura.
        if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic))
        {
            throw new InvalidDataException("Arquivo de picos sem a assinatura \"PeakFile\".");
        }

        uint version = reader.ReadUInt32();
        if (version != Version)
        {
            throw new InvalidDataException($"Versão de arquivo de picos não suportada: 0x{version:X4}.");
        }

        int lengthMs = reader.ReadInt32();
        uint samplesPerSec = reader.ReadUInt32();
        ushort channels = reader.ReadUInt16();
        ushort bits = reader.ReadUInt16();
        uint samplesPerPeak = reader.ReadUInt32();
        uint count = reader.ReadUInt32();

        var peaks = new Peak[count];
        // Como em PeakFS.Read do legado, um arquivo truncado deixa o restante zerado.
        var buffer = MemoryMarshal.AsBytes(peaks.AsSpan());
        stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);

        return new PeakData
        {
            LengthMs = lengthMs,
            SamplesPerSecond = samplesPerSec,
            Channels = channels,
            BitsPerSample = bits,
            SamplesPerPeak = samplesPerPeak,
            Peaks = peaks,
        };
    }

    public static void Save(PeakData data, string path)
    {
        using var stream = new MemoryStream();
        Write(data, stream);
        Text.AtomicFile.WriteAllBytes(path, stream.ToArray());
    }

    public static void Write(PeakData data, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(data.LengthMs);
        writer.Write(data.SamplesPerSecond);
        writer.Write(data.Channels);
        writer.Write(data.BitsPerSample);
        writer.Write(data.SamplesPerPeak);
        writer.Write((uint)data.Peaks.Length);
        writer.Write(MemoryMarshal.AsBytes(data.Peaks.AsSpan()));
    }

    /// <summary>
    /// NormalizePeakTab(-1) feito ao carregar: fator 32768 / maior valor absoluto, aplicado só quando
    /// passa de 1,1. Devolve true quando os picos mudaram (o legado então regrava o .peak).
    /// </summary>
    public static bool Normalize(PeakData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        int maxAbs = 0;
        foreach (var p in data.Peaks)
        {
            maxAbs = Math.Max(maxAbs, Math.Max(p.Max, -p.Min));
        }

        if (maxAbs == 0)
        {
            return false;
        }

        double factor = 32768.0 / maxAbs;
        if (factor <= 1.1)
        {
            return false;
        }

        var peaks = data.Peaks;
        for (int i = 0; i < peaks.Length; i++)
        {
            // Round do Delphi (arredonda para par) e atribuição a SmallInt sem verificação de faixa.
            peaks[i] = new Peak(
                unchecked((short)(int)Math.Round(peaks[i].Max * factor, MidpointRounding.ToEven)),
                unchecked((short)(int)Math.Round(peaks[i].Min * factor, MidpointRounding.ToEven)));
        }

        return true;
    }
}
