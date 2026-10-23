using FFmpeg.AutoGen;
using static VisualSubSync.Media.FFmpeg.FFmpegLoader;

namespace VisualSubSync.Media.FFmpeg;

/// <summary>Arquivo aberto com um único fluxo selecionado; os demais são descartados na leitura.</summary>
internal sealed unsafe class MediaInput : IDisposable
{
    private AVFormatContext* _format;

    public MediaInput(string path, AVMediaType type, int? streamOrdinal = null)
    {
        EnsureLoaded();
        AVFormatContext* fmt = null;
        Check(ffmpeg.avformat_open_input(&fmt, path, null, null), $"abrir \"{path}\"");
        _format = fmt;
        try
        {
            Check(ffmpeg.avformat_find_stream_info(fmt, null), "ler informações dos fluxos");
    
            if (streamOrdinal is null)
            {
                AVCodec* codec = null;
                StreamIndex = Check(ffmpeg.av_find_best_stream(fmt, type, -1, -1, &codec, 0), $"encontrar fluxo {type}");
                Codec = codec;
            }
            else
            {
                StreamIndex = FindStreamByOrdinal(fmt, type, streamOrdinal.Value);
                Codec = ffmpeg.avcodec_find_decoder(fmt->streams[StreamIndex]->codecpar->codec_id);
                if (Codec is null)
                {
                    throw new InvalidOperationException($"Decodificador ausente para o fluxo {StreamIndex}.");
                }
            }
    
            for (int i = 0; i < fmt->nb_streams; i++)
            {
                fmt->streams[i]->discard = i == StreamIndex ? AVDiscard.AVDISCARD_DEFAULT : AVDiscard.AVDISCARD_ALL;
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public AVFormatContext* Format => _format;

    public int StreamIndex { get; }

    public AVCodec* Codec { get; }

    public AVStream* Stream => _format->streams[StreamIndex];

    /// <summary>Início do arquivo em ms (o mpv mostra tempos relativos a ele).</summary>
    public double StartMs => _format->start_time == ffmpeg.AV_NOPTS_VALUE ? 0 : _format->start_time / 1000.0;

    public double DurationMs => _format->duration == ffmpeg.AV_NOPTS_VALUE ? 0 : _format->duration / 1000.0;

    public double ToMs(long ts) => (ts * ffmpeg.av_q2d(Stream->time_base) * 1000.0) - StartMs;

    private static int FindStreamByOrdinal(AVFormatContext* format, AVMediaType type, int ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);

        int current = 0;
        for (int i = 0; i < format->nb_streams; i++)
        {
            if (format->streams[i]->codecpar->codec_type != type)
            {
                continue;
            }

            if (current == ordinal)
            {
                return i;
            }

            current++;
        }

        throw new InvalidOperationException($"Fluxo {type} #{ordinal + 1} não encontrado.");
    }

    public void Dispose()
    {
        var fmt = _format;
        if (fmt != null)
        {
            ffmpeg.avformat_close_input(&fmt);
            _format = null;
        }
    }
}
