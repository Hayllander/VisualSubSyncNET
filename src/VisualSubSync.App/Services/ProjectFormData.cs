using VisualSubSync.Core.Projects;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.App.Services;

/// <summary>
/// Estado do TProjectForm (ProjectUnit.pas), nos dois modos: novo projeto e propriedades.
/// Os caminhos vazios equivalem ao "Leave empty for extraction" da interface.
/// </summary>
public sealed class ProjectFormData
{
    /// <summary>Novo projeto: legenda "New Project"; senão, "Project Properties".</summary>
    public bool NewProject { get; set; } = true;

    public string VideoSource { get; set; } = string.Empty;

    public string WavFile { get; set; } = string.Empty;

    public string PeakFile { get; set; } = string.Empty;

    public string SubtitlesFile { get; set; } = string.Empty;

    public string SubtitlesVO { get; set; } = string.Empty;

    public string ProjectFile { get; set; } = string.Empty;

    public ProjectWavMode WavMode { get; set; } = ProjectWavMode.NoWaveform;

    /// <summary>Faixa de áudio escolhida para gerar picos/WAV a partir do vídeo.</summary>
    public int AudioStreamIndex { get; set; }

    public bool SaveAsUtf8 { get; set; }
}

/// <summary>Tipo de extração do TExtractWAVForm (TWAVExtractionType em Renderer.pas).</summary>
public enum WavExtractionType
{
    OnlyPeakFile = 0,
    NoConversion = 1,
    FastConversion = 2,
}

/// <summary>Pedido do diálogo de extração: arquivo de vídeo, destino do WAV, modo inicial e faixa de áudio.</summary>
public sealed record ExtractWavRequest(string VideoFile, string DestinationFile, WavExtractionType Type, int AudioStreamIndex = 0);

/// <summary>
/// Resultado do TExtractWAVForm quando a extração termina (ModalResult = mrOk):
/// <see cref="OnlyPeak"/> equivale a <c>rbOnlyPeak.Checked</c>.
/// </summary>
public sealed record ExtractWavResult(bool OnlyPeak, string DestinationFile, int AudioStreamIndex);

/// <summary>TDelayApplyToType (SubStructUnit.pas).</summary>
public enum DelayApplyTo
{
    All,
    Selected,
    FromCursor,
}

/// <summary>Preenchimento de ActionDelayExecute: "Selected subtitles" e o tamanho da seleção da onda.</summary>
public sealed record DelayRequest(bool SelectedOnly, int? DelayMs);

/// <summary>O que TDelayForm devolve com Apply.</summary>
public sealed record DelayResult(int DelayMs, DelayApplyTo ApplyTo, DelayShiftType ShiftType);

/// <summary>O que TGotoForm devolve com Go: a linha (rbGotoLine) ou o tempo (rbGotoTime).</summary>
public sealed record GotoResult(int? Line, int? TimeMs);

/// <summary>TTranslateForm: tstMissingOnly, o tradutor e o arquivo de destino (tstAll).</summary>
public sealed record TranslateResult(bool MissingOnly, Func<string, string> Translate, string TargetFile);
