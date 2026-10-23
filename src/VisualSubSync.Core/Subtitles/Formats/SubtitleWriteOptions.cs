namespace VisualSubSync.Core.Subtitles.Formats;

/// <summary>Parâmetros de SaveSubtitles que não fazem parte do documento.</summary>
public sealed record SubtitleWriteOptions
{
    /// <summary>Extensão do arquivo anterior (".srt", ".ass"...) quando o formato muda; decide a conversão de marcas.</summary>
    public string PreviousExtension { get; init; } = string.Empty;

    /// <summary>Grava em UTF-8 com BOM; falso = ANSI. Por padrão segue <see cref="SubtitleDocument.IsUtf8"/>.</summary>
    public bool? Utf8 { get; init; }

    /// <summary>Tamanho do vídeo aberto, para PlayResX/PlayResY do cabeçalho SSA/ASS padrão.</summary>
    public int VideoWidth { get; init; }

    public int VideoHeight { get; init; }

    /// <summary>Versão escrita em "; Written by VisualSubSync ...". Padrão: versão do assembly (a.b.c.d).</summary>
    public string? ApplicationVersion { get; init; }

    /// <summary>
    /// Transformação do texto antes de gravar SRT/SSA/ASS (TTranslator do legado). Ex.: "SRT stripped
    /// of text" usa <see cref="StripText"/>. Nulo = texto original.
    /// </summary>
    public Func<string, string>? Translate { get; init; }

    /// <summary>TTranslatorEmpty: grava só os tempos.</summary>
    public static Func<string, string> StripText { get; } = static _ => string.Empty;

    internal string TranslateText(string text) => Translate is null ? text : Translate(text);

    internal string Version =>
        ApplicationVersion ?? typeof(SubtitleWriteOptions).Assembly.GetName().Version?.ToString(4) ?? "0.0.0.0";

    internal bool ConvertsFrom(params string[] extensions) =>
        extensions.Contains(PreviousExtension, StringComparer.OrdinalIgnoreCase);
}
