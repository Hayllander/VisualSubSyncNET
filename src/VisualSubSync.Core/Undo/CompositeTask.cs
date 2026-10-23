namespace VisualSubSync.Core.Undo;

/// <summary>TUndoableCompositeTask: faz as tarefas em ordem e desfaz na ordem inversa.</summary>
public sealed class CompositeTask(params UndoableTask[] tasks) : UndoableTask
{
    public override string Name => "TUndoableCompositeTask";

    public override void DoTask()
    {
        foreach (var task in tasks)
        {
            task.DoTask();
        }
    }

    public override void UndoTask()
    {
        for (int i = tasks.Length - 1; i >= 0; i--)
        {
            tasks[i].UndoTask();
        }
    }
}
