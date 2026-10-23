using System.Buffers.Binary;
using System.Text;

namespace VisualSubSync.Core.Audio;

/// <summary>
/// TWAVFile (WAVFileUnit.pas): leitura do cabeçalho de um WAV PCM e extração de trechos. Como no
/// legado, o bloco "data" tem de vir logo depois do "fmt ".
/// </summary>
public sealed class WavFile : IDisposable
{
    /// <summary>SizeOf(TWaveFormatEx): o registro empacotado de 18 bytes, com cbSize.</summary>
    private const int WaveFormatExSize = 18;

    private readonly FileStream _stream;
    private readonly byte[] _format;
    private readonly long _headerSize;

    private WavFile(FileStream stream, byte[] format, long headerSize, uint dataSize, bool damaged)
    {
        _stream = stream;
        _format = format;
        _headerSize = headerSize;
        DataSize = dataSize;
        IsDamaged = damaged;
    }

    /// <summary>O tamanho do RIFF não bate com o do arquivo (o legado avisa e continua).</summary>
    public bool IsDamaged { get; }

    public uint DataSize { get; }

    public int Channels => BinaryPrimitives.ReadUInt16LittleEndian(_format.AsSpan(2));

    public int SamplesPerSecond => (int)BinaryPrimitives.ReadUInt32LittleEndian(_format.AsSpan(4));

    public int AverageBytesPerSecond => (int)BinaryPrimitives.ReadUInt32LittleEndian(_format.AsSpan(8));

    public int BlockAlign => BinaryPrimitives.ReadUInt16LittleEndian(_format.AsSpan(12));

    public int BitsPerSample => BinaryPrimitives.ReadUInt16LittleEndian(_format.AsSpan(14));

