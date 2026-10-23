using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Subtitles.Formats;

/// <summary>
/// Abre e salva legendas pela extensão, como TMainForm.LoadSubtitles e SaveSubtitles.
/// Leitura: .srt, .ass e .ssa. Gravação: também .cue, .txt e .csv.
/// </summary>
public static class SubtitleFile
{
    public static IReadOnlyList<string> ReadableExtensions { get; } = [".srt", ".ass", ".ssa"];

    public static IReadOnlyList<string> WritableExtensions { get; } = [".srt", ".ass", ".ssa", ".cue", ".txt", ".csv"];

    public static bool CanRead(string path) => ReadableExtensions.Contains(Extension(path));

    public static bool CanWrite(string path) => WritableExtensions.Contains(Extension(path));

    /// <summary>A coluna de estilo só aparece para SSA/ASS.</summary>
    public static bool HasStyles(string path) => Extension(path) is ".ass" or ".ssa";

    /// <summary>ConfigureMode(sfmASS): o arquivo é .ass.</summary>
    public static bool IsAss(string path) => Extension(path) == ".ass";

    public static SubtitleDocument Load(string path)
    {
        var (text, encoding) = TextFileReader.ReadAllText(path);
        var document = Parse(text, Extension(path));
        document.IsUtf8 = TextFileReader.IsUnicode(encoding);
        return document;
    }

    /// <summary>Analisa o texto segundo a extensão (".srt", ".ass" ou ".ssa").</summary>
    public static SubtitleDocument Parse(string text, string extension)
    {
        ArgumentNullException.ThrowIfNull(text);
        SubtitleDocument document;
        switch (extension.ToLowerInvariant())
        {
            case ".srt":
                document = SrtFormat.Parse(text);
                break;
            case ".ass" or ".ssa":
                document = SsaFormat.Parse(text).Document;
                document.EnsureDefaultStyle();
                break;
            default:
                throw new NotSupportedException($"Unsupported subtitle format \"{extension}\".");
        }

        return document;
    }

    /// <summary>Texto que seria gravado para a extensão, antes da codificação.</summary>
    public static string Format(SubtitleDocument document, string extension, SubtitleWriteOptions? options = null) =>
        extension.ToLowerInvariant() switch
        {
            ".srt" => SrtFormat.Format(document, options),
            ".ass" => SsaFormat.Format(document, ass: true, options),
            ".ssa" => SsaFormat.Format(document, ass: false, options),
            ".cue" => ExportFormats.FormatCue(document),
            ".txt" => ExportFormats.FormatTxt(document),
            ".csv" => ExportFormats.FormatCsv(document),
            _ => throw new NotSupportedException($"Unsupported subtitle format \"{extension}\"."),
        };

    /// <summary>
    /// Grava em UTF-8 com BOM ou ANSI conforme <see cref="SubtitleWriteOptions.Utf8"/> (padrão: o documento).
    /// CUE é sempre ANSI, como no legado. <paramref name="formatOf"/> escolhe o formato por outro
    /// arquivo: a cópia automática (.bak) sai no formato da legenda do projeto.
    /// </summary>
    public static void Save(SubtitleDocument document, string path, SubtitleWriteOptions? options = null, string? formatOf = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new SubtitleWriteOptions();
        string extension = Extension(formatOf ?? path);
        string text = Format(document, extension, options);
        bool utf8 = extension != ".cue" && (options.Utf8 ?? document.IsUtf8);
        TextFileWriter.Write(path, text, utf8);
    }

    /// <summary>
    /// Regra de ActionSaveExecute: se o documento é ANSI e algum texto não cabe na página ANSI,
    /// passa a ser UTF-8 dali em diante. Devolve verdadeiro quando mudou (o legado avisa o usuário).
    /// </summary>
    public static bool PromoteToUtf8IfNeeded(SubtitleDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.IsUtf8 || !document.Items.Any(static s => TextFileWriter.WouldLoseDataInAnsi(s.Text)))
        {
            return false;
        }

        document.IsUtf8 = true;
        return true;
    }

    /// <summary>
    /// Cópia de segurança antes de sobrescrever: &lt;pasta&gt;\&lt;nome&gt;.bak, como SaveSubtitles com EnableBackup.
    /// </summary>
    public static string? Backup(string path, string backupDirectory)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        Directory.CreateDirectory(backupDirectory);
        string destination = Path.Combine(backupDirectory, Path.ChangeExtension(Path.GetFileName(path), ".bak"));
        File.Copy(path, destination, overwrite: true);
        return destination;
    }

    private static string Extension(string path) => Path.GetExtension(path).ToLowerInvariant();
}
