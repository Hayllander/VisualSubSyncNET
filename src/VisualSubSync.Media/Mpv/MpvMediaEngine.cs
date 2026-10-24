using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using static VisualSubSync.Media.Mpv.MpvNative;

namespace VisualSubSync.Media.Mpv;

/// <summary>
/// Motor libmpv para <see cref="IMediaEngine"/>. Uma thread lê os eventos do mpv; a posição vem
/// da observação de "time-pos" e o fim de trecho é tratado quando a posição alcança o limite.
/// </summary>
public sealed class MpvMediaEngine : IMediaEngine
{
    private const ulong ObsTimePos = 1, ObsDuration = 2, ObsWidth = 3, ObsHeight = 4, ObsFps = 5, ObsEof = 6;

    private readonly nint _ctx;
    private readonly Thread _eventThread;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly object _gate = new();
    private TaskCompletionSource? _openTcs;
    private TaskCompletionSource? _seekTcs;
    private bool _subtitlesVisible = true;
    private volatile int _positionMs;
    private long _positionStamp;
    private volatile bool _playing;
    private int? _rangeStartMs, _rangeStopMs;
    private bool _loop;
    private VideoSubtitleStyle? _subtitleStyle;
    private volatile bool _awaitingRestart;
    private double _rate = 1.0;
    private int _disposed;
    private long _internalAudioId;
    private long? _externalAudioId;

    /// <param name="windowHandle">HWND onde o vídeo é desenhado (opção "wid"). Nulo para usar <paramref name="headless"/>.</param>
    /// <param name="headless">Sem saída de vídeo nem áudio, para medições automáticas.</param>
    public MpvMediaEngine(nint? windowHandle = null, bool headless = false)
    {
        _ctx = Create();
        if (_ctx == 0)
        {
            throw new InvalidOperationException("mpv_create falhou.");
        }

        Opt("idle", "yes");
        Opt("keep-open", "always");
        Opt("pause", "yes");
        Opt("hr-seek", "yes");               // busca exata por padrão
        Opt("hr-seek-framedrop", "no");
        Opt("audio-pitch-correction", "yes"); // velocidade de 60 a 100% sem alterar o tom
        Opt("osc", "no");
        Opt("input-default-bindings", "no");
        Opt("input-vo-keyboard", "no");
        Opt("sub-auto", "no");
        Opt("terminal", "no");
        if (windowHandle is { } hwnd)
        {
            Opt("wid", ((long)hwnd).ToString(CultureInfo.InvariantCulture));
            Opt("vo", "gpu-next,gpu,direct3d");
            Opt("hwdec", "auto-safe");
        }
        else if (headless)
        {
            Opt("vo", "null");
            Opt("ao", "null");
        }

        Check(MpvNative.Initialize(_ctx), "mpv_initialize");
        ObserveProperty(_ctx, ObsTimePos, "time-pos", Format.Double);
        ObserveProperty(_ctx, ObsDuration, "duration", Format.Double);
        ObserveProperty(_ctx, ObsWidth, "dwidth", Format.Int64);
        ObserveProperty(_ctx, ObsHeight, "dheight", Format.Int64);
        ObserveProperty(_ctx, ObsFps, "container-fps", Format.Double);
        // Com keep-open o mpv pausa no último quadro sem alcançar o fim pedido (Play to end).
        ObserveProperty(_ctx, ObsEof, "eof-reached", Format.Flag);

        _eventThread = new Thread(EventLoop) { IsBackground = true, Name = "mpv events" };
        _eventThread.Start();
    }

    public bool IsOpen { get; private set; }

    public bool HasVideo => VideoSize.Width > 0;

    public bool IsPlaying => _playing;

    public int DurationMs { get; private set; }

    /// <summary>Última posição informada pelo mpv, sem interpolação.</summary>
    public int PositionMs => _positionMs;

    /// <summary>Posição interpolada pelo relógio desde o último evento: para o cursor andar suave.</summary>
    public int InterpolatedPositionMs => _playing
        ? _positionMs + (int)((_clock.ElapsedMilliseconds - Interlocked.Read(ref _positionStamp)) * _rate)
        : _positionMs;

    public double FramesPerSecond { get; private set; }

    public (int Width, int Height) VideoSize { get; private set; }

    /// <summary>Quantas atualizações de posição chegaram (para medir a frequência).</summary>
    public long PositionUpdates { get; private set; }

