using VisualSubSync.Core.TextPipe;

namespace VisualSubSync.App.Services;

/// <summary>O MemoTextPipe como o TMainForm o usa, além do que as tarefas de desfazer precisam.</summary>
public interface ITextPipeView : ITextPipe
{
    /// <summary>MemoTextPipe.Focused.</summary>
    bool IsFocused { get; }

    /// <summary>MemoTextPipe.Clear.</summary>
    void Clear();

    /// <summary>MemoTextPipe.Text := ...: texto simples, sem as cores.</summary>
    void SetText(string text);

    /// <summary>SelText := ...: troca a seleção pelo texto.</summary>
    void ReplaceSelection(string text);

    void LoadRtf(byte[] rtf);

    byte[] SaveRtf();

    /// <summary>SelStart := position; EM_SCROLLCARET, com a linha no alto (vai antes ao fim).</summary>
    void ScrollToPosition(int position);
}
