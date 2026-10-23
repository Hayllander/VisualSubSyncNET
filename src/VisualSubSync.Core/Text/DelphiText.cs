using System.Globalization;
using System.Text;

namespace VisualSubSync.Core.Text;

/// <summary>
/// Funções de texto com a semântica exata do Delphi 7/TntUnicode usadas pelos parsers legados.
/// Trim do Delphi remove qualquer caractere &lt;= ' ' (não só espaços), e TTntStringList
/// quebra linhas em CR, LF ou CRLF e descarta a última linha vazia.
/// </summary>
public static class DelphiText
{
    public static string Trim(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        int start = 0, end = s.Length - 1;
        while (start <= end && s[start] <= ' ')
        {
            start++;
        }

        while (end >= start && s[end] <= ' ')
        {
            end--;
        }

        return s.Substring(start, end - start + 1);
    }

    public static string TrimLeft(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        int start = 0;
        while (start < s.Length && s[start] <= ' ')
        {
            start++;
        }

        return s[start..];
    }

    public static string TrimRight(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        int end = s.Length - 1;
        while (end >= 0 && s[end] <= ' ')
        {
            end--;
        }

        return s[..(end + 1)];
    }

    /// <summary>Equivalente a TTntStrings.SetTextStr: cada CR, LF ou CRLF encerra uma linha.</summary>
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

    /// <summary>StartsText: comparação sem diferenciar maiúsculas.</summary>
    public static bool StartsText(string prefix, string text) =>
        text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    public static bool EndsText(string suffix, string text) =>
        text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>StrToIntDef: decimal com sinal ou hexadecimal com '$', em 32 bits.</summary>
    public static int StrToIntDef(string s, int defaultValue)
    {
        ArgumentNullException.ThrowIfNull(s);
        var span = s.AsSpan().TrimStart(' ');
        bool negative = false;
        if (span.Length > 0 && (span[0] == '-' || span[0] == '+'))
        {
            negative = span[0] == '-';
            span = span[1..];
        }

        long value;
        if (span.Length > 0 && span[0] == '$')
        {
            if (!long.TryParse(span[1..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value))
            {
                return defaultValue;
            }
        }
        else if (span.Length == 0 || !char.IsAsciiDigit(span[0]) ||
                 !long.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            return defaultValue;
        }

        value = negative ? -value : value;
        return value is < int.MinValue or > int.MaxValue ? defaultValue : (int)value;
    }

    /// <summary>StrToFloatDef com separador decimal '.'.</summary>
    public static double StrToFloatDef(string s, double defaultValue) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : defaultValue;

    /// <summary>FloatToStr com separador '.': até 15 dígitos significativos, sem zeros à direita.</summary>
    public static string FloatToStr(double value) => value.ToString("G15", CultureInfo.InvariantCulture);

    /// <summary>
    /// TStringList.CommaText (leitura): itens separados por vírgula ou por caracteres até o espaço;
    /// entre aspas, as aspas dobradas viram uma.
    /// </summary>
    public static List<string> SplitCommaText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var list = new List<string>();
        int i = 0;
        while (i < text.Length && text[i] <= ' ')
        {
            i++;
        }

        while (i < text.Length)
        {
            var sb = new StringBuilder();
            if (text[i] == '"')
            {
                i++;
                while (i < text.Length)
                {
                    if (text[i] == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    sb.Append(text[i++]);
                }

                // O resto até o separador entra junto (AnsiExtractQuotedStr seguido do texto).
                while (i < text.Length && text[i] > ' ' && text[i] != ',')
                {
                    sb.Append(text[i++]);
                }
            }
            else
            {
                while (i < text.Length && text[i] > ' ' && text[i] != ',')
                {
                    sb.Append(text[i++]);
                }
            }

            list.Add(sb.ToString());
            while (i < text.Length && text[i] <= ' ')
            {
                i++;
            }

            if (i < text.Length && text[i] == ',')
            {
                i++;
                while (i < text.Length && text[i] <= ' ')
                {
                    i++;
                }

                if (i >= text.Length)
                {
                    list.Add(string.Empty);
                }
            }
        }

        return list;
    }

    /// <summary>TStringList.CommaText (escrita): aspas em volta dos itens vazios ou com espaço, aspas ou vírgula.</summary>
    public static string ToCommaText(IEnumerable<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var list = items.ToList();
        if (list.Count == 1 && list[0].Length == 0)
        {
            return "\"\"";
        }

        return string.Join(',', list.Select(item =>
            item.Length == 0 || item.Any(c => c <= ' ' || c == '"' || c == ',')
                ? "\"" + item.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
                : item));
    }
}