    public long SelectedAudioTrackId => GetInt64Property(_ctx, "aid", Format.Int64, out long value) >= 0 ? value : 0;

    public void SetExternalAudio(string? path)
    {
        if (_externalAudioId is { } external)
        {
            Check(MpvNative.Command(_ctx, "audio-remove", external.ToString(CultureInfo.InvariantCulture)), "audio-remove");
            SetPropertyString(_ctx, "aid", _internalAudioId > 0 ? _internalAudioId.ToString(CultureInfo.InvariantCulture) : "no");
            _externalAudioId = null;
        }

        if (path is null) return;
        _internalAudioId = SelectedAudioTrackId;
        Check(MpvNative.Command(_ctx, "audio-add", path, "select"), "audio-add");
        _externalAudioId = SelectedAudioTrackId;
    }

    /// <summary>Posição informada no instante em que o fim de trecho foi detectado.</summary>
    public int? LastRangeStopDetectedAtMs { get; private set; }

    public double PlaybackRate
    {
        get => _rate;
        set
        {
            _rate = value;
            double v = value;
            SetProperty(_ctx, "speed", Format.Double, ref v);
        }
    }

    /// <summary>
    /// A posição do tbVolume (0..100). TDShowRenderer.SetVolume aplica 1000·log2(v/100) centésimos de dB
    /// (-10 dB em 50); o mpv aplica (volume/100)³, então o mesmo ganho é 100·10^(dB/60).
    /// </summary>
    public double Volume
    {
        get => _volume;
        set
        {
            _volume = value;
            double v = value <= 0 ? 0 : 100 * Math.Pow(10, 10 * Math.Log2(value / 100.0) / 60);
            SetProperty(_ctx, "volume", Format.Double, ref v);
        }
    }

    private double _volume = 100;

    public event EventHandler? PlaybackStopped;

