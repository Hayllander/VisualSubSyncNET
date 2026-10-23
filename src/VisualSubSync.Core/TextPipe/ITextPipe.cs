namespace VisualSubSync.Core.TextPipe;

/// <summary>
/// O MemoTextPipe (TTntRichEdit) visto pelas tarefas: posições em caracteres do texto com quebras
/// de linha "\r\n" (como SelStart/SelLength do TntRichEdit) e cores no formato TColor ($00BBGGRR).
/// </summary>
public interface ITextPipe
{
    string Text { get; }

    int SelStart { get; }

    int SelLength { get; }

    void Select(int start, int length);

    /// <summary>As cores do trecho, em pedaços contíguos (SelAttributes.Color de cada parte).</summary>
    IReadOnlyList<TextPipeColorRun> GetColors(int start, int length);

    void SetColor(int start, int length, int color);

    void Delete(int start, int length);

    void Insert(int start, string text);
}

/// <summary>Um pedaço do texto com uma só cor.</summary>
public readonly record struct TextPipeColorRun(int Start, int Length, int Color);

/// <summary>pmiAutoColorizeText / pmiAutoDeleteText / pmiAutoDeleteAllTextBefore (GetTextPipeAutoOption).</summary>
public enum TextPipeAutoOption
{
    Colorize = 0,
    Delete = 1,
    DeleteAllBefore = 2,
}