    /// <summary>TWAVFile.Open: null quando o arquivo não existe ou o cabeçalho não é reconhecido.</summary>
    public static WavFile? Open(string fileName)
    {
        if (!File.Exists(fileName))
        {
            return null;
        }

        FileStream stream;
        try
        {
            stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var result = ReadHeader(stream);
        if (result is null)
        {
            stream.Dispose();
        }

        return result;
    }

    private static WavFile? ReadHeader(FileStream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        if (!ReadTag(stream, buffer, "RIFF") || !ReadInt(stream, buffer, out int riffSize))
        {
            return null;
        }

        bool damaged = unchecked((uint)riffSize) + 8L != stream.Length;
        if (!ReadTag(stream, buffer, "WAVE")) return null;
        byte[]? format = null;
        Span<byte> number = stackalloc byte[4];
        while (stream.Position + 8 <= stream.Length)
        {
            if (stream.ReadAtLeast(buffer, 4, throwOnEndOfStream: false) != 4 || !ReadInt(stream, number, out int rawSize)) return null;
            string tag = Encoding.ASCII.GetString(buffer);
            uint chunkSize = unchecked((uint)rawSize);
            long start = stream.Position;
            if (tag == "fmt ")
            {
                if (chunkSize < 16) return null;
                format = new byte[WaveFormatExSize];
                int size = (int)Math.Min(WaveFormatExSize, chunkSize);
                if (stream.ReadAtLeast(format.AsSpan(0, size), size, throwOnEndOfStream: false) != size) return null;
            }
            else if (tag == "data" && format is not null)
            {
                return new WavFile(stream, format, stream.Position, chunkSize, damaged);
            }

            // Enhanced added support for FFmpeg LIST metadata. Walk RIFF chunks rather than assuming 30 bytes.
            long next = start + chunkSize + (chunkSize & 1);
            if (next > stream.Length) return null;
            stream.Position = next;
        }

        return null;
    }

    private static bool ReadTag(Stream stream, Span<byte> buffer, string tag) =>
        stream.ReadAtLeast(buffer, 4, throwOnEndOfStream: false) == 4 && Encoding.ASCII.GetString(buffer) == tag;

    private static bool ReadInt(Stream stream, Span<byte> buffer, out int value)
    {
        bool ok = stream.ReadAtLeast(buffer, 4, throwOnEndOfStream: false) == 4;
        value = ok ? BinaryPrimitives.ReadInt32LittleEndian(buffer) : 0;
        return ok;
    }

    /// <summary>
    /// TWAVFile.ExtractToStream: grava em <paramref name="output"/> um WAV com o trecho pedido, alinhado
    /// às amostras, e devolve o tamanho total escrito. Falha como CopyFrom quando o áudio acaba antes.
    /// </summary>
    public long ExtractToStream(int startMs, int stopMs, Stream output)
    {
        uint start = AlignedOffset(startMs);
        uint stop = AlignedOffset(stopMs);
        uint length = stop - start;
        uint total = (uint)(4 + 4 + 8 + 4 + WaveFormatExSize + 4 + 4) + length;

        Span<byte> number = stackalloc byte[4];
        output.Write("RIFF"u8);
        BinaryPrimitives.WriteUInt32LittleEndian(number, total - 8);
        output.Write(number);
        output.Write("WAVEfmt "u8);
        BinaryPrimitives.WriteUInt32LittleEndian(number, WaveFormatExSize);
        output.Write(number);
        output.Write(_format);
        output.Write("data"u8);
        BinaryPrimitives.WriteUInt32LittleEndian(number, length);
        output.Write(number);

        _stream.Seek(_headerSize + start, SeekOrigin.Begin);
        var chunk = new byte[64 * 1024];
        long remaining = length;
        while (remaining > 0)
        {
            int read = _stream.Read(chunk, 0, (int)Math.Min(chunk.Length, remaining));
            if (read == 0)
            {
                throw new EndOfStreamException("Stream read error");
            }

            output.Write(chunk, 0, read);
            remaining -= read;
        }

        return total;
    }

    private uint AlignedOffset(int timeMs)
    {
        uint offset = (uint)Math.Round(timeMs / 1000.0 * AverageBytesPerSecond);
        return BlockAlign > 0 ? offset - (offset % (uint)BlockAlign) : offset;
    }

    public void Dispose() => _stream.Dispose();

    /// <summary>TWAVDisplayer.CreatePeakTab: one min/max pair per ~10 ms across all channels.</summary>
    /// <summary>CreatePeakTab; <paramref name="progress"/> recebe (i * 100) div PeakTabSize, como pfcevtProgress.</summary>
    public PeakData CreatePeaks(IProgress<int>? progress = null)
    {
        if (Channels <= 0 || SamplesPerSecond < 100 || AverageBytesPerSecond <= 0 || BitsPerSample is not (8 or 16 or 32))
        {
            throw new NotSupportedException("WAV peak generation supports PCM 8/16-bit and float 32-bit audio.");
        }

        int formatTag = BinaryPrimitives.ReadUInt16LittleEndian(_format);
        if ((BitsPerSample == 32 && formatTag != 3) || (BitsPerSample != 32 && formatTag != 1))
        {
            throw new NotSupportedException("Unsupported WAV sample encoding.");
        }

        int framesPerPeak = SamplesPerSecond / 100;
        int bytesPerSample = BitsPerSample / 8;
        int blockBytes = checked(framesPerPeak * Channels * bytesPerSample);
        int count = checked((int)(((long)DataSize + blockBytes - 1) / blockBytes));
        var peaks = new Peak[count];
        var block = new byte[blockBytes];
        _stream.Position = _headerSize;
        long remaining = DataSize;
        int lastPercent = -1;
        for (int i = 0; i < count; i++)
        {
            Array.Clear(block);
            int wanted = (int)Math.Min(remaining, blockBytes);
            _stream.ReadAtLeast(block.AsSpan(0, wanted), wanted, throwOnEndOfStream: true);
            remaining -= wanted;
            short min = short.MaxValue, max = short.MinValue;
            for (int offset = 0; offset < block.Length; offset += bytesPerSample)
            {
                short sample = BitsPerSample switch
                {
                    8 => (short)((block[offset] - 128) << 8),
                    16 => BinaryPrimitives.ReadInt16LittleEndian(block.AsSpan(offset)),
                    _ => (short)Math.Clamp(Math.Round(BitConverter.Int32BitsToSingle(
                        BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(offset))) * 32767.0), short.MinValue, short.MaxValue),
                };
                min = Math.Min(min, sample);
                max = Math.Max(max, sample);
            }

            peaks[i] = new Peak(max, min);
            int percent = (int)((long)i * 100 / count);
            if (percent != lastPercent)
            {
                lastPercent = percent;
                progress?.Report(percent);
            }
        }

        var result = new PeakData
        {
            LengthMs = (int)Math.Round(DataSize * 1000.0 / AverageBytesPerSecond),
            SamplesPerSecond = (uint)SamplesPerSecond,
            Channels = (ushort)Channels,
            BitsPerSample = (ushort)BitsPerSample,
            SamplesPerPeak = (uint)framesPerPeak,
            Peaks = peaks,
        };
        PeakFile.Normalize(result);
        return result;
    }
}
