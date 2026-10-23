using System.Text;
using System.Text.RegularExpressions;

namespace VisualSubSync.Core.Text;

/// <summary>
/// Busca e troca de texto da janela Find: WideStringFind, WideStringCount e Tnt_WideStringReplace
/// (MiscToolsUnit.pas) e o TRegExpr usado por SearchSubtitles/ReplaceAllTextSubs. Posições começam
/// em 1, como no Delphi; 0 quer dizer "não achou".
/// </summary>
public static class TextSearch
{
    /// <summary>WideStringFind: a primeira ocorrência a partir de <paramref name="offset"/>.</summary>
    public static int Find(int offset, string text, string pattern, bool ignoreCase, bool wholeWord)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(pattern);
        if (text.Length == 0)
        {
            return 0;
        }

        if (pattern.Length == 0)
        {
            return 1;
        }

        var (search, patt) = Prepare(text, pattern, ignoreCase);
        int off = Math.Max(offset, 1);
        while (off <= search.Length)
        {
            off = PosEx(patt, search, off);
            if (off == 0)
            {
                break;
            }

            if (wholeWord && !IsWholeWord(search, off, pattern.Length))
            {
                off += pattern.Length;
                continue;
            }

            return off;
        }

        return 0;
    }

    /// <summary>WideStringCount: como no original, uma ocorrência pode começar dentro da anterior.</summary>
    public static int Count(string text, string pattern, bool ignoreCase, bool wholeWord)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(pattern);
        if (text.Length == 0)
        {
            return 0;
        }

        if (pattern.Length == 0)
        {
            return 1;
        }

        var (search, patt) = Prepare(text, pattern, ignoreCase);
        int count = 0, off = 1;
        while (off <= search.Length)
        {
            off = PosEx(patt, search, off);
            if (off == 0)
            {
                break;
            }

            if (wholeWord && !IsWholeWord(search, off, pattern.Length))
            {
                off += pattern.Length;
                continue;
            }

            count++;
            off++;
        }

        return count;
    }

    /// <summary>Tnt_WideStringReplace com rfReplaceAll: troca todas as ocorrências, da esquerda para a direita.</summary>
    public static string ReplaceAll(string text, string pattern, string replacement, bool ignoreCase, bool wholeWord)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(replacement);
        if (text.Length == 0 || pattern.Length == 0)
        {
            return text;
        }

        var result = new StringBuilder();
        int last = 1;
        int pos = Find(1, text, pattern, ignoreCase, wholeWord);
        while (pos > 0)
        {
            result.Append(text, last - 1, pos - last).Append(replacement);
            last = pos + pattern.Length;
            pos = last <= text.Length ? Find(last, text, pattern, ignoreCase, wholeWord) : 0;
        }

        return result.Append(text, last - 1, text.Length - last + 1).ToString();
    }

    /// <summary>
    /// TRegExpr com os modificadores padrão (s ligado: o ponto também pega quebras de linha) e i quando
    /// não diferencia maiúsculas.
    /// </summary>
    public static Regex CreateRegex(string pattern, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var options = RegexOptions.Singleline | RegexOptions.CultureInvariant;
        if (ignoreCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        return new Regex(pattern, options, TimeSpan.FromSeconds(2));
    }

    /// <summary>ExecPos(Offset): a primeira ocorrência a partir de <paramref name="offset"/>; posição 0 quando não há.</summary>
    public static (int Position, int Length) RegexFind(Regex regex, string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(regex);
        ArgumentNullException.ThrowIfNull(text);
        if (offset < 1 || offset > text.Length + 1)
        {
            return (0, 0);
        }

        var match = regex.Match(text, offset - 1);
        return match.Success ? (match.Index + 1, match.Length) : (0, 0);
    }

    /// <summary>TRegExpr.Replace(..., UseSubstitution = True) com ReplaceCount.</summary>
    public static string RegexReplace(Regex regex, string text, string replacement, out int count)
    {
        ArgumentNullException.ThrowIfNull(regex);
        int n = 0;
        string result = regex.Replace(text, m =>
        {
            n++;
            return m.Result(replacement);
        });
        count = n;
        return result;
    }

    private static (string Search, string Pattern) Prepare(string text, string pattern, bool ignoreCase) =>
        ignoreCase ? (text.ToUpperInvariant(), pattern.ToUpperInvariant()) : (text, pattern);

    private static int PosEx(string pattern, string text, int offset)
    {
        if (offset > text.Length)
        {
            return 0;
        }

        int i = text.IndexOf(pattern, offset - 1, StringComparison.Ordinal);
        return i < 0 ? 0 : i + 1;
    }

    private static bool IsWholeWord(string text, int off, int length)
    {
        char prev = off == 1 ? '\0' : text[off - 2];
        char next = off + length <= text.Length ? text[off + length - 1] : '\0';
        return IsWordSeparator(prev) && IsWordSeparator(next);
    }

    /// <summary>IsWideCharSpace ou IsWideCharPunct (C1_SPACE/C1_PUNCT do GetStringTypeW).</summary>
    private static bool IsWordSeparator(char c) =>
        c == '\0' || char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c);
}
