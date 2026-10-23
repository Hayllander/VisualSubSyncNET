using VisualSubSync.Core.Subtitles.Formats;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Subtitles;

/// <summary>
/// As legendas de ActionInsertTextFileExecute: um .srt entra com os próprios tempos; outro arquivo
/// vira uma legenda de 1 s por linha, a cada 2 s, começando 2 s depois do fim da última legenda (ou em
/// 99:00:00 sem legendas).
/// </summary>
public static class InsertedFile
{
    public const string Filter = "Text files|*.TXT|Subtitle files (*.srt)|*.SRT|All files (*.*)|*.*";

    /// <summary>99 * 60 * 60 * 1000: o início quando ainda não há legendas.</summary>
    public const int EmptyListStartMs = 99 * 60 * 60 * 1000;

    public static bool IsSrt(string path) =>
        string.Equals(Path.GetExtension(path), ".srt", StringComparison.OrdinalIgnoreCase);

    /// <summary>TSRTParser.Load + UpdateSubTimeFromText.</summary>
    public static List<Subtitle> ReadSrt(string path)
    {
        var items = SrtFormat.Parse(TextFileReader.ReadAllText(path).Text).Items.ToList();
        foreach (var sub in items)
        {
            Karaoke.UpdateSubTimesFromText(sub);
        }

        return items;
    }

    /// <summary>Uma legenda por linha (TStrings.LoadFromFile), a partir de <paramref name="lastStopMs"/> + 2 s.</summary>
    public static List<Subtitle> FromLines(string text, int? lastStopMs)
    {
        int start = lastStopMs is { } stop ? stop + 2000 : EmptyListStartMs;
        var result = new List<Subtitle>();
        foreach (string line in SplitLines(text))
        {
            result.Add(new Subtitle { StartMs = start, StopMs = start + 1000, Text = line });
            start += 2000;
        }

        return result;
    }

    /// <summary>TStrings.SetTextStr: CR, LF ou CRLF separam as linhas; a quebra final não abre outra.</summary>
    public static List<string> SplitLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            int start = i;
            while (i < text.Length && text[i] != '\r' && text[i] != '\n')
            {
                i++;
            }

            lines.Add(text[start..i]);
            if (i < text.Length && text[i] == '\r')
            {
                i++;
            }

            if (i < text.Length && text[i] == '\n')
            {
                i++;
            }
        }

        return lines;
    }
}
