namespace VisualSubSync.Core.Undo;

/// <summary>
/// Dados de TUndoableTextTask (TntRichEditCustomUndoUnit.pas): o trecho trocado no texto do editor e
/// as seleções antes e depois. As posições aqui começam em 0 (no Delphi, em 1).
/// </summary>
public sealed class TextEdit
{
    public int ReplaceStart { get; private set; }

    /// <summary>Trecho que existia antes da mudança.</summary>
    public string OriginalText { get; private set; } = string.Empty;

    /// <summary>Trecho que entrou no lugar.</summary>
    public string ReplaceByText { get; private set; } = string.Empty;

    public int OriginalSelStart { get; private set; }

    public int OriginalSelLength { get; private set; }

    public int SelStart { get; private set; }

    public int SelLength { get; private set; }

    public bool IsMerged { get; private set; }

    public bool IsMergeDisabled { get; private set; }

    /// <summary>TTntRichEditCustomUndo.Change + DiffText: o menor trecho que difere entre os dois textos.</summary>
    public static TextEdit Diff(string newText, string oldText, int originalSelStart, int originalSelLength,
        int selStart, int selLength)
    {
        ArgumentNullException.ThrowIfNull(newText);
        ArgumentNullException.ThrowIfNull(oldText);
        int pos = 0;
        while (pos < newText.Length && pos < oldText.Length && newText[pos] == oldText[pos])
        {
            pos++;
        }

        int i = newText.Length - 1, j = oldText.Length - 1;
        while (i >= pos && j >= pos && newText[i] == oldText[j])
        {
            i--;
            j--;
        }

        return new TextEdit
        {
            ReplaceStart = pos,
            OriginalText = oldText.Substring(pos, j - pos + 1),
            ReplaceByText = newText.Substring(pos, i - pos + 1),
            OriginalSelStart = originalSelStart,
            OriginalSelLength = originalSelLength,
            SelStart = selStart,
            SelLength = selLength,
        };
    }

    /// <summary>
    /// Diff com a seleção depois da mudança no fim do trecho novo (o cursor de quem acabou de digitar,
    /// colar ou apagar).
    /// </summary>
    public static TextEdit Diff(string newText, string oldText, int originalSelStart, int originalSelLength)
    {
        var edit = Diff(newText, oldText, originalSelStart, originalSelLength, 0, 0);
        edit.SelStart = edit.ReplaceStart + edit.ReplaceByText.Length;
        return edit;
    }

    /// <summary>DoTask: troca OriginalText por ReplaceByText no texto atual.</summary>
    public string Apply(string current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return Splice(current, OriginalText.Length, ReplaceByText);
    }

    /// <summary>UndoTask: volta ReplaceByText para OriginalText; depois disso nada mais se junta a ela.</summary>
    public string Revert(string current)
    {
        ArgumentNullException.ThrowIfNull(current);
        IsMergeDisabled = true;
        return Splice(current, ReplaceByText.Length, OriginalText);
    }

    /// <summary>Merge: digitação de 1 caractere seguido, Backspace e Delete repetidos viram uma tarefa só.</summary>
    public bool Merge(TextEdit next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (IsMergeDisabled)
        {
            return false;
        }

        if (next.ReplaceByText.Length == 1 && next.OriginalText.Length == 0 && OriginalText.Length == 0 &&
            next.ReplaceStart == ReplaceStart + ReplaceByText.Length && (ReplaceByText.Length == 1 || IsMerged))
        {
            ReplaceByText += next.ReplaceByText;
            SelStart = next.SelStart;
            IsMerged = true;
            return true;
        }

        if (next.OriginalText.Length == 1 && next.ReplaceByText.Length == 0 &&
            next.ReplaceStart == ReplaceStart - 1 && (OriginalText.Length == 1 || IsMerged))
        {
            OriginalText = next.OriginalText + OriginalText;
            ReplaceStart = next.ReplaceStart;
            SelStart = next.SelStart;
            IsMerged = true;
            return true;
        }

        if (next.OriginalText.Length == 1 && next.ReplaceByText.Length == 0 &&
            next.ReplaceStart == ReplaceStart && (OriginalText.Length == 1 || IsMerged))
        {
            OriginalText += next.OriginalText;
            OriginalSelLength += next.OriginalSelLength;
            IsMerged = true;
            return true;
        }

        return false;
    }

    /// <summary>Copy(Text, 1, ReplaceStart - 1) + novo + Copy(Text, ReplaceStart + tamanho, MaxInt).</summary>
    private string Splice(string current, int removeLength, string insert)
    {
        string start = current[..Math.Min(ReplaceStart, current.Length)];
        int endIndex = Math.Min(ReplaceStart + removeLength, current.Length);
        return start + insert + current[endIndex..];
    }
}
