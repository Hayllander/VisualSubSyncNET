using VisualSubSync.Core.Settings;

namespace VisualSubSync.Media;

/// <summary>
/// Contrato do motor de reprodução, derivado do que Renderer.pas (TDShowRenderer) oferece ao restante
/// do programa. Cada candidato da prova de conceito implementa esta interface, para que a escolha
/// final não afete a interface nem o núcleo.
/// </summary>
public interface IMediaEngine : IAsyncDisposable
{
    bool IsOpen { get; }

    bool HasVideo { get; }

    bool IsPlaying { get; }

    int DurationMs { get; }

    /// <summary>Posição atual. Precisa ser barata: a forma de onda consulta a cada quadro desenhado.</summary>
    int PositionMs { get; }

    /// <summary>Posição suavizada (interpolada entre eventos do motor) para o cursor de reprodução.</summary>
    int InterpolatedPositionMs { get; }

    /// <summary>Quadros por segundo do vídeo; 0 quando não há vídeo. Usado pelo alinhamento de quadro.</summary>
    double FramesPerSecond { get; }

    /// <summary>Velocidade. O legado oferece 60%, 70%, 80%, 90% e 100%.</summary>
    double PlaybackRate { get; set; }

    double Volume { get; set; }

    (int Width, int Height) VideoSize { get; }

    Task OpenAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Toca de <paramref name="startMs"/> até <paramref name="stopMs"/> (ou até o fim, se nulo)
    /// e para exatamente no fim do trecho: base de "Play", "Loop" e "Play selection".</summary>
    void Play(int startMs, int? stopMs = null, bool loop = false);

    /// <summary>
    /// Troca o trecho em reprodução sem reiniciar (TDShowRenderer.UpdatePlayRange): usado quando a
    /// seleção muda enquanto toca. Sem efeito parado.
    /// </summary>
    void UpdatePlayRange(int startMs, int stopMs);

    /// <summary>Pausa mantendo o trecho atual (TWAVDisplayer.Pause → FRenderer.Pause).</summary>
    void Pause();

    /// <summary>Continua o trecho pausado (TWAVDisplayer.Pause → FRenderer.Resume).</summary>
    void Resume();

    void Stop();

    /// <summary>Unload the current file, like TDShowRenderer.Close, while retaining the video surface.</summary>
    void Close();

    /// <summary>Choose the project's WAV while video is hidden; null restores the container audio.</summary>
    void SetExternalAudio(string? path);

    /// <summary>Mostra o quadro exato do tempo pedido (Show start/stop frame).</summary>
    Task SeekAsync(int timeMs, CancellationToken cancellationToken = default);

    /// <summary>VideoRenderer.HideSubtitle: mostra ou esconde a legenda desenhada sobre o vídeo.</summary>
    bool SubtitlesVisible { get; set; }

    /// <summary>Atualiza as legendas sobrepostas ao vídeo (substitui o DirectVobSub do legado).</summary>
    void SetSubtitleOverlay(string? subtitlePath);

    /// <summary>Aplica o estilo da legenda mostrada sobre o vídeo, como o renderer do Enhanced.</summary>
    void SetSubtitleStyle(VideoSubtitleStyle style);

    event EventHandler? PlaybackStopped;
}

/// <summary>Configuração visual da legenda sobre o vídeo (fonte, transparências, contorno e sombra).</summary>
public sealed record VideoSubtitleStyle(
    FontSpec Font,
    int Transparency,
    int Outline,
    int OutlineColor,
    int TransparencyOutline,
    int Shadow,
    int ShadowColor,
    int TransparencyShadow);

/// <summary>Faixa de áudio disponível para extração no diálogo de novo projeto / WAV.</summary>
public sealed record AudioStreamInfo(int Index, string DisplayName);

/// <summary>Extrai o áudio de um vídeo para WAV e/ou tabela de picos (substitui o filtro WavWriter).</summary>
public interface IAudioExtractor
{
    /// <summary>Quantidade de fluxos de áudio do arquivo (TDSWavExtractor.AudioStreamCount).</summary>
    Task<int> CountAudioStreamsAsync(string mediaPath, CancellationToken cancellationToken);

    /// <summary>Lista as faixas de áudio com um rótulo amigável para a interface.</summary>
    Task<IReadOnlyList<AudioStreamInfo>> GetAudioStreamsAsync(string mediaPath, CancellationToken cancellationToken);

    Task<Core.Audio.PeakData> ExtractPeaksAsync(string mediaPath, string? wavOutputPath,
        int audioStreamIndex, IProgress<double>? progress, CancellationToken cancellationToken);
}

/// <summary>Lê os quadros-chave do vídeo para a detecção de mudança de cena (SceneChangeUnit.pas).</summary>
public interface IKeyframeReader
{
    Task<int[]> ReadKeyframesMsAsync(string mediaPath, CancellationToken cancellationToken);
}
