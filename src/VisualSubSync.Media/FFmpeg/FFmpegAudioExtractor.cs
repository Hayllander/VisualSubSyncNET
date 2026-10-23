using System.Globalization;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using VisualSubSync.Core.Audio;
using static VisualSubSync.Media.FFmpeg.FFmpegLoader;

namespace VisualSubSync.Media.FFmpeg;

/// <summary>
/// Decodifica o áudio com FFmpeg e produz a tabela de picos e, opcionalmente, o WAV
/// (substitui o filtro DirectShow WavWriter). Modos do diálogo de extração original:
/// <list type="bullet">
/// <item>Só picos (<c>wavOutputPath</c> nulo): taxa e canais originais.</item>
/// <item>Conversão rápida (<c>fastConversion</c>): WAV mono, metade da taxa.</item>
/// <item>Sem conversão: WAV com taxa e canais originais.</item>
/// </list>
/// Sempre 16 bits, um pico a cada 10 ms (taxa de saída ÷ 100).
/// </summary>
public sealed class FFmpegAudioExtractor(bool fastConversion = true) : IAudioExtractor
{
    public Task<int> CountAudioStreamsAsync(string mediaPath, CancellationToken cancellationToken) =>
        Task.Run(() => ProbeAudioStreamCount(mediaPath), cancellationToken);

