namespace VisualSubSync.Core.Subtitles;

/// <summary>A cópia das legendas que UpdateSubtitleForPreview grava para o vídeo mostrar.</summary>
public static class SubtitlePreview
{
    /// <summary>
    /// GetTemporaryFolder + "VisualSubSync\" + nome do arquivo de legendas; sem pasta temporária,
    /// "~" + nome, ao lado do arquivo.
    /// </summary>
    public static string TemporaryFileName(string subtitlesFile, string? temporaryFolder = null)
    {
        ArgumentNullException.ThrowIfNull(subtitlesFile);
        string name = Path.GetFileName(subtitlesFile);
        string temp = temporaryFolder ?? Path.GetTempPath();
        return temp.Length > 0
            ? Path.Combine(temp, "VisualSubSync", name)
            : Path.Combine(Path.GetDirectoryName(subtitlesFile) ?? string.Empty, "~" + name);
    }
}
