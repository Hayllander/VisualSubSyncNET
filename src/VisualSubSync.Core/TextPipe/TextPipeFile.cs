using System.Text;
using VisualSubSync.Core.Subtitles.Formats;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.TextPipe;

/// <summary>Arquivos do text pipe: ActionLoadTextPipe, ActionSaveTextPipeAs e o .vssscript do projeto.</summary>
public static class TextPipeFile
{
    /// <summary>Filtros do TntOpenDialog1 em ActionLoadTextPipeExecute; o 4º é INDEX_STRIPPED.</summary>
    public const string OpenFilter =
        "Text files (*.txt)|*.TXT|RTF files (*.rtf)|*.RTF|Subtitle files (*.srt)|*.SRT|" +
        "Subtitle files stripped of timings (*.srt)|*.SRT|All files (*.*)|*.*";

    public const int StrippedFilterIndex = 4;

    /// <summary>Filtros do TntSaveDialog1 em ActionSaveTextPipeAsExecute (1 = texto, 2 = RTF).</summary>
    public const string SaveFilter = "Text file (*.txt)|*.TXT|RTF file (*.rtf)|*.RTF";

    /// <summary>
    /// TRichEdit.Lines.LoadFromFile sem PlainText: o RichEdit lê como RTF o que começa por "{\rtf"
    /// e como texto o resto, seja qual for a extensão.
    /// </summary>
    public static bool IsRtf(ReadOnlySpan<byte> bytes) => bytes.StartsWith("{\\rtf"u8);

    /// <summary>Texto simples com a mesma detecção dos outros arquivos (BOM, senão ANSI).</summary>
    public static string ReadPlainText(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var encoding = TextFileReader.Detect(bytes, out int preamble);
        return encoding.GetString(bytes, preamble, bytes.Length - preamble);
    }

    /// <summary>"Subtitle files stripped of timings": o texto de cada legenda, uma depois da outra.</summary>
    public static string ReadStrippedSrt(string path)
    {
        var document = SrtFormat.Parse(TextFileReader.ReadAllText(path).Text);
        return string.Join("\r\n", document.Items.Select(s => s.Text));
    }

    /// <summary>PlainText := True; Lines.SaveToFile: o TntRichEdit grava UTF-16 com BOM.</summary>
    public static byte[] EncodePlainText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)];
    }
}
