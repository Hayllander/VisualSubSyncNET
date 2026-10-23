using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Timing;
using VisualSubSync.Core.Undo;

namespace VisualSubSync.App.ViewModels;

/// <summary>
/// Desfazer/refazer (UndoStack/RedoStack, PushUndoableTask, ActionUndo/ActionRedo) e as operações
/// que as tarefas chamam no TMainForm: SetSubtitleTime, ApplyDelay, AddSubtitle, DeleteSubtitles e a
/// edição de texto do editor (TTntRichEditCustomUndo).
/// </summary>
public sealed partial class MainViewModel
{
    private readonly UndoHistory _undoStack = new();

    /// <summary>TTntRichEditCustomUndo.UndoDisabled: o texto muda por desfazer/refazer, sem gravar tarefa.</summary>
    private bool _undoDisabled;

    /// <summary>FSelStartBeforeChange / FSelLengthBeforeChange do editor.</summary>
    private (int Start, int Length) _editorSelection;

    /// <summary>Restaura a seleção do editor depois de desfazer/refazer uma edição de texto.</summary>
    public event EventHandler<(int Start, int Length)>? EditorSelectionRequested;

    private bool CanUndo => _undoStack.CanUndo;

    private bool CanRedo => _undoStack.CanRedo;

    /// <summary>ActionUndoExecute.</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _undoStack.Undo();

    /// <summary>ActionRedoExecute.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _undoStack.Redo();

    /// <summary>SaveSelectionInfo: a janela informa a seleção do editor sempre que ela muda.</summary>
    public void UpdateEditorSelection(int start, int length) => _editorSelection = (start, length);

    /// <summary>Faz a tarefa e a empilha (o par DoTask + PushUndoableTask do original).</summary>
    private void DoAndPush(UndoableTask task)
    {
        task.DoTask();
        _undoStack.Push(task);
    }

    /// <summary>ClearStack(RedoStack); ClearStack(UndoStack).</summary>
    private void ClearUndo() => _undoStack.Clear();

    private void OnUndoStackChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// TTntRichEditCustomUndo.Change + OnUndo: cada mudança de texto feita no editor vira uma
    /// TUndoableSubTextTask da legenda focada; digitação seguida se junta numa tarefa só.
    /// </summary>
    private void RecordTextChange(SubtitleItemViewModel item, string oldText)
    {
        int index = Subtitles.IndexOf(item);
        if (_undoDisabled || index < 0)
        {
            return;
        }

        var edit = TextEdit.Diff(item.Text, oldText, _editorSelection.Start, _editorSelection.Length);
        _undoStack.Push(new SubTextTask(this, index, edit));
    }

    /// <summary>SetSubtitleTime: novos tempos, reordenação, seleção presa acompanha; devolve o novo índice.</summary>
    internal int SetSubtitleTime(int index, int startMs, int stopMs)
    {
        var item = Subtitles[index];
        bool wasSelectedRange = Waveform?.SelectedRangeIndex == index;
        item.Model.StartMs = startMs;
        item.Model.StopMs = stopMs;
        if (item.Model.SubTimes.Length > 0)
        {
            item.Model.Text = Core.Subtitles.Karaoke.TextFromSubTimes(item.Model);
            item.RefreshAll();
        }
        SortSubtitles();
        item.RefreshTimes();
        IsDirty = true;
        RefreshRanges();
        if (wasSelectedRange)
        {
            Waveform?.SelectRange(startMs, stopMs);
        }

        UpdateDurationWithSubOnly();
        UpdateEditorStatus(_caret.Line, _caret.Column, _caret.FirstVisible);
        return Subtitles.IndexOf(item);
    }

    /// <summary>
    /// ApplyDelay: desloca os tempos pedidos, a seleção presa a uma delas acompanha, reordena e
    /// atualiza <paramref name="indexes"/> com as novas posições.
    /// </summary>
    internal void ApplyDelay(int[] indexes, int delayMs, DelayShiftType shiftType)
    {
        int selected = Waveform?.SelectedRangeIndex ?? -1;
        var items = indexes.Select(i => Subtitles[i]).ToList();
        var selectedItem = selected >= 0 && indexes.Contains(selected) ? Subtitles[selected] : null;
        foreach (var model in items.Select(i => i.Model))
        {
            if (shiftType != DelayShiftType.StopTimeOnly)
            {
                model.StartMs += delayMs;
            }

            if (shiftType != DelayShiftType.StartTimeOnly)
            {
                model.StopMs += delayMs;
            }

            if (shiftType == DelayShiftType.BothTimes)
            {
                for (int i = 0; i < model.SubTimes.Length; i++) model.SubTimes[i] += delayMs;
            }
            else if (ReferenceEquals(model, selectedItem?.Model) && model.SubTimes.Length > 0)
            {
                model.Text = Core.Subtitles.Karaoke.TextFromSubTimes(model);
            }
        }

        SortSubtitles();
        IsDirty = true;
        for (int i = 0; i < items.Count; i++)
        {
            items[i].RefreshAll();
            indexes[i] = Subtitles.IndexOf(items[i]);
        }

        RefreshRanges();
        if (selectedItem is not null)
        {
            Waveform?.SelectRange(selectedItem.Model.StartMs, selectedItem.Model.StopMs);
        }

        UpdateDurationWithSubOnly();
        UpdateEditorStatus(_caret.Line, _caret.Column, _caret.FirstVisible);
    }

    /// <summary>
    /// DeleteSubtitles: apaga de trás para frente; a legenda presa à seleção é solta; o foco vai para
    /// a que ficou no lugar da primeira apagada (ou para a primeira da lista).
    /// </summary>
    internal void DeleteSubtitles(int[] indexes)
    {
        KeepingSelectedRange(() =>
        {
            foreach (int index in indexes.OrderByDescending(i => i))
            {
                DeleteErrorsOf(_document.Items[index]);
                _document.Items.RemoveAt(index);
                Subtitles.RemoveAt(index);

                // Update SearchNodeIndex (TODO : SearchPos), como o legado.
                if (_searchNodeIndex > index)
                {
                    _searchNodeIndex--;
                }

                if (_searchNodeIndex >= index)
                {
                    _searchPos = 1;
                }
            }

            Renumber(indexes.Min());
            IsDirty = true;
            RefreshRanges();
        });

        // A legenda logo depois da anterior à primeira apagada; apagadas as últimas, nenhuma é focada.
        int first = indexes.Min();
        SelectedSubtitle = first < Subtitles.Count ? Subtitles[first] : null;
        UpdateDurationWithSubOnly();
    }

    /// <summary>TUndoableSubTextTask: foca a legenda (FocusNodeAt) e troca o texto sem gravar outra tarefa.</summary>
    internal void ApplyTextEdit(int index, Func<string, string> edit, int selStart, int selLength)
    {
        if (index < 0 || index >= Subtitles.Count)
        {
            return;
        }

        var item = Subtitles[index];
        SelectedSubtitle = item;
        _undoDisabled = true;
        try
        {
            item.Text = edit(item.Text);
        }
        finally
        {
            _undoDisabled = false;
        }

        EditorSelectionRequested?.Invoke(this, (selStart, selLength));
    }
}
