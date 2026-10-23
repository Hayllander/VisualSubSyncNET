using CommunityToolkit.Mvvm.Input;
using VisualSubSync.App.Services;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.App.ViewModels;

/// <summary>
/// Edição da lista de legendas (main.pas): apagar, mesclar (Merge, Merge dialog, Merge on 1 line,
/// Merge with previous/next) e dividir no cursor, todas com desfazer.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>As legendas selecionadas na lista (vtvSubsList.GetFirstSelected/GetNextSelected).</summary>
    private IReadOnlyList<SubtitleItemViewModel> _selectedItems = [];

    /// <summary>vtvSubsList.SelectedCount.</summary>
    public int SelectedCount => _selectedItems.Count;

    /// <summary>WAVDisplayer.SelectionIsEmpty.</summary>
    public bool IsWaveSelectionEmpty => SelectionIsEmpty;

    /// <summary>A janela informa a seleção da lista sempre que ela muda.</summary>
    public void UpdateSelectedSubtitles(IEnumerable<SubtitleItemViewModel> items)
    {
        _selectedItems = [.. items];
        UpdateStylesComboboxFromSelection();
    }

    /// <summary>WAVDisplayer.RangeList.GetRangeIdxAt(WAVDisplayer.GetCursorPos).</summary>
    public int RangeIndexAtCursor() => GetRangeIdxAt(CursorMs);

    /// <summary>pmiSubListDeleteClick: apaga as legendas selecionadas.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void DeleteSelectedSubtitles()
    {
        int[] indexes = SelectedIndexes();
        if (indexes.Length > 0)
        {
            DoAndPush(new DeleteTask(this, indexes));
        }
    }

    /// <summary>WAVDisplayPopup_DeleteRange: apaga a legenda sob o cursor.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void DeleteSubtitleAtCursor()
    {
        int index = RangeIndexAtCursor();
        if (index != -1)
        {
            DoAndPush(new DeleteTask(this, [index]));
        }
    }

    /// <summary>ActionSplitAtCursorExecute: divide a legenda sob o cursor, com o espaço mínimo entre legendas.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void SplitAtCursor()
    {
        int index = RangeIndexAtCursor();
        if (index != -1)
        {
            var sub = _document.Items[index];
            DoAndPush(new SplitTask(this, index, sub.StartMs, sub.StopMs, CursorMs, Settings.SpaceKeyBlankBetweenSubtitles));
        }
    }

    /// <summary>
    /// ActionDelayExecute: com mais de uma selecionada a janela já vem em "Selected subtitles"; com
    /// seleção na onda, o atraso vem com o tamanho dela.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void Delay()
    {
        var request = new DelayRequest(SelectedCount > 1, SelectionIsEmpty ? null : SelectionStopMs - SelectionStartMs);
        if (_dialogs.ShowDelay(request) is not { } result)
        {
            return;
        }

        int[] indexes = result.ApplyTo switch
        {
            DelayApplyTo.Selected => SelectedIndexes(),
            DelayApplyTo.FromCursor => SelectedSubtitle is { } focused
                ? [.. Enumerable.Range(Subtitles.IndexOf(focused), Subtitles.Count - Subtitles.IndexOf(focused))]
                : [],
            _ => [.. Enumerable.Range(0, Subtitles.Count)],
        };
        DoAndPush(new DelayTask(this, indexes, result.DelayMs, result.ShiftType));
    }

    /// <summary>ActionMergeExecute: Merge(mtNormal, mrSelected).</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void Merge() => Merge(MergeType.Normal, SelectedIndexes());

    /// <summary>ActionMergeDialogExecute: com mais de uma selecionada, Merge(mtDialog, mrSelected).</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void MergeDialog()
    {
        if (SelectedCount > 1)
        {
            Merge(MergeType.Dialog, SelectedIndexes());
        }
    }

    /// <summary>ActionMergeOnOneLineExecute: com mais de uma selecionada, Merge(mtOneLine, mrSelected).</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void MergeOnOneLine()
    {
        if (SelectedCount > 1)
        {
            Merge(MergeType.OneLine, SelectedIndexes());
        }
    }

    /// <summary>ActionMergeWithPreviousExecute: a primeira selecionada com a anterior.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void MergeWithPrevious()
    {
        int[] indexes = SelectedIndexes();
        if (indexes.Length > 0 && indexes[0] > 0)
        {
            Merge(MergeType.Normal, [indexes[0] - 1, indexes[0]]);
        }
    }

    /// <summary>ActionMergeWithNextExecute: a primeira selecionada com a seguinte.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void MergeWithNext()
    {
        int[] indexes = SelectedIndexes();
        if (indexes.Length > 0 && indexes[0] + 1 < Subtitles.Count)
        {
            Merge(MergeType.Normal, [indexes[0], indexes[0] + 1]);
        }
    }

    /// <summary>
    /// TMainForm.Merge: se a legenda presa à seleção da onda está entre as mescladas, a mesclada é
    /// que fica selecionada.
    /// </summary>
    private void Merge(MergeType type, int[] indexes)
    {
        // O original não confere: sem legenda selecionada, MergeSubtitles leria fora da lista.
        if (indexes.Length == 0)
        {
            return;
        }

        int selected = Waveform?.SelectedRangeIndex ?? -1;
        int selectedSubIndex = selected >= 0 && indexes.Contains(selected) ? selected : -1;
        DoAndPush(new MergeTask(this, indexes, selectedSubIndex, type));
    }

    /// <summary>Os índices das selecionadas, na ordem da lista.</summary>
    private int[] SelectedIndexes() =>
        [.. _selectedItems.Select(Subtitles.IndexOf).Where(i => i >= 0).Order()];

    /// <summary>TRangeList.GetRangeIdxAt: a última legenda que começa até a posição e a contém.</summary>
    private int GetRangeIdxAt(int posMs)
    {
        if (_document.Items.Count == 0)
        {
            return -1;
        }

        for (int i = Math.Clamp(FindInsertPos(posMs), 0, _document.Items.Count - 1); i >= 0; i--)
        {
            var sub = _document.Items[i];
            if (sub.StartMs <= posMs && sub.StopMs >= posMs)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// TWAVDisplayer guarda a legenda presa à seleção, não a posição dela: depois de inserir ou apagar,
    /// a seleção acompanha a mesma legenda (ou é solta, se ela saiu da lista).
    /// </summary>
    private void KeepingSelectedRange(Action change)
    {
        int selected = Waveform?.SelectedRangeIndex ?? -1;
        var model = selected >= 0 && selected < _document.Items.Count ? _document.Items[selected] : null;
        change();
        if (model is null)
        {
            return;
        }

        if (_document.Items.Contains(model))
        {
            Waveform?.SelectRange(model.StartMs, model.StopMs);
        }
        else
        {
            Waveform?.ClearSelection();
        }
    }

    private void Renumber(int from)
    {
        for (int i = from; i < Subtitles.Count; i++)
        {
            Subtitles[i].Index = i + 1;
        }
    }

    /// <summary>AddSubtitle(SubRange, False): insere na ordem, sem mudar o foco; devolve o índice.</summary>
    internal int InsertSubtitle(Subtitle model)
    {
        int index = InsertPosition(model.StartMs, model.StopMs);
        KeepingSelectedRange(() =>
        {
            _document.Items.Insert(index, model);
            Subtitles.Insert(index, new SubtitleItemViewModel(model, index + 1, OnSubtitleTextChanged));
            Renumber(index + 1);
            IsDirty = true;
            RefreshRanges();
        });

        // Update SearchNodeIndex (TODO : SearchPos), como o AddSubtitle do legado.
        if (_searchNodeIndex >= index)
        {
            _searchNodeIndex++;
        }

        UpdateDurationWithSubOnly();
        return index;
    }

    /// <summary>CloneSubtitles.</summary>
    internal List<Subtitle> CloneSubtitles(int[] indexes) => [.. indexes.Select(i => _document.Items[i].Clone())];

    /// <summary>
    /// RestoreSubtitles: devolve as legendas apagadas, da última para a primeira; devolve onde cada
    /// uma ficou, na ordem da lista recebida.
    /// </summary>
    internal int[] RestoreSubtitles(List<Subtitle> subtitles)
    {
        var models = subtitles.Select(s => s.Clone()).ToList();
        for (int i = models.Count - 1; i >= 0; i--)
        {
            InsertSubtitle(models[i]);
        }

        return [.. models.Select(m => _document.Items.IndexOf(m))];
    }

    /// <summary>CopyVTVToClipboard: as selecionadas no formato próprio e como texto SRT.</summary>
    public (byte[] Data, string Text)? CopySelectedSubtitles()
    {
        int[] indexes = SelectedIndexes();
        if (indexes.Length == 0)
        {
            return null;
        }

        var subs = indexes.Select(i => _document.Items[i]).ToList();
        return (SubtitleClipboard.Serialize(subs), SubtitleClipboard.ToText(indexes.Select(i => (i + 1, _document.Items[i]))));
    }

    /// <summary>
    /// ActionPasteExecute (lista) e ActionPasteAtCursorExecute: as legendas coladas entram numa
    /// TUndoableMultiAddTask; "at cursor" desloca todas para que a primeira comece no cursor.
    /// </summary>
    public void PasteSubtitles(List<Subtitle> subtitles, bool atCursor)
    {
        ArgumentNullException.ThrowIfNull(subtitles);
        if (subtitles.Count == 0)
        {
            return;
        }

        if (atCursor)
        {
            SubtitleClipboard.MoveToCursor(subtitles, CursorMs);
        }

        DoAndPush(new MultiAddTask(this, subtitles));
    }

    /// <summary>MergeSubtitles.</summary>
    internal Subtitle MergeSubtitles(int[] indexes, MergeType type) =>
        SubtitleEditRules.Merge([.. indexes.Select(i => _document.Items[i])], type);

    /// <summary>FocusNode(Node, False).</summary>
    internal void FocusSubtitle(int index)
    {
        if (index >= 0 && index < Subtitles.Count)
        {
            SelectedSubtitle = Subtitles[index];
        }
    }

    /// <summary>SelectNode / SelectNodeAtIndex: foca a legenda e a seleciona na onda (vtvSubsListDblClick).</summary>
    internal void SelectSubtitleAt(int index)
    {
        if (index >= 0 && index < Subtitles.Count)
        {
            SelectedSubtitle = Subtitles[index];
            ActivateSelectedSubtitle();
        }
    }

    /// <summary>
    /// SplitSubtitle: a legenda termina no corte e uma cópia dela (texto e estilo iguais) começa logo
    /// depois, inserida em seguida.
    /// </summary>
    internal void SplitSubtitle(int index, int splitMs, int blankMs)
    {
        var sub = _document.Items[index];
        var (stop1, start2) = SubtitleEditRules.SplitTimes(sub.StartMs, sub.StopMs, splitMs, blankMs);
        var second = sub.Clone();
        sub.StopMs = stop1;
        second.StartMs = start2;
        KeepingSelectedRange(() =>
        {
            _document.Items.Insert(index + 1, second);
            Subtitles.Insert(index + 1, new SubtitleItemViewModel(second, index + 2, OnSubtitleTextChanged));
            Renumber(index + 2);
            Subtitles[index].RefreshTimes();
            IsDirty = true;
            RefreshRanges();
        });
    }
}
