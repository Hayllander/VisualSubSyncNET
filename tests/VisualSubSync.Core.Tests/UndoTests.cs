using VisualSubSync.Core.Undo;

namespace VisualSubSync.Core.Tests;

public class UndoTests
{
    private sealed class TextTask(List<string> log, TextEdit edit) : UndoableTask
    {
        public TextEdit Edit => edit;

        public override string Name => "text";

        public override void DoTask() => log.Add("do");

        public override void UndoTask() => log.Add("undo");

        public override bool Merge(UndoableTask task) => task is TextTask next && edit.Merge(next.Edit);
    }

    [Fact]
    public void Push_clears_redo_and_undo_redo_move_between_stacks()
    {
        var log = new List<string>();
        var history = new UndoHistory();
        int changes = 0;
        history.Changed += (_, _) => changes++;
        history.Push(new TextTask(log, TextEdit.Diff("ab", "a", 1, 0)));
        history.Push(new TextTask(log, TextEdit.Diff("xab", "ab", 0, 0)));
        Assert.True(history.CanUndo);
        history.Undo();
        Assert.True(history.CanRedo);
        history.Redo();
        Assert.False(history.CanRedo);
        history.Undo();
        history.Push(new TextTask(log, TextEdit.Diff("abc", "ab", 2, 0)));
        Assert.False(history.CanRedo);
        Assert.Equal(["undo", "do", "undo"], log);
        history.Clear();
        Assert.False(history.CanUndo || history.CanRedo);
        Assert.Equal(7, changes);
    }

    [Fact]
    public void Diff_finds_the_smallest_replaced_part()
    {
        var edit = TextEdit.Diff("Hello big world", "Hello world", 6, 0);
        Assert.Equal((6, "", "big "), (edit.ReplaceStart, edit.OriginalText, edit.ReplaceByText));
        Assert.Equal(10, edit.SelStart);
        Assert.Equal("Hello big world", edit.Apply("Hello world"));
        Assert.Equal("Hello world", edit.Revert("Hello big world"));

        var replace = TextEdit.Diff("aXc", "abc", 1, 1);
        Assert.Equal((1, "b", "X"), (replace.ReplaceStart, replace.OriginalText, replace.ReplaceByText));
    }

    [Fact]
    public void Typing_backspace_and_delete_merge_into_one_task()
    {
        var history = new UndoHistory();
        var log = new List<string>();
        history.Push(new TextTask(log, TextEdit.Diff("a", "", 0, 0)));
        history.Push(new TextTask(log, TextEdit.Diff("ab", "a", 1, 0)));
        history.Push(new TextTask(log, TextEdit.Diff("abc", "ab", 2, 0)));
        var typed = ((TextTask)history.Top!).Edit;
        Assert.Equal("abc", typed.ReplaceByText);
        Assert.Equal("abc", typed.Apply(""));

        var back = TextEdit.Diff("abcd", "abcde", 5, 0);
        Assert.True(back.Merge(TextEdit.Diff("abc", "abcd", 4, 0)));
        Assert.Equal((3, "de"), (back.ReplaceStart, back.OriginalText));
        Assert.Equal("abcde", back.Revert("abc"));

        var del = TextEdit.Diff("bcde", "abcde", 0, 0);
        Assert.True(del.Merge(TextEdit.Diff("cde", "bcde", 0, 0)));
        Assert.Equal((0, "ab"), (del.ReplaceStart, del.OriginalText));

        // Colar mais de um caractere não se junta à digitação.
        Assert.False(typed.Merge(TextEdit.Diff("abcXY", "abc", 3, 0)));

        // Depois de desfeita, a tarefa não junta mais nada.
        var undone = TextEdit.Diff("a", "", 0, 0);
        undone.Revert("a");
        Assert.False(undone.Merge(TextEdit.Diff("ab", "a", 1, 0)));
    }
}
