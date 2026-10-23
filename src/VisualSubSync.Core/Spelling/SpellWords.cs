namespace VisualSubSync.Core.Spelling;

/// <summary>Uma palavra no texto: posição (base 0) e tamanho (TWordInfo, que usa base 1).</summary>
public readonly record struct WordInfo(int Position, int Length);

/// <summary>As funções de palavras de LibHunspellUnit.pas.</summary>
public static class SpellWords
{
    /// <summary>WordDelimiters: espaços, pontuação, aspas e travessões que separam palavras.</summary>
    private const string Delimiters =
        " .,?!:;()[]{}\r\n\t\"/\\|+-*=%&><¡¿" +
        "  " + // espaços não separáveis
        "…" +       // reticências
        "‘" +       // aspas simples (o apóstrofo tipográfico U+2019 não separa)
        "«»“”" +
        "–—";

    public static bool IsWordDelimiter(char c) => Delimiters.Contains(c, StringComparison.Ordinal);

    /// <summary>
    /// GetNextWordPos: pula os separadores a partir de <paramref name="index"/> e devolve a palavra
    /// seguinte; <paramref name="index"/> fica logo depois dela. O tamanho é 0 no fim do texto.
    /// </summary>
    public static WordInfo GetNextWordPos(ref int index, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        while (index < text.Length && IsWordDelimiter(text[index]))
        {
            index++;
        }

        int position = index;
        while (index < text.Length && !IsWordDelimiter(text[index]))
        {
            index++;
        }

        return new WordInfo(position, index - position);
    }

    /// <summary>ContainDigit.</summary>
    public static bool ContainDigit(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Any(char.IsDigit);
    }

    /// <summary>IsUpperCase: todos os caracteres em maiúscula (verdadeiro para texto vazio).</summary>
    public static bool IsUpperCase(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.All(char.IsUpper);
    }

    /// <summary>
    /// GetWordAt: a palavra que contém a posição <paramref name="cursorIndex"/> (base 0, entre
    /// caracteres): volta até um separador e avança até o próximo.
    /// </summary>
    public static WordInfo GetWordAt(int cursorIndex, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        cursorIndex = Math.Clamp(cursorIndex, 0, text.Length);
        int start = cursorIndex;
        while (start > 0 && !IsWordDelimiter(text[start - 1]))
        {
            start--;
        }

        int end = cursorIndex;
        while (end < text.Length && !IsWordDelimiter(text[end]))
        {
            end++;
        }

        return new WordInfo(start, end - start);
    }

    /// <summary>
    /// TagHighlight e NextError: as palavras de um trecho de texto (sem tags) que o corretor recusa.
    /// Palavras com dígito ou todas em maiúscula são aceitas.
    /// </summary>
    public static IEnumerable<WordInfo> Misspelled(string text, Func<string, bool> spell)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(spell);
        int offset = 0;
        while (offset < text.Length)
        {
            var word = GetNextWordPos(ref offset, text);
            string value = text.Substring(word.Position, word.Length);
            if (!(ContainDigit(value) || IsUpperCase(value) || spell(value)))
            {
                yield return word;
            }

            offset++;
        }
    }
}
