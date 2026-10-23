using System.Text;
using VisualSubSync.Core.TextPipe;
using VisualSubSync.Core.Undo;

namespace VisualSubSync.Core.Tests;

public class TextPipeTests
{
    /// <summary>Text pipe em memória: uma cor por caractere.</summary>
    private sealed class FakePipe(string text) : ITextPipe
    {
        private readonly StringBuilder _text = new(text);
        private readonly List<int> _colors = [.. Enumerable.Repeat(0, text.Length)];

        public string Text => _text.ToString();

        public int SelStart { get; private set; }

        public int SelLength { get; private set; }

        public int ColorAt(int index) => _colors[index];

        public void Select(int start, int length) => (SelStart, SelLength) = (start, length);

        public IReadOnlyList<TextPipeColorRun> GetColors(int start, int length)
        {
            var runs = new List<TextPipeColorRun>();
            for (int i = start; i < start + length; i++)
            {
                if (runs.Count > 0 && runs[^1].Color == _colors[i])
                {
                    runs[^1] = runs[^1] with { Length = runs[^1].Length + 1 };
                }
                else
                {
                    runs.Add(new TextPipeColorRun(i, 1, _colors[i]));
                }
            }

            return runs;
        }

        public void SetColor(int start, int length, int color)
        {
            for (int i = start; i < start + length; i++)
            {
                _colors[i] = color;
            }
        }

        public void Delete(int start, int length)
        {
            _text.Remove(start, length);
            _colors.RemoveRange(start, length);
        }

        public void Insert(int start, string text)
        {
            _text.Insert(start, text);
            _colors.InsertRange(start, Enumerable.Repeat(0, text.Length));
        }
    }

    [Fact]
    public void Color_alternates_with_the_subtitle_count()
    {
        Assert.Equal(0x003333FF, TextPipeTask.ColorFor(0));
        Assert.Equal(0x00FF8000, TextPipeTask.ColorFor(3));
    }

    [Fact]
    public void Colorize_and_undo_restore_each_original_color()
    {
        var pipe = new FakePipe("abcdef");
        pipe.SetColor(2, 1, 7);
        pipe.Select(1, 3);
        var task = new TextPipeTask(pipe, TextPipeAutoOption.Colorize, 5);
        task.DoTask();
        Assert.Equal([0, 5, 5, 5, 0], Enumerable.Range(0, 5).Select(pipe.ColorAt));
        task.UndoTask();
        Assert.Equal([0, 0, 7, 0, 0], Enumerable.Range(0, 5).Select(pipe.ColorAt));
        Assert.Equal((1, 3), (pipe.SelStart, pipe.SelLength));
    }

    [Fact]
    public void Delete_removes_the_selection_and_undo_puts_it_back_selected()
    {
        var pipe = new FakePipe("one\r\ntwo\r\nthree");
        pipe.Select(5, 5);
        var task = new TextPipeTask(pipe, TextPipeAutoOption.Delete, 0);
        task.DoTask();
        Assert.Equal("one\r\nthree", pipe.Text);
        Assert.Equal((5, 0), (pipe.SelStart, pipe.SelLength));
        task.UndoTask();
        Assert.Equal("one\r\ntwo\r\nthree", pipe.Text);
        Assert.Equal((5, 5), (pipe.SelStart, pipe.SelLength));
    }

    [Fact]
    public void Delete_all_before_removes_up_to_the_end_of_the_selection()
    {
        var pipe = new FakePipe("one\r\ntwo\r\nthree");
        pipe.Select(5, 3);
        var task = new TextPipeTask(pipe, TextPipeAutoOption.DeleteAllBefore, 0);
        task.DoTask();
        Assert.Equal("\r\nthree", pipe.Text);
        task.UndoTask();
        Assert.Equal("one\r\ntwo\r\nthree", pipe.Text);
        Assert.Equal((5, 3), (pipe.SelStart, pipe.SelLength));
    }

    [Fact]
    public void Composite_undoes_in_reverse_order()
    {
        var log = new List<string>();
        var composite = new CompositeTask(new LogTask(log, "a"), new LogTask(log, "b"));
        composite.DoTask();
        composite.UndoTask();
        Assert.Equal(["do a", "do b", "undo b", "undo a"], log);
    }

    [Fact]
    public void Files_detect_rtf_and_encode_plain_text_as_utf16()
    {
        Assert.True(TextPipeFile.IsRtf("{\\rtf1\\ansi"u8));
        Assert.False(TextPipeFile.IsRtf("hello"u8));
        byte[] bytes = TextPipeFile.EncodePlainText("é");
        Assert.Equal(new byte[] { 0xFF, 0xFE, 0xE9, 0x00 }, bytes);
        Assert.Equal("é", TextPipeFile.ReadPlainText(bytes));
    }

    [Fact]
    public void Stripped_srt_keeps_only_the_texts()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "1\r\n00:00:01,000 --> 00:00:02,000\r\nHello\r\nworld\r\n\r\n2\r\n00:00:03,000 --> 00:00:04,000\r\nBye\r\n");
            Assert.Equal("Hello\r\nworld\r\nBye", TextPipeFile.ReadStrippedSrt(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class LogTask(List<string> log, string name) : UndoableTask
    {
        public override string Name => name;

        public override void DoTask() => log.Add("do " + name);

        public override void UndoTask() => log.Add("undo " + name);
    }
}
