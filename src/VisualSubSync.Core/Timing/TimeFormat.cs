using System.Globalization;

namespace VisualSubSync.Core.Timing;

/// <summary>
/// Conversões de tempo equivalentes às de MiscToolsUnit.pas (TimeMsToString,
/// TimeStringToMs, TimeMsToSSAString). Todos os tempos são inteiros em milissegundos.
/// </summary>
public static class TimeFormat
{
    /// <summary>hh:mm:ss{sep}mmm, como TimeMsToString. O separador padrão do legado é '.'.</summary>
    public static string ToVss(int timeMs, char decimalSeparator = '.')
    {
        var (h, m, s, ms) = Split(timeMs);
        return string.Create(CultureInfo.InvariantCulture, $"{h:00}:{m:00}:{s:00}{decimalSeparator}{ms:000}");
    }

    /// <summary>hh:mm:ss,mmm usado nos arquivos SRT.</summary>
    public static string ToSrt(int timeMs) => ToVss(timeMs, ',');

    /// <summary>h:mm:ss.cc usado em SSA/ASS (centésimos, truncados como no legado).</summary>
    public static string ToSsa(int timeMs)
    {
        var (h, m, s, ms) = Split(timeMs);
        return string.Create(CultureInfo.InvariantCulture, $"{h}:{m:00}:{s:00}.{ms / 10:00}");
    }

    /// <summary>
    /// Mesmo contrato de TimeStringToMs: aceita "00:50:06,969" ou "00:50:06.969"
    /// em posições fixas e devolve -1 quando o texto é inválido.
    /// </summary>
    public static int ParseVss(ReadOnlySpan<char> text)
    {
        if (text.Length < 8 || text[2] != ':' || text[5] != ':')
        {
            return -1;
        }

        if (!TryPart(text, 0, 2, out int h) || !TryPart(text, 3, 2, out int m) ||
            !TryPart(text, 6, 2, out int s) || !TryPart(text, 9, 3, out int ms))
        {
            return -1;
        }

        return (h * 3_600_000) + (m * 60_000) + (s * 1000) + ms;
    }

    /// <summary>
    /// TimeStringToMS_SSA: "h:mm:ss.cc" ou "hh:mm:ss.cc" (centésimos); devolve -1 quando inválido.
    /// </summary>
    public static int ParseSsa(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length < 8)
        {
            return -1;
        }

        if (text[1] == ':')
        {
            text = "0" + text;
        }

        var span = text.AsSpan();
        if (span[2] != ':' || span[5] != ':')
        {
            return -1;
        }

        if (!TryPart(span, 0, 2, out int h) || !TryPart(span, 3, 2, out int m) ||
            !TryPart(span, 6, 2, out int s) || !TryPart(span, 9, 2, out int cs))
        {
            return -1;
        }

        return (h * 3_600_000) + (m * 60_000) + (s * 1000) + (10 * cs);
    }

    /// <summary>
    /// TimeMsToCUE: mm:ss:ff com 75 quadros por segundo. As horas são descartadas, como no legado.
    /// </summary>
    public static string ToCue(int timeMs)
    {
        var (_, m, s, ms) = Split(timeMs);
        int ff = (int)Math.Round(ms / (1000.0 / 75), MidpointRounding.ToEven);
        return string.Create(CultureInfo.InvariantCulture, $"{m:00}:{s:00}:{ff:00}");
    }

    private static bool TryPart(ReadOnlySpan<char> text, int start, int length, out int value)
    {
        value = -1;
        if (start >= text.Length)
        {
            return false;
        }

        var part = text.Slice(start, Math.Min(length, text.Length - start)).Trim();
        return int.TryParse(part, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) && value >= 0;
    }

    private static (int H, int M, int S, int Ms) Split(int timeMs)
    {
        int totalSeconds = timeMs / 1000;
        int h = totalSeconds / 3600;
        int m = totalSeconds % 3600 / 60;
        int s = totalSeconds % 60;
        int ms = timeMs - (totalSeconds * 1000);
        return (h, m, s, ms);
    }
}
