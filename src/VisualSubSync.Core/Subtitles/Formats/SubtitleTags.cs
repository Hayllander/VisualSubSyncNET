using System.Text;

namespace VisualSubSync.Core.Subtitles.Formats;

/// <summary>Conversões de texto usadas ao trocar de formato (MiscToolsUnit.pas).</summary>
public static class SubtitleTags
{
    private static readonly (string Ssa, string Srt)[] Pairs =
    [
        (@"{\i1}", "<i>"), (@"{\i0}", "</i>"),
        (@"{\b1}", "<b>"), (@"{\b0}", "</b>"),
        (@"{\u1}", "<u>"), (@"{\u0}", "</u>"),
    ];

    /// <summary>ConvertSSAToSRT: só itálico, negrito e sublinhado, como no legado.</summary>
    public static string SsaToSrt(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sb = new StringBuilder(text);
        foreach (var (ssa, srt) in Pairs)
        {
            sb.Replace(ssa, srt);
        }

        return sb.ToString();
    }

    /// <summary>ConvertSRTToSSA.</summary>
    public static string SrtToSsa(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sb = new StringBuilder(text);
        foreach (var (ssa, srt) in Pairs)
        {
            sb.Replace(srt, ssa);
        }

        return sb.ToString();
    }

    /// <summary>Junta as linhas com um espaço, na mesma ordem de substituições dos gravadores CUE/TXT/CSV.</summary>
    public static string JoinLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Replace(" \r\n ", " ", StringComparison.Ordinal)
                   .Replace(" \r\n", " ", StringComparison.Ordinal)
                   .Replace("\r\n ", " ", StringComparison.Ordinal)
                   .Replace("\r\n", " ", StringComparison.Ordinal);
    }
}
