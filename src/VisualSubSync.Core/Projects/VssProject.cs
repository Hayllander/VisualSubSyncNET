namespace VisualSubSync.Core.Projects;

/// <summary>Modo de forma de onda do projeto (TProjectWAVMode). Os valores numéricos são gravados no arquivo.</summary>
public enum ProjectWavMode
{
    NoWaveform = 0,
    External = 1,
    PeakOnly = 2,
}

/// <summary>
/// Projeto .vssprj (TVSSProject em ProjectUnit.pas). Caminhos ficam absolutos em memória;
/// <see cref="VssProjectFile"/> converte de/para relativos como o legado.
/// </summary>
public sealed class VssProject
{
    public string FileName { get; set; } = string.Empty;

    public string VideoSource { get; set; } = string.Empty;

    public string WavFile { get; set; } = string.Empty;

    public string PeakFile { get; set; } = string.Empty;

    public ProjectWavMode WavMode { get; set; } = ProjectWavMode.External;

    public string SubtitlesFile { get; set; } = string.Empty;

    public string SubtitlesVO { get; set; } = string.Empty;

    public string TextPipeSource { get; set; } = string.Empty;

    public int TextPipePosition { get; set; }

    public int VideoPanelWidth { get; set; }

    public int VideoPanelHeight { get; set; }

    public bool ShowVideo { get; set; }

    public bool ShowVO { get; set; }

    public bool DetachedVideo { get; set; }

    public int VideoWindowNormalLeft { get; set; }

    public int VideoWindowNormalTop { get; set; }

    public int VideoWindowNormalWidth { get; set; }

    public int VideoWindowNormalHeight { get; set; }

    public int WavDisplayerPositionStartMs { get; set; } = -1;

    public int WavDisplayerPositionStopMs { get; set; } = -1;

    public int FocusedTimeMs { get; set; } = -1;

    /// <summary>Nome do dicionário. A grafia "Dictionnary" é a chave original e deve ser mantida.</summary>
    public string Dictionary { get; set; } = string.Empty;

    public string Presets { get; set; } = string.Empty;
}
