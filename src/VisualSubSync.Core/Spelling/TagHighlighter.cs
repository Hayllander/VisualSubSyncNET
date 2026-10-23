using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Spelling;

/// <summary>O que um trecho do editor recebe em <see cref="TagHighlighter"/>.</summary>
public enum HighlightKind
{
    /// <summary>Tag (clGrayText).</summary>
    Tag,

    /// <summary>A tag de karaokê selecionada na onda (clRed).</summary>
    SelectedKaraokeTag,

    /// <summary>Palavra recusada pelo dicionário (sublinhado ondulado).</summary>
    Misspelled,
}

/// <summary>Um trecho do texto do editor, em índices do texto (com CRLF).</summary>
public readonly record struct HighlightSpan(int Start, int Length, HighlightKind Kind);

/// <summary>
/// TagHighlight (main.pas) sem o RichEdit: as tags em cinza, a tag de índice <c>tagIndex</c> em
/// vermelho e, nos trechos de texto, as palavras recusadas quando a verificação ao vivo está ligada.
/// </summary>
public static class TagHighlighter
{
    /// <param name="text">O texto do editor.</param>
    /// <param name="tagIndex">WAVDisplayer.KaraokeSelectedIndex, ou -1.</param>
    /// <param name="spell">FSpellChecker.Spell, ou nulo quando a verificação ao vivo não se aplica.</param>
    public static IReadOnlyList<HighlightSpan> Compute(string text, int tagIndex, Func<string, bool>? spell)
    {
        ArgumentNullException.ThrowIfNull(text);
        var spans = new List<HighlightSpan>();
        var wordArray = SubtitleTextStats.TagSplit(text);
        int j = 0;
        for (int i = 0; i < wordArray.Count; i++)
        {
            string part = wordArray[i];
            if (i == (tagIndex * 2) + 1)
            {
                spans.Add(new HighlightSpan(j, part.Length, HighlightKind.SelectedKaraokeTag));
            }
            else if (i % 2 != 0)
            {
                spans.Add(new HighlightSpan(j, part.Length, HighlightKind.Tag));
            }
            else if (spell is not null)
            {
                foreach (var word in SpellWords.Misspelled(part, spell))
                {
                    spans.Add(new HighlightSpan(j + word.Position, word.Length, HighlightKind.Misspelled));
                }
            }

            j += part.Length;
        }

        return spans;
    }
}
