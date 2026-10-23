using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Timing;
using VisualSubSync.Core.Undo;

namespace VisualSubSync.App.ViewModels;

/// <summary>TUndoableSetTimeTask: novos tempos de uma legenda; o índice acompanha a reordenação.</summary>
internal sealed class SetTimeTask(MainViewModel owner, int index, int oldStartMs, int oldStopMs, int newStartMs, int newStopMs)
    : UndoableTask
{
    private int _index = index;

    public override string Name => "Set time";

    public override void DoTask() => _index = owner.SetSubtitleTime(_index, newStartMs, newStopMs);

    public override void UndoTask() => _index = owner.SetSubtitleTime(_index, oldStartMs, oldStopMs);
}

/// <summary>TUndoableDelayTask: atraso no início, no fim ou nos dois tempos das legendas indicadas.</summary>
internal sealed class DelayTask(MainViewModel owner, int[] indexes, int delayMs, DelayShiftType shiftType) : UndoableTask
{
    public override string Name => "Delay " + TimeFormat.ToVss(delayMs);

    public override void DoTask() => owner.ApplyDelay(indexes, delayMs, shiftType);

    public override void UndoTask() => owner.ApplyDelay(indexes, -delayMs, shiftType);
}

/// <summary>TUndoableAddTask: nova legenda (com SetText, quando vem do text pipe); desfazer apaga a legenda criada.</summary>
internal sealed class AddTask(MainViewModel owner, int startMs, int stopMs, bool autoSelect, string text = "") : UndoableTask
{
    private int _index;

    public override string Name => "Add subtitle";

    public override void DoTask() => _index = owner.AddSubtitle(startMs, stopMs, autoSelect, text);

    public override void UndoTask() => owner.DeleteSubtitles([_index]);
}

/// <summary>
/// TUndoableSubTextTask: edição de texto no editor (TUndoableTextTask) presa à legenda em que foi
/// feita; desfazer e refazer focam essa legenda antes de mexer no texto.
/// </summary>
internal sealed class SubTextTask(MainViewModel owner, int index, TextEdit edit) : UndoableTask
{
    public override string Name => "TUndoableTextTask";

    public override void DoTask() =>
        owner.ApplyTextEdit(index, edit.Apply, edit.SelStart, edit.SelLength);

    public override void UndoTask() =>
        owner.ApplyTextEdit(index, edit.Revert, edit.OriginalSelStart, edit.OriginalSelLength);

    /// <remarks>O original não compara a legenda; aqui só se junta digitação na mesma legenda.</remarks>
    public override bool Merge(UndoableTask task) => task is SubTextTask next && next.Index == index && edit.Merge(next.Edit);

    private int Index => index;

    private TextEdit Edit => edit;
}

/// <summary>TUndoableDeleteTask: guarda cópias das legendas apagadas para devolvê-las.</summary>
internal sealed class DeleteTask(MainViewModel owner, int[] indexes) : UndoableTask
{
    private List<Subtitle> _deleted = [];

    public override string Name => "Delete";

    public override void DoTask()
    {
        _deleted = owner.CloneSubtitles(indexes);
        owner.DeleteSubtitles(indexes);
    }

    public override void UndoTask()
    {
        owner.RestoreSubtitles(_deleted);
        _deleted = [];
    }
}

/// <summary>TUndoableSplitTask: desfazer apaga a segunda parte e devolve os tempos da primeira.</summary>
internal sealed class SplitTask(MainViewModel owner, int index, int startMs, int stopMs, int splitMs, int blankMs)
    : UndoableTask
{
    public override string Name => "Split";

    public override void DoTask() => owner.SplitSubtitle(index, splitMs, blankMs);

    public override void UndoTask()
    {
        owner.DeleteSubtitles([index + 1]);
        owner.SetSubtitleTime(index, startMs, stopMs);
    }
}

/// <summary>
/// TUndoableMergeTask: troca as legendas pela mesclada; desfazer apaga a mesclada e devolve as
/// originais. Com <paramref name="selectedSubIndex"/>, a seleção da onda acompanha.
/// </summary>
internal sealed class MergeTask(MainViewModel owner, int[] indexes, int selectedSubIndex, MergeType type) : UndoableTask
{
    private List<Subtitle> _deleted = [];
    private int _mergedIndex;

    public override string Name => "Merge";

    public override void DoTask()
    {
        var merged = owner.MergeSubtitles(indexes, type);
        _deleted = owner.CloneSubtitles(indexes);
        owner.DeleteSubtitles(indexes);
        _mergedIndex = owner.InsertSubtitle(merged);
        owner.FocusSubtitle(_mergedIndex);
        if (selectedSubIndex != -1)
        {
            owner.SelectSubtitleAt(_mergedIndex);
        }
    }

    public override void UndoTask()
    {
        owner.DeleteSubtitles([_mergedIndex]);
        owner.RestoreSubtitles(_deleted);
        _deleted = [];
        if (selectedSubIndex != -1)
        {
            owner.SelectSubtitleAt(selectedSubIndex);
        }
    }
}

/// <summary>TUndoableMultiChangeTask: textos de várias legendas trocados de uma vez (Replace All).</summary>
internal sealed class MultiChangeTask(MainViewModel owner, IReadOnlyList<TextChange> changes) : UndoableTask
{
    public override string Name => "TUndoableMultiChangeTask";

    public override void DoTask() => owner.ApplyTextChanges(changes, isUndo: false);

    public override void UndoTask() => owner.ApplyTextChanges(changes, isUndo: true);
}

/// <summary>TUndoableMultiAddTask: colar; desfazer apaga as coladas (guardando cópias para refazer).</summary>
internal sealed class MultiAddTask(MainViewModel owner, List<Subtitle> subtitles) : UndoableTask
{
    private List<Subtitle> _pending = subtitles;
    private int[] _indexes = [];

    public override string Name => "MultiAdd";

    public override void DoTask()
    {
        _indexes = owner.RestoreSubtitles(_pending);
        _pending = [];
    }

    public override void UndoTask()
    {
        _pending = owner.CloneSubtitles(_indexes);
        owner.DeleteSubtitles([.. _indexes.Order()]);
    }
}

/// <summary>TUndoableInsertSceneChange: desfazer apaga a mudança de cena no mesmo tempo.</summary>
internal sealed class InsertSceneChangeTask(MainViewModel owner, int timeMs) : UndoableTask
{
    public override string Name => "TUndoableInsertSceneChange";

    public override void DoTask() => owner.InsertSceneChanges([timeMs]);

    public override void UndoTask() => owner.DeleteSceneChanges(timeMs, timeMs);
}

/// <summary>TUndoableDeleteSceneChange: guarda as mudanças de cena apagadas para devolvê-las.</summary>
internal sealed class DeleteSceneChangeTask(MainViewModel owner, int startMs, int stopMs) : UndoableTask
{
    private int[] _deleted = [];

    public override string Name => "TUndoableDeleteSceneChange";

    public override void DoTask() => _deleted = owner.DeleteSceneChanges(startMs, stopMs);

    public override void UndoTask()
    {
        owner.InsertSceneChanges(_deleted);
        _deleted = [];
    }
}
