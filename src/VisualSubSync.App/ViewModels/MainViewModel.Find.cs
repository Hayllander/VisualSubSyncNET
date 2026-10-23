using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.App.Services;
using VisualSubSync.Core.Text;

namespace VisualSubSync.App.ViewModels;

/// <summary>
/// Busca e troca (ActionFind, ActionFindNext, SearchSubtitles, ReplaceText, ReplaceAllText,
/// ShowMatchingSubOnly) e ActionGoto. As opções são as da janela Find (TFindForm), que fica ligada a
/// estas propriedades.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>SearchNodeIndex: a legenda do último resultado, ou -1.</summary>
    private int _searchNodeIndex = -1;

    /// <summary>SearchPos: onde a próxima busca começa no texto dessa legenda (1 = início).</summary>
    private int _searchPos = 1;

    private Predicate<SubtitleItemViewModel>? _subtitleFilter;

    /// <summary>SearchItemPos: no text pipe, onde está o último resultado (1 = início; -1 = nenhum).</summary>
    private int _searchItemPos = -1;

    /// <summary>SearchItem = @MemoTextPipe: a janela Find foi aberta com o text pipe focado ("Find text").</summary>
    [ObservableProperty]
    private bool _isFindInTextPipe;

    /// <summary>cbFind.</summary>
    [ObservableProperty]
    private string _findText = string.Empty;

    /// <summary>cbReplaceWith.</summary>
    [ObservableProperty]
    private string _replaceText = string.Empty;

    /// <summary>chkMatchCase.</summary>
    [ObservableProperty]
    private bool _findMatchCase;

    /// <summary>chkFromCursor.</summary>
    [ObservableProperty]
    private bool _findFromCursor;

    /// <summary>chkRegExp.</summary>
    [ObservableProperty]
    private bool _findUseRegExp;

    /// <summary>chkWholeWord (só vale sem expressão regular).</summary>
    [ObservableProperty]
    private bool _findWholeWord;

    /// <summary>vtvSubsList.IsVisible[Node]: a lista mostra só as legendas aceitas por este filtro.</summary>
    public event EventHandler? SubtitleFilterChanged;

    /// <summary>LastFindSucceded.</summary>
    public bool LastFindSucceeded => IsFindInTextPipe ? _searchItemPos > 0 : _searchNodeIndex != -1;

    /// <summary>Legendas visíveis na lista ("Show matching sub only").</summary>
    public bool IsSubtitleVisible(SubtitleItemViewModel item) => _subtitleFilter?.Invoke(item) ?? true;

    private bool WholeWord => !FindUseRegExp && FindWholeWord;

    /// <summary>ActionFindExecute: abre a janela Find; com o text pipe focado, ela busca no texto dele.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void Find()
    {
        if (!_dialogs.IsFindShowing)
        {
            IsFindInTextPipe = TextPipe?.IsFocused == true;
            ResetFind();
            _dialogs.ShowFind(this);
        }
    }

    /// <summary>ActionFindNextExecute.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void FindNext()
    {
        if (FindText.Length == 0)
        {
            return;
        }

        bool found = IsFindInTextPipe && TextPipe is { } pipe ? SearchMemo(pipe, FindText) : SearchSubtitles(FindText);
        if (!found)
        {
            ShowStatusBarMessage("No more items.");
        }
    }

    /// <summary>ResetFind.</summary>
    public void ResetFind()
    {
        _searchNodeIndex = -1;
        _searchPos = 1;
        _searchItemPos = -1;
    }

    /// <summary>ShowMatchingSubOnly: com <paramref name="displayAll"/> falso, esconde as que não casam.</summary>
    public void ShowMatchingSubOnly(bool displayAll)
    {
        string findText = FindText;
        Regex? regex = null;
        if (findText.Length > 0 && !displayAll && FindUseRegExp && !TryCreateRegex(findText, out regex))
        {
            return;
        }

        bool Match(SubtitleItemViewModel item) =>
            findText.Length == 0 || displayAll ||
            (regex is not null
                ? regex.IsMatch(item.Text)
                : TextSearch.Find(1, item.Text, findText, !FindMatchCase, WholeWord) > 0);

        int count = Subtitles.Count(Match);
        _subtitleFilter = displayAll || findText.Length == 0 ? null : Match;
        SubtitleFilterChanged?.Invoke(this, EventArgs.Empty);
        if (!displayAll)
        {
            ShowStatusBarMessage($"{count} subtitle(s) match.");
        }
    }

    /// <summary>
    /// SearchSubtitles: continua da legenda e posição do último resultado (ou da focada, com
    /// "From cursor"); achando, foca a legenda e seleciona o trecho no editor.
    /// </summary>
    private bool SearchSubtitles(string findText)
    {
        Regex? regex = null;
        if (FindUseRegExp && !TryCreateRegex(findText, out regex))
        {
            return false;
        }

        int index = _searchNodeIndex >= 0 && _searchNodeIndex < Subtitles.Count ? _searchNodeIndex : -1;
        if (FindFromCursor)
        {
            int focused = SelectedSubtitle is { } item ? Subtitles.IndexOf(item) : -1;
            if (index != focused)
            {
                _searchPos = 1;
            }

            index = focused;
        }

        if (index == -1)
        {
            index = 0;
        }

        for (; index < Subtitles.Count; index++)
        {
            _searchNodeIndex = index;
            string text = Subtitles[index].Text;
            int matchLen = findText.Length;
            if (regex is not null)
            {
                (_searchPos, matchLen) = TextSearch.RegexFind(regex, text, _searchPos);
            }
            else
            {
                _searchPos = TextSearch.Find(_searchPos, text, findText, !FindMatchCase, WholeWord);
            }

            if (_searchPos > 0)
            {
                SelectedSubtitle = Subtitles[index];
                EditorSelectionRequested?.Invoke(this, (_searchPos - 1, matchLen));
                _searchPos += matchLen;
                return true;
            }

            _searchPos = 1;
        }

        _searchNodeIndex = -1;
        return false;
    }

    /// <summary>ReplaceText / ReplaceTextSubs: troca o trecho selecionado no editor (com desfazer).</summary>
    public void ReplaceSelection()
    {
        if (IsFindInTextPipe && TextPipe is { } pipe)
        {
            if (LastFindSucceeded)
            {
                ReplaceTextMemo(pipe);
            }

            return;
        }

        if (!LastFindSucceeded || SelectedSubtitle is not { } item)
        {
            return;
        }

        var (start, length) = _editorSelection;
        start = Math.Min(start, item.Text.Length);
        length = Math.Min(length, item.Text.Length - start);
        string selected = item.Text.Substring(start, length);
        string newText = ReplaceText;
        if (FindUseRegExp)
        {
            if (!TryCreateRegex(FindText, out var regex))
            {
                return;
            }

            newText = TextSearch.RegexReplace(regex, selected, ReplaceText, out _);
        }

        item.Text = item.Text[..start] + newText + item.Text[(start + length)..];
        EditorSelectionRequested?.Invoke(this, (start, newText.Length));
    }

    /// <summary>ReplaceAllText / ReplaceAllTextSubs: troca em todas (ou da focada até o fim), numa tarefa de desfazer.</summary>
    public void ReplaceAll()
    {
        string findText = FindText;
        if (findText.Length == 0)
        {
            return;
        }

        Regex? regex = null;
        if (FindUseRegExp && !TryCreateRegex(findText, out regex))
        {
            return;
        }

        if (IsFindInTextPipe && TextPipe is { } pipe)
        {
            ShowStatusBarMessage($"{ReplaceAllTextMemo(pipe, findText, regex)} item(s) replaced.");
            return;
        }

        int first = FindFromCursor ? (SelectedSubtitle is { } focused ? Subtitles.IndexOf(focused) : Subtitles.Count) : 0;
        var changes = new List<TextChange>();
        int total = 0;
        for (int i = Math.Max(first, 0); i < Subtitles.Count; i++)
        {
            string oldText = Subtitles[i].Text;
            string newText;
            if (regex is not null)
            {
                newText = TextSearch.RegexReplace(regex, oldText, ReplaceText, out int count);
                total += count;
            }
            else
            {
                total += TextSearch.Count(oldText, findText, !FindMatchCase, WholeWord);
                newText = TextSearch.ReplaceAll(oldText, findText, ReplaceText, !FindMatchCase, WholeWord);
            }

            changes.Add(new TextChange(i, oldText, newText));
        }

        if (changes.Count > 0)
        {
            DoAndPush(new MultiChangeTask(this, changes));
            // ReplaceAllTextSubs: com a tarefa empilhada o projeto fica alterado, mesmo sem trocas.
            IsDirty = true;
        }

        ShowStatusBarMessage($"{total} item(s) replaced.");
    }

    /// <summary>ProcessMultiChangeSub (textos e marcadores de karaokê): troca sem gravar outra tarefa.</summary>
    internal void ApplyTextChanges(IReadOnlyList<TextChange> changes, bool isUndo)
    {
        // ProcessMultiChangeSub: aplica, reordena e guarda os novos índices.
        int selected = Waveform?.SelectedRangeIndex ?? -1;
        var items = changes.Select(c => Subtitles[c.Index]).ToList();
        var selectedItem = selected >= 0 && selected < Subtitles.Count ? Subtitles[selected] : null;
        bool timesChanged = false;
        _undoDisabled = true;
        try
        {
            for (int i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                var item = items[i];
                if ((isUndo ? change.OldStart : change.NewStart) is int start)
                {
                    item.Model.StartMs = start;
                    timesChanged = true;
                }

                if ((isUndo ? change.OldStop : change.NewStop) is int stop)
                {
                    item.Model.StopMs = stop;
                    timesChanged = true;
                }

                if ((isUndo ? change.OldText : change.NewText) is { } text)
                {
                    item.Text = text;
                }

                if ((isUndo ? change.OldSubTimes : change.NewSubTimes) is { } subTimes)
                {
                    item.Model.SubTimes = [.. subTimes];
                }
            }

            if (timesChanged)
            {
                SortSubtitles();
                IsDirty = true;
                for (int i = 0; i < changes.Count; i++)
                {
                    items[i].RefreshTimes();
                    changes[i].Index = Subtitles.IndexOf(items[i]);
                }
            }

            RefreshRanges();
        }
        finally
        {
            _undoDisabled = false;
        }

        if (timesChanged)
        {
            if (selectedItem is not null && items.Contains(selectedItem))
            {
                Waveform?.SelectRange(selectedItem.Model.StartMs, selectedItem.Model.StopMs);
            }

            UpdateDurationWithSubOnly();
            UpdateEditorStatus(_caret.Line, _caret.Column, _caret.FirstVisible);
        }
    }

    /// <summary>
    /// ActionGotoExecute: pela linha (limitada à lista) ou pelo tempo (a legenda que contém o tempo ou
    /// a primeira depois dele, senão a última); sem legendas, mostra 20 s em volta do tempo.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void Goto()
    {
        if (_dialogs.ShowGoto() is not { } result)
        {
            return;
        }

        int index = -1;
        if (result.Line is int line)
        {
            if (Subtitles.Count > 0)
            {
                index = Math.Clamp(line - 1, 0, Subtitles.Count - 1);
            }
        }
        else if (result.TimeMs is int ms)
        {
            if (Subtitles.Count > 0)
            {
                index = _document.Items.FindIndex(s => (s.StartMs <= ms && s.StopMs >= ms) || s.StartMs >= ms);
                if (index == -1)
                {
                    index = Subtitles.Count - 1;
                }
            }
            else
            {
                Waveform?.ZoomRange(ms - 10_000, ms + 10_000);
            }

            CursorMs = Math.Clamp(ms, 0, LengthMs);
        }

        SelectSubtitleAt(index);
    }

    /// <summary>
    /// SearchMemo: do início na primeira busca (sem "From cursor"), senão logo depois da seleção;
    /// achando, seleciona o trecho no text pipe.
    /// </summary>
    private bool SearchMemo(ITextPipeView pipe, string findText)
    {
        Regex? regex = null;
        if (FindUseRegExp && !TryCreateRegex(findText, out regex))
        {
            return false;
        }

        _searchItemPos = MemoSearchStart(pipe);
        string text = pipe.Text;
        int matchLen = findText.Length;
        if (regex is not null)
        {
            (_searchItemPos, matchLen) = TextSearch.RegexFind(regex, text, _searchItemPos);
        }
        else
        {
            _searchItemPos = TextSearch.Find(_searchItemPos, text, findText, !FindMatchCase, WholeWord);
        }

        if (_searchItemPos > 0)
        {
            pipe.Select(_searchItemPos - 1, matchLen);
            return true;
        }

        _searchItemPos = -1;
        return false;
    }

    private int MemoSearchStart(ITextPipeView pipe) =>
        _searchItemPos <= 0 && !FindFromCursor ? 1 : pipe.SelStart + 1 + pipe.SelLength;

    /// <summary>ReplaceTextMemo: troca a seleção (com a expressão aplicada a ela) e a seleciona.</summary>
    private void ReplaceTextMemo(ITextPipeView pipe)
    {
        int selStart = pipe.SelStart;
        string newText = ReplaceText;
        if (FindUseRegExp)
        {
            if (!TryCreateRegex(FindText, out var regex))
            {
                return;
            }

            newText = TextSearch.RegexReplace(regex, SelectedPipeText(pipe), ReplaceText, out _);
        }

        pipe.ReplaceSelection(newText);
        pipe.Select(selStart, newText.Length);
    }

    /// <summary>
    /// ReplaceAllTextMemo: troca do início (ou depois da seleção) até o fim; como o original, o texto
    /// é regravado inteiro e perde as cores (aqui, só quando algo foi trocado).
    /// </summary>
    private int ReplaceAllTextMemo(ITextPipeView pipe, string findText, Regex? regex)
    {
        _searchItemPos = MemoSearchStart(pipe);
        string text = pipe.Text;
        int start = Math.Clamp(_searchItemPos - 1, 0, text.Length);
        string rest = text[start..];
        int count;
        if (regex is not null)
        {
            rest = TextSearch.RegexReplace(regex, rest, ReplaceText, out count);
        }
        else
        {
            count = TextSearch.Count(rest, findText, !FindMatchCase, WholeWord);
            rest = TextSearch.ReplaceAll(rest, findText, ReplaceText, !FindMatchCase, WholeWord);
        }

        if (count > 0)
        {
            pipe.SetText(text[..start] + rest);
            IsDirty = true;
        }

        return count;
    }

    private bool TryCreateRegex(string pattern, out Regex regex)
    {
        try
        {
            regex = TextSearch.CreateRegex(pattern, !FindMatchCase);
            return true;
        }
        catch (ArgumentException ex)
        {
            _dialogs.ShowError(ex.Message);
            regex = null!;
            return false;
        }
    }
}

/// <summary>
/// TChangeSubData: o que mudou numa legenda (tempos, texto, marcadores de karaokê); o que fica null
/// não mudou. O índice é atualizado depois da reordenação.
/// </summary>
internal sealed class TextChange
{
    public TextChange(int index, string oldText, string newText, int[]? oldSubTimes = null, int[]? newSubTimes = null)
    {
        Index = index;
        OldText = oldText;
        NewText = newText;
        OldSubTimes = oldSubTimes;
        NewSubTimes = newSubTimes;
    }

    public TextChange(int index) => Index = index;

    public int Index { get; set; }

    public string? OldText { get; set; }

    public string? NewText { get; set; }

    public int[]? OldSubTimes { get; }

    public int[]? NewSubTimes { get; }

    public int? OldStart { get; set; }

    public int? NewStart { get; set; }

    public int? OldStop { get; set; }

    public int? NewStop { get; set; }
}