    public async Task OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        Close();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _openTcs = tcs;
        }

        IsOpen = false;
        Check(MpvNative.Command(_ctx, "loadfile", path, "replace"), "loadfile");
        using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
        {
            await tcs.Task.ConfigureAwait(false);
        }

        IsOpen = true;
    }

    public void Play(int startMs, int? stopMs = null, bool loop = false)
    {
        lock (_gate)
        {
            _rangeStartMs = startMs;
            _rangeStopMs = stopMs;
            _loop = loop && stopMs is not null;
            LastRangeStopDetectedAtMs = null;
        }

        _awaitingRestart = true;
        SeekCommand(startMs);
        SetPropertyString(_ctx, "pause", "no");
        _playing = true;
    }

    public void UpdatePlayRange(int startMs, int stopMs)
    {
        lock (_gate)
        {
            if (_rangeStopMs is null)
            {
                return; // trecho já terminou
            }

            _rangeStartMs = startMs;
            _rangeStopMs = stopMs;
        }
    }

    public void Pause()
    {
        SetPropertyString(_ctx, "pause", "yes");
        _playing = false;
    }

    public void Resume()
    {
        Interlocked.Exchange(ref _positionStamp, _clock.ElapsedMilliseconds);
        SetPropertyString(_ctx, "pause", "no");
        _playing = true;
    }

    public void Stop()
    {
        lock (_gate)
        {
            _rangeStopMs = null;
        }

        Pause();
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    public void Close()
    {
        SetPropertyString(_ctx, "pause", "yes");
        Check(MpvNative.Command(_ctx, "stop"), "stop");
        lock (_gate)
        {
            _playing = false;
            _awaitingRestart = false;
            _rangeStartMs = null;
            _rangeStopMs = null;
            _loop = false;
            _openTcs?.TrySetCanceled();
            _seekTcs?.TrySetCanceled();
            _openTcs = null;
            _seekTcs = null;
        }

        IsOpen = false;
        DurationMs = 0;
        _externalAudioId = null;
        _internalAudioId = 0;
        VideoSize = (0, 0);
        FramesPerSecond = 0;
        _positionMs = 0;
        LastRangeStopDetectedAtMs = null;
        Interlocked.Exchange(ref _positionStamp, _clock.ElapsedMilliseconds);
    }

    public async Task SeekAsync(int timeMs, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _seekTcs = tcs;
        }

        SeekCommand(timeMs);
        using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
        {
            await tcs.Task.ConfigureAwait(false);
        }
    }

    /// <summary>Lê "time-pos" diretamente (síncrono), sem esperar o próximo evento.</summary>
    public double QueryTimePosSeconds() => GetProperty(_ctx, "time-pos", Format.Double, out double v) >= 0 ? v : double.NaN;

    public bool SubtitlesVisible
    {
        get => _subtitlesVisible;
        set
        {
            _subtitlesVisible = value;
            SetPropertyString(_ctx, "sub-visibility", value ? "yes" : "no");
        }
    }

    public void SetSubtitleOverlay(string? subtitlePath)
    {
        MpvNative.Command(_ctx, "sub-remove");
        if (!string.IsNullOrEmpty(subtitlePath))
        {
            Check(MpvNative.Command(_ctx, "sub-add", subtitlePath, "select"), "sub-add");
            if (_subtitleStyle is { } style)
            {
                ApplySubtitleStyle(style);
                MpvNative.Command(_ctx, "sub-reload");
            }
        }
    }

    public void SetSubtitleStyle(VideoSubtitleStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        _subtitleStyle = style;
        ApplySubtitleStyle(style);
    }

    private void ApplySubtitleStyle(VideoSubtitleStyle style)
    {
        Check(MpvNative.Command(_ctx, "set", "sub-ass-override", "force"), "set sub-ass-override");
        Check(MpvNative.Command(_ctx, "set", "sub-ass-force-style", BuildAssForceStyle(style)), "set sub-ass-force-style");
    }

    /// <summary>Recarrega a legenda atual após edição (equivalente ao refresh do DirectVobSub).</summary>
    public void ReloadSubtitles() => MpvNative.Command(_ctx, "sub-reload");

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            // mpv_terminate_destroy encerra o laço de eventos (MPV_EVENT_SHUTDOWN).
            MpvNative.Command(_ctx, "quit");
            _eventThread.Join(TimeSpan.FromSeconds(5));
            TerminateDestroy(_ctx);
        }

        return ValueTask.CompletedTask;
    }

    private void SeekCommand(int timeMs) =>
        Check(MpvNative.Command(_ctx, "seek", (timeMs / 1000.0).ToString("0.000", CultureInfo.InvariantCulture), "absolute+exact"), "seek");

    private static string BuildAssForceStyle(VideoSubtitleStyle style)
    {
        string primaryColor = ToAssColorFromRgb(style.Font.ToRgb() ?? 0xFFFFFF, style.Transparency);
        string outlineColor = ToAssColorFromColorRef(style.OutlineColor, style.TransparencyOutline);
        string shadowColor = ToAssColorFromColorRef(style.ShadowColor, style.TransparencyShadow);

        return string.Create(CultureInfo.InvariantCulture,
            $"FontName={style.Font.Name}," +
            $"FontSize={style.Font.SizePt}," +
            $"PrimaryColour={primaryColor}," +
            $"SecondaryColour={primaryColor}," +
            $"OutlineColour={outlineColor}," +
            $"BackColour={shadowColor}," +
            $"Bold={(style.Font.Bold ? -1 : 0)}," +
            $"Italic={(style.Font.Italic ? -1 : 0)}," +
            $"Underline={(style.Font.Underline ? -1 : 0)}," +
            $"StrikeOut={(style.Font.Strikeout ? -1 : 0)}," +
            $"Outline={style.Outline}," +
            $"Shadow={style.Shadow}");
    }

    /// <summary>ASS usa &amp;HAABBGGRR, com alfa 0 opaco e 255 transparente.</summary>
    private static string ToAssColorFromRgb(int rgb, int transparency)
    {
        int alpha = Math.Clamp(transparency, 0, 10) * 255 / 10;
        int red = (rgb >> 16) & 0xFF;
        int green = (rgb >> 8) & 0xFF;
        int blue = rgb & 0xFF;

        return string.Create(CultureInfo.InvariantCulture, $"&H{alpha:X2}{blue:X2}{green:X2}{red:X2}");
    }

    private static string ToAssColorFromColorRef(int colorRef, int transparency)
    {
        int alpha = Math.Clamp(transparency, 0, 10) * 255 / 10;
        int red = colorRef & 0xFF;
        int green = (colorRef >> 8) & 0xFF;
        int blue = (colorRef >> 16) & 0xFF;
        return string.Create(CultureInfo.InvariantCulture, $"&H{alpha:X2}{blue:X2}{green:X2}{red:X2}");
    }

    private void EventLoop()
    {
        while (true)
        {
            var ev = Marshal.PtrToStructure<Event>(WaitEvent(_ctx, -1));
            switch (ev.EventIdValue)
            {
                case EventId.Shutdown:
                    return;
                case EventId.FileLoaded:
                    TakeTcs(ref _openTcs)?.TrySetResult();
                    break;
                case EventId.EndFile:
                    var end = Marshal.PtrToStructure<EventEndFile>(ev.Data);
                    if (end.Error < 0)
                    {
                        TakeTcs(ref _openTcs)?.TrySetException(new IOException($"mpv: {ErrorString(end.Error)}"));
                    }

                    break;
                case EventId.PlaybackRestart:
                    _awaitingRestart = false;
                    TakeTcs(ref _seekTcs)?.TrySetResult();
                    break;
                case EventId.PropertyChange:
                    OnPropertyChange(ev.ReplyUserData, Marshal.PtrToStructure<EventProperty>(ev.Data));
                    break;
            }
        }
    }

    private void OnPropertyChange(ulong id, EventProperty prop)
    {
        if (prop.Format == Format.None || prop.Data == 0)
        {
            return;
        }

        switch (id)
        {
            case ObsTimePos:
                double seconds = Marshal.PtrToStructure<double>(prop.Data);
                _positionMs = (int)Math.Round(seconds * 1000);
                Interlocked.Exchange(ref _positionStamp, _clock.ElapsedMilliseconds);
                PositionUpdates++;
                CheckRangeEnd();
                break;
            case ObsDuration:
                DurationMs = (int)Math.Round(Marshal.PtrToStructure<double>(prop.Data) * 1000);
                break;
            case ObsWidth:
                VideoSize = ((int)Marshal.ReadInt64(prop.Data), VideoSize.Height);
                break;
            case ObsHeight:
                VideoSize = (VideoSize.Width, (int)Marshal.ReadInt64(prop.Data));
                break;
            case ObsFps:
                FramesPerSecond = Marshal.PtrToStructure<double>(prop.Data);
                break;
            case ObsEof:
                if (Marshal.ReadInt32(prop.Data) != 0)
                {
                    OnEndOfFile();
                }

                break;
        }
    }

    private void CheckRangeEnd()
    {
        int start, stop;
        bool loop;
        lock (_gate)
        {
            if (!_playing || _awaitingRestart || _rangeStopMs is null || _rangeStartMs is null)
            {
                return;
            }

            start = _rangeStartMs.Value;
            stop = _rangeStopMs.Value;
            loop = _loop;
        }

        // Antecipa meio quadro: o próximo quadro já passaria do fim.
        double halfFrameMs = FramesPerSecond > 0 ? 500.0 / FramesPerSecond : 0;
        // TWaitCompletionThread também termina quando o início do trecho passa da posição
        // (UpdatePlayRange); a margem de um quadro absorve a busca exata, que para no quadro anterior.
        double frameMs = FramesPerSecond > 0 ? 1000.0 / FramesPerSecond : 50;
        if (_positionMs + halfFrameMs < stop && _positionMs + frameMs >= start)
        {
            return;
        }

        LastRangeStopDetectedAtMs = _positionMs;
        EndRange(start, loop);
    }

    /// <summary>Fim do arquivo durante a reprodução: o trecho termina (ou recomeça, em loop) como no fim pedido.</summary>
    private void OnEndOfFile()
    {
        int? start;
        bool loop;
        lock (_gate)
        {
            if (!_playing || _awaitingRestart)
            {
                return;
            }

            start = _rangeStartMs;
            loop = _loop;
        }

        LastRangeStopDetectedAtMs = _positionMs;
        EndRange(start ?? 0, loop && start is not null);
    }

    private void EndRange(int start, bool loop)
    {
        if (loop)
        {
            _awaitingRestart = true; // evita várias buscas enquanto a posição ainda está além do fim
            SeekCommand(start);
            SetPropertyString(_ctx, "pause", "no"); // keep-open pauses at EOF; a loop must resume.
            return;
        }

        lock (_gate)
        {
            _rangeStopMs = null;
        }

        SetPropertyString(_ctx, "pause", "yes");
        _playing = false;
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    private TaskCompletionSource? TakeTcs(ref TaskCompletionSource? field)
    {
        lock (_gate)
        {
            var tcs = field;
            field = null;
            return tcs;
        }
    }

    private void Opt(string name, string value) => Check(SetOptionString(_ctx, name, value), $"opção {name}");

    private static void Check(int result, string what)
    {
        if (result < 0)
        {
            throw new InvalidOperationException($"mpv: {what} falhou ({ErrorString(result)}).");
        }
    }
}
