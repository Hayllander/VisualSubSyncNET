using System.Globalization;
using System.Text;

namespace VisualSubSync.Core.Subtitles;

/// <summary>
/// Contador de caracteres do editor (TMainForm.UpdateLinesCounter) e as rotinas de tags que ele usa
/// (TagSplit e StripTagsKeepNL de MiscToolsUnit.pas).
/// </summary>
public static class SubtitleTextStats
{
    private const string CrLf = "\r\n";

    /// <summary>TagSplit: alterna texto e tag ("&lt;...&gt;" ou "{\...}"); índices pares são texto.</summary>
    public static List<string> TagSplit(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = new List<string>();
        string s = text;
        while (s.Length > 0)
        {
            int start = -1;
            bool ssaType = false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '{')
                {
                    if (i + 3 < s.Length && s[i + 1] == '\\')
                    {
                        start = i;
                        ssaType = true;
                        break;
                    }
                }
                else if (s[i] == '<')
                {
                    start = i;
                    break;
                }
            }

            if (start < 0)
            {
                words.Add(s);
                break;
            }

            words.Add(s[..start]);
            s = s[start..];
            int end = s.IndexOf(ssaType ? '}' : '>', StringComparison.Ordinal);
            if (end >= 0)
            {
                words.Add(s[..(end + 1)]);
                s = s[(end + 1)..];
            }
            else
            {
                // Tag sem fechamento: o resto volta para o último texto.
                words[^1] += s;
                s = string.Empty;
            }
        }

        return words;
    }

    /// <summary>StripTags: só o texto fora das tags.</summary>
    public static string StripTags(string text)
    {
        var words = TagSplit(text);
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < words.Count; i += 2)
        {
            sb.Append(words[i]);
        }

        return sb.ToString();
    }

    /// <summary>StripTagsKeepNL: remove as tags, mas mantém as quebras de linha que estavam dentro delas.</summary>
    public static string StripTagsKeepNewLines(string text)
    {
        var words = TagSplit(text);
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < words.Count; i++)
        {
            if (i % 2 == 0)
            {
                sb.Append(words[i]);
            }
            else
            {
                int count = CountOf(words[i], CrLf);
                for (int j = 0; j < count; j++)
                {
                    sb.Append(CrLf);
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Texto do MemoLinesCounter: quantidade de caracteres (sem tags) de cada linha, a partir da
    /// primeira linha visível, como no original.
    /// </summary>
    public static string LinesCounter(string text, int firstVisibleLine) => Compute(text, firstVisibleLine).Counter;

    /// <summary>
    /// Texto do StatusBarPanel1: "Line: n, Column: c | Total: t" e, quando há legenda focada,
    /// ", Char/s: cps" (-1 se a duração for zero). Total soma só a partir da primeira linha visível,
    /// como no original.
    /// </summary>
    public static string StatusText(string text, int lineIndex, int column, int firstVisibleLine, int? durationMs)
    {
        var (_, total, linesCount) = Compute(text, firstVisibleLine);
        var sb = new StringBuilder(64);
        sb.Append(CultureInfo.InvariantCulture, $"Line: {lineIndex + 1}, Column: {column + 1} | Total: {total}");
        if (durationMs is int duration)
        {
            int withCrLf = linesCount > 0 ? total + ((linesCount - 1) * 2) : 0;
            int cps = duration > 0 ? (int)Math.Round(withCrLf / (duration / 1000.0), MidpointRounding.ToEven) : -1;
            sb.Append(CultureInfo.InvariantCulture, $", Char/s: {cps}");
        }

        return sb.ToString();
    }

    private static (string Counter, int Total, int LinesCount) Compute(string text, int firstVisibleLine)
    {
        ArgumentNullException.ThrowIfNull(text);
        string[] lines = StripTagsKeepNewLines(text).Split(CrLf);

        // Lines.Count do TStrings ignora a quebra final; EM_GETLINECOUNT conta a linha vazia depois dela.
        int linesCount = text.Length == 0 ? 0 : text.Split(CrLf).Length - (text.EndsWith(CrLf, StringComparison.Ordinal) ? 1 : 0);
        int editLineCount = text.Split(CrLf).Length;

        var sb = new StringBuilder();
        int total = 0;
        for (int i = Math.Max(0, firstVisibleLine); i < linesCount; i++)
        {
            int count = i < lines.Length ? lines[i].Length : 0;
            total += count;
            sb.Append(count.ToString(CultureInfo.InvariantCulture)).Append(CrLf);
        }

        for (int i = linesCount; i < editLineCount; i++)
        {
            sb.Append('0').Append(CrLf);
        }

        return (Text.DelphiText.Trim(sb.ToString()), total, linesCount);
    }

    private static int CountOf(string s, string pattern)
    {
        int count = 0;
        for (int i = s.IndexOf(pattern, StringComparison.Ordinal); i >= 0; i = s.IndexOf(pattern, i + pattern.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
