namespace VisualSubSync.Core.Undo;

/// <summary>TUndoableTask (UndoableTaskUnit.pas).</summary>
public abstract class UndoableTask
{
    public abstract string Name { get; }

    public abstract void DoTask();

    public abstract void UndoTask();

    /// <summary>Junta a tarefa seguinte a esta (digitação contínua, por exemplo). Por padrão, nada junta.</summary>
    public virtual bool Merge(UndoableTask task) => false;
}

/// <summary>
/// UndoStack e RedoStack do TMainForm, com PushUndoableTask, ActionUndoExecute e ActionRedoExecute.
/// </summary>
public sealed class UndoHistory
{
    private readonly Stack<UndoableTask> _undo = new();
    private readonly Stack<UndoableTask> _redo = new();

    /// <summary>ActionUndo.Enabled / ActionRedo.Enabled mudaram.</summary>
    public event EventHandler? Changed;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public UndoableTask? Top => _undo.Count > 0 ? _undo.Peek() : null;

    /// <summary>PushUndoableTask: limpa o refazer e tenta juntar com a tarefa do topo.</summary>
    public void Push(UndoableTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        _redo.Clear();
        if (_undo.Count == 0 || !_undo.Peek().Merge(task))
        {
            _undo.Push(task);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_undo.Count > 0)
        {
            var task = _undo.Pop();
            task.UndoTask();
            _redo.Push(task);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Redo()
    {
        if (_redo.Count > 0)
        {
            var task = _redo.Pop();
            task.DoTask();
            _undo.Push(task);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>ClearStack(RedoStack); ClearStack(UndoStack).</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