    public Task<IReadOnlyList<AudioStreamInfo>> GetAudioStreamsAsync(string mediaPath, CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<AudioStreamInfo>>(() => ProbeAudioStreams(mediaPath), cancellationToken);

    public Task<PeakData> ExtractPeaksAsync(string mediaPath, string? wavOutputPath,
        int audioStreamIndex, IProgress<double>? progress, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            string? temporary = wavOutputPath is null ? null : wavOutputPath + $".{Guid.NewGuid():N}.tmp";
            try
            {
                PeakData result;
                using (var session = new Session(mediaPath, temporary, audioStreamIndex, wavOutputPath is not null && fastConversion))
                {
                    result = session.Run(progress, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (temporary is not null)
                {
                    if (File.Exists(wavOutputPath)) File.Replace(temporary, wavOutputPath!, null);
                    else File.Move(temporary, wavOutputPath!);
                }

                return result;
            }
            finally
            {
                if (temporary is not null && File.Exists(temporary)) File.Delete(temporary);
            }
        }, cancellationToken);

    /// <summary>Conta os fluxos de áudio sem decodificar, como a enumeração de pinos do DirectShow.</summary>
    private static unsafe int ProbeAudioStreamCount(string path)
    {
        return ProbeAudioStreams(path).Count;
    }

    private static unsafe List<AudioStreamInfo> ProbeAudioStreams(string path)
    {
        EnsureLoaded();
        AVFormatContext* format = null;
        Check(ffmpeg.avformat_open_input(&format, path, null, null), $"abrir \"{path}\"");
        try
        {
            Check(ffmpeg.avformat_find_stream_info(format, null), "ler informações dos fluxos");
            var result = new List<AudioStreamInfo>();
            int count = 0;
            for (int i = 0; i < format->nb_streams; i++)
            {
                var stream = format->streams[i];
                if (stream->codecpar->codec_type != AVMediaType.AVMEDIA_TYPE_AUDIO)
                {
                    continue;
                }

                result.Add(new AudioStreamInfo(count, BuildDisplayName(stream, count)));
                count++;
            }

            return result;
        }
        finally
        {
            ffmpeg.avformat_close_input(&format);
        }
    }

    private static unsafe string BuildDisplayName(AVStream* stream, int index)
    {
        var codec = stream->codecpar;
        string codecName = ffmpeg.avcodec_get_name(codec->codec_id) ?? "audio";
        codecName = codecName.ToUpperInvariant();
        int channels = codec->ch_layout.nb_channels;
        string bitrate = codec->bit_rate > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{FormatNumber(codec->bit_rate / 1000)} kb/s")
            : "? kb/s";
        string sampleRate = codec->sample_rate > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{codec->sample_rate / 1000.0:0.0} kHz")
            : "? kHz";
        string language = GetLanguageLabel(ReadMetadata(stream->metadata, "language"));
        string suffix = language.Length > 0 ? string.Create(CultureInfo.InvariantCulture, $" [{language}]") : string.Empty;
        return string.Create(CultureInfo.InvariantCulture,
            $"{index + 1:00}: {codecName} {channels} channels {bitrate} {sampleRate}{suffix}");
    }

    private static string FormatNumber(long value)
    {
        var info = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        info.NumberGroupSeparator = " ";
        return value.ToString("N0", info);
    }

    private static unsafe string ReadMetadata(AVDictionary* metadata, string key)
    {
        var entry = ffmpeg.av_dict_get(metadata, key, null, 0);
        return entry is null ? string.Empty : Marshal.PtrToStringAnsi((nint)entry->value) ?? string.Empty;
    }

    private static string GetLanguageLabel(string code) => code.ToLowerInvariant() switch
    {
        "eng" or "en" => "English",
        "chi" or "zho" or "zh" => "Chinese",
        "por" or "pt" => "Portuguese",
        "spa" or "es" => "Spanish",
        "ita" or "it" => "Italian",
        "fra" or "fre" or "fr" => "French",
        "deu" or "ger" or "de" => "German",
        "jpn" or "ja" => "Japanese",
        "kor" or "ko" => "Korean",
        "rus" or "ru" => "Russian",
        "ara" or "ar" => "Arabic",
        "ces" or "cze" or "cs" => "Czech",
        "ell" or "gre" or "el" => "Greek",
        "ron" or "rum" or "ro" => "Romanian",
        "tur" or "tr" => "Turkish",
        "pol" or "pl" => "Polish",
        _ => code.Length == 0 ? string.Empty : code.ToUpperInvariant(),
    };

    private sealed unsafe class Session : IDisposable
    {
        private readonly MediaInput _input;
        private readonly PeakBuilder _peaks;
        private readonly WavFileWriter? _wav;
        private readonly int _outChannels, _outRate;
        private AVCodecContext* _cc;
        private SwrContext* _swr;
        private AVPacket* _pkt;
        private AVFrame* _frame;
        private short[] _buffer = new short[8192];

        public Session(string path, string? wavPath, int audioStreamIndex, bool convert)
        {
            _input = new MediaInput(path, AVMediaType.AVMEDIA_TYPE_AUDIO, audioStreamIndex);
            try
            {
                _cc = ffmpeg.avcodec_alloc_context3(_input.Codec);
                _pkt = ffmpeg.av_packet_alloc();
                _frame = ffmpeg.av_frame_alloc();
    
                Check(ffmpeg.avcodec_parameters_to_context(_cc, _input.Stream->codecpar), "copiar parâmetros");
                _cc->pkt_timebase = _input.Stream->time_base;
                Check(ffmpeg.avcodec_open2(_cc, _input.Codec, null), "abrir decodificador");
    
                int inRate = _cc->sample_rate;
                _outChannels = convert ? 1 : _cc->ch_layout.nb_channels;
                _outRate = convert ? inRate / 2 : inRate;
    
                AVChannelLayout outLayout;
                ffmpeg.av_channel_layout_default(&outLayout, _outChannels);
                SwrContext* swr = null;
                Check(ffmpeg.swr_alloc_set_opts2(&swr, &outLayout, AVSampleFormat.AV_SAMPLE_FMT_S16, _outRate,
                    &_cc->ch_layout, _cc->sample_fmt, inRate, 0, null), "configurar conversão");
                _swr = swr;
                Check(ffmpeg.swr_init(_swr), "iniciar conversão");
    
                _peaks = new PeakBuilder(_outChannels, _outRate / 100);
                _wav = wavPath is null ? null : new WavFileWriter(wavPath, _outChannels, _outRate);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public PeakData Run(IProgress<double>? progress, CancellationToken ct)
        {
            double totalMs = Math.Max(1, _input.DurationMs);
            while (ffmpeg.av_read_frame(_input.Format, _pkt) >= 0)
            {
                ct.ThrowIfCancellationRequested();
                if (_pkt->stream_index == _input.StreamIndex && ffmpeg.avcodec_send_packet(_cc, _pkt) >= 0)
                {
                    Drain(progress, totalMs);
                }

                ffmpeg.av_packet_unref(_pkt);
            }

            ffmpeg.avcodec_send_packet(_cc, null);
            Drain(progress, totalMs);
            Output(null, 0); // amostras retidas pelo conversor
            progress?.Report(1);

            long frames = _peaks.TotalSamples / _outChannels;
            return new PeakData
            {
                LengthMs = (int)Math.Round(frames * 1000.0 / _outRate),
                SamplesPerSecond = (uint)_outRate,
                Channels = (ushort)_outChannels,
                BitsPerSample = 16,
                SamplesPerPeak = (uint)(_outRate / 100),
                Peaks = _peaks.Finish(),
            };
        }

        private void Drain(IProgress<double>? progress, double totalMs)
        {
            while (ffmpeg.avcodec_receive_frame(_cc, _frame) == 0)
            {
                Output(_frame->extended_data, _frame->nb_samples);
                if (progress is not null && _frame->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE)
                {
                    progress.Report(Math.Clamp(_input.ToMs(_frame->best_effort_timestamp) / totalMs, 0, 1));
                }

                ffmpeg.av_frame_unref(_frame);
            }
        }

        private void Output(byte** data, int count)
        {
            int max = ffmpeg.swr_get_out_samples(_swr, count);
            if (max <= 0)
            {
                return;
            }

            if (_buffer.Length < max * _outChannels)
            {
                _buffer = new short[max * _outChannels * 2];
            }

            int produced;
            fixed (short* p = _buffer)
            {
                byte* outPtr = (byte*)p;
                produced = Check(ffmpeg.swr_convert(_swr, &outPtr, max, data, count), "converter amostras");
            }

            var samples = _buffer.AsSpan(0, produced * _outChannels);
            _peaks.Add(samples);
            _wav?.Write(samples);
        }

        public void Dispose()
        {
            _wav?.Dispose();
            var frame = _frame;
            ffmpeg.av_frame_free(&frame);
            var pkt = _pkt;
            ffmpeg.av_packet_free(&pkt);
            var swr = _swr;
            ffmpeg.swr_free(&swr);
            var cc = _cc;
            ffmpeg.avcodec_free_context(&cc);
            _frame = null;
            _pkt = null;
            _swr = null;
            _cc = null;
            _input.Dispose();
        }
    }
}
