namespace VisualSubSync.Core.Projects;

/// <summary>
/// Arquivos soltos na janela principal (não existia no VSS original; segue a regra do
/// ProcessParams e do TProjectForm). Um .vssprj abre o projeto; vídeo, legendas, WAV e picos
/// preenchem o diálogo de novo projeto. A primeira legenda é a legenda do projeto e a segunda, a VO.
/// </summary>
public sealed record DroppedFiles(
    string? ProjectFile,
    string? VideoFile,
    string? SubtitleFile,
    string? SubtitleVoFile,
    string? WavFile,
    string? PeakFile)
{
    /// <summary>Extensões do filtro de vídeo do TProjectForm e alguns contêineres atuais.</summary>
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avi", ".ogm", ".mkv", ".mka", ".mp4", ".m4v", ".divx", ".rm", ".rmvb", ".mpeg", ".mpg",
        ".vob", ".avs", ".wmv", ".mov", ".flv", ".webm", ".ts", ".m2ts", ".mts",
    };

    /// <summary>Nenhum arquivo reconhecido: a soltura não faz nada.</summary>
    public bool IsEmpty => ProjectFile is null && !HasNewProjectFiles;

    /// <summary>Há algo para preencher o diálogo de novo projeto.</summary>
    public bool HasNewProjectFiles =>
        VideoFile is not null || SubtitleFile is not null || WavFile is not null || PeakFile is not null;

    /// <summary>O arquivo tem uma extensão que a soltura reconhece.</summary>
    public static bool IsSupported(string path) => Kind(path) is not FileKind.Other;

    /// <summary>Separa os caminhos por tipo; pastas e extensões desconhecidas são ignoradas.</summary>
    public static DroppedFiles Classify(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        string? project = null, video = null, subtitle = null, vo = null, wav = null, peak = null;
        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path))
            {
                continue;
            }

            switch (Kind(path))
            {
                case FileKind.Project:
                    project ??= path;
                    break;
                case FileKind.Video:
                    video ??= path;
                    break;
                case FileKind.Subtitle when subtitle is null:
                    subtitle = path;
                    break;
                case FileKind.Subtitle:
                    vo ??= path;
                    break;
                case FileKind.Wav:
                    wav ??= path;
                    break;
                case FileKind.Peak:
                    peak ??= path;
                    break;
            }
        }

        return new DroppedFiles(project, video, subtitle, vo, wav, peak);
    }

    private static FileKind Kind(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".vssprj" => FileKind.Project,
            ".srt" or ".ssa" or ".ass" => FileKind.Subtitle,
            ".wav" => FileKind.Wav,
            ".peak" => FileKind.Peak,
            _ when VideoExtensions.Contains(ext) => FileKind.Video,
            _ => FileKind.Other,
        };
    }

    private enum FileKind
    {
        Other,
        Project,
        Video,
        Subtitle,
        Wav,
        Peak,
    }
}
