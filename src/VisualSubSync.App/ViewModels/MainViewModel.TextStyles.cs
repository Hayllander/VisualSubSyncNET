using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.App.ViewModels;

/// <summary>
/// Estilos rápidos (TMainForm.TagText, ActionTextItalic/Bold/Underline/Color/Size) e ActionStripTags:
/// com o editor focado a tag entra no texto dele; senão envolve cada legenda selecionada.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>MemoSubtitleText.Focused: a janela informa se o editor de texto tem o foco.</summary>
    public Func<bool>? IsEditorFocused { get; set; }

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void TextItalic() => TagText(TextTagType.Italic);

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void TextBold() => TagText(TextTagType.Bold);

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void TextUnderline() => TagText(TextTagType.Underline);

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void TextColor() => TagText(TextTagType.Color);

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void TextSize() => TagText(TextTagType.Size);

    /// <summary>ActionStripTagsExecute: tira as tags das legendas selecionadas, numa tarefa de desfazer.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void StripTags() => ChangeSelectedTexts(SubtitleTextStats.StripTags);

    /// <summary>TagText(TagType) + TagText(StartTag, StopTag).</summary>
    private void TagText(TextTagType tagType)
    {
        if (Project is null || TextStyleTags.For(tagType, Project.SubtitlesFile) is not var (startTag, stopTag))
        {
            return;
        }

        if (IsEditorFocused?.Invoke() == true)
        {
            TagTextMemo(startTag, stopTag);
        }
        else
        {
            ChangeSelectedTexts(text => startTag + text + stopTag);
        }
    }

    /// <summary>TagTextMemo: o texto muda como uma edição do editor (SaveSelectionInfo) e a seleção é refeita.</summary>
    private void TagTextMemo(string startTag, string stopTag)
    {
        if (SelectedSubtitle is not { } item)
        {
            return;
        }

        var (start, length) = _editorSelection;
        var result = TextStyleTags.TagMemo(item.Text, start, length, startTag, stopTag);
        item.Text = result.Text;
        EditorSelectionRequested?.Invoke(this, (result.SelStart, result.SelLength));
    }

    /// <summary>TagTextLines / ActionStripTagsExecute: uma TUndoableMultiChangeTask com as legendas que mudaram.</summary>
    private void ChangeSelectedTexts(Func<string, string> transform)
    {
        var changes = new List<TextChange>();
        foreach (int index in SelectedIndexes())
        {
            string oldText = Subtitles[index].Text;
            string newText = transform(oldText);
            if (newText != oldText)
            {
                changes.Add(new TextChange(index, oldText, newText));
            }
        }

        if (changes.Count > 0)
        {
            DoAndPush(new MultiChangeTask(this, changes));
        }
    }
}
