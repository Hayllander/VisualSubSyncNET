using FFmpeg.AutoGen;

namespace VisualSubSync.Media.FFmpeg;

/// <summary>Quadros-chave lidos dos pacotes, sem decodificar (substitui MatroskaHelper/MP4File/VfW).</summary>
public sealed class FFmpegKeyframeReader : IKeyframeReader
{
    public Task<int[]> ReadKeyframesMsAsync(string mediaPath, CancellationToken cancellationToken) =>
        Task.Run(() => Read(mediaPath, cancellationToken), cancellationToken);

    private static unsafe int[] Read(string path, CancellationToken ct)
    {
        using var input = new MediaInput(path, AVMediaType.AVMEDIA_TYPE_VIDEO);
        var result = new SortedSet<int>();
        // ExtractKeyFramesAVI: 100 quadros-chave seguidos indicam um vídeo só de quadros-chave (MJPEG, DV...),
        // e então nenhuma mudança de cena é gravada.
        bool detectKeyframeOnly = string.Equals(Path.GetExtension(path), ".avi", StringComparison.OrdinalIgnoreCase);
        bool previousWasKey = false;
        int consecutiveKeyframes = 0;
        AVPacket* pkt = ffmpeg.av_packet_alloc();
        try
        {
            while (ffmpeg.av_read_frame(input.Format, pkt) >= 0)
            {
                ct.ThrowIfCancellationRequested();
                if (pkt->stream_index == input.StreamIndex)
                {
                    bool isKey = (pkt->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0;
                    if (isKey)
                    {
                        long ts = pkt->pts != ffmpeg.AV_NOPTS_VALUE ? pkt->pts : pkt->dts;
                        if (ts != ffmpeg.AV_NOPTS_VALUE)
                        {
                            // Trunc(...) como nos leitores AVI, MKV e MP4 do legado.
                            result.Add((int)Math.Floor(input.ToMs(ts)));
                        }

                        consecutiveKeyframes = previousWasKey ? consecutiveKeyframes + 1 : 0;
                        if (detectKeyframeOnly && consecutiveKeyframes == 100)
                        {
                            return [];
                        }
                    }

                    previousWasKey = isKey;
                }

                ffmpeg.av_packet_unref(pkt);
            }
        }
        finally
        {
            ffmpeg.av_packet_free(&pkt);
        }

        return [.. result];
    }
}
