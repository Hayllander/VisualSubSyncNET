using System.Text;

namespace VisualSubSync.Media.FFmpeg;

/// <summary>WAV PCM 16 bits, cabeçalho de 44 bytes corrigido ao fechar (como CWin32File/WAVFile.cpp).</summary>
internal sealed class WavFileWriter : IDisposable
{
    private readonly FileStream _stream;
    private readonly BinaryWriter _writer;
    private readonly int _channels, _sampleRate;
    private long _dataBytes;

    public WavFileWriter(string path, int channels, int sampleRate)
    {
        _channels = channels;
        _sampleRate = sampleRate;
        _stream = File.Create(path);
        _writer = new BinaryWriter(_stream, Encoding.ASCII);
        WriteHeader();
    }

    public void Write(ReadOnlySpan<short> samples)
    {
        _writer.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples));
        _dataBytes += samples.Length * 2;
    }

    public void Dispose()
    {
        _stream.Position = 0;
        WriteHeader();
        _writer.Dispose();
    }

    private void WriteHeader()
    {
        int blockAlign = _channels * 2;
        _writer.Write("RIFF"u8);
        _writer.Write((uint)Math.Min(uint.MaxValue, 36 + _dataBytes));
        _writer.Write("WAVEfmt "u8);
        _writer.Write(16);
        _writer.Write((short)1); // PCM
        _writer.Write((short)_channels);
        _writer.Write(_sampleRate);
        _writer.Write(_sampleRate * blockAlign);
        _writer.Write((short)blockAlign);
        _writer.Write((short)16);
        _writer.Write("data"u8);
        _writer.Write((uint)Math.Min(uint.MaxValue, _dataBytes));
    }
}
