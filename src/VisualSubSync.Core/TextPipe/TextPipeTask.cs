using VisualSubSync.Core.Undo;

namespace VisualSubSync.Core.TextPipe;

/// <summary>
/// TUndoablePipeTask: o que acontece no text pipe depois de usar a seleção (colorir, apagar a
/// seleção ou apagar tudo até o fim dela). A seleção é guardada na criação (SetData).
/// </summary>
public sealed class TextPipeTask : UndoableTask
{
    /// <summary>NewColor de ColorizeOrDeleteTextPipe: $003333FF (vermelho).</summary>
    public const int EvenColor = 0x003333FF;

    /// <summary>$00FF8000 (azul), quando o número de legendas é ímpar.</summary>
    public const int OddColor = 0x00FF8000;

    private readonly ITextPipe _pipe;
    private readonly TextPipeAutoOption _action;
    private readonly int _newColor;
    private readonly int _oldSelStart;
    private readonly int _oldSelLength;
    private readonly IReadOnlyList<TextPipeColorRun> _oldColors;
    private string _oldSelText;

    public TextPipeTask(ITextPipe pipe, TextPipeAutoOption action, int newColor)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        _pipe = pipe;
        _action = action;
        _newColor = newColor;
        _oldSelStart = pipe.SelStart;
        _oldSelLength = pipe.SelLength;
        _oldSelText = SafeSubstring(pipe.Text, _oldSelStart, _oldSelLength);
        _oldColors = action == TextPipeAutoOption.Colorize ? pipe.GetColors(_oldSelStart, _oldSelLength) : [];
    }

    public override string Name => "TUndoablePipeTask";

    /// <summary>A cor alterna conforme o número de legendas (vtvSubsList.TotalCount mod 2).</summary>
    public static int ColorFor(int subtitleCount) => subtitleCount % 2 != 0 ? OddColor : EvenColor;

    public override void DoTask()
    {
        switch (_action)
        {
            case TextPipeAutoOption.Colorize:
                _pipe.Select(_oldSelStart, _oldSelLength);
                _pipe.SetColor(_oldSelStart, _oldSelLength, _newColor);
                break;
            case TextPipeAutoOption.Delete:
                _pipe.Delete(_oldSelStart, _oldSelLength);
                _pipe.Select(_oldSelStart, 0);
                break;
            case TextPipeAutoOption.DeleteAllBefore:
                int length = Math.Min(_oldSelStart + _oldSelLength, _pipe.Text.Length);
                _oldSelText = _pipe.Text[..length];
                _pipe.Delete(0, length);
                _pipe.Select(0, 0);
                break;
        }
    }

    public override void UndoTask()
    {
        switch (_action)
        {
            case TextPipeAutoOption.Colorize:
                foreach (var run in _oldColors)
                {
                    _pipe.SetColor(run.Start, run.Length, run.Color);
                }

                break;
            case TextPipeAutoOption.Delete:
                _pipe.Insert(_oldSelStart, _oldSelText);
                break;
            case TextPipeAutoOption.DeleteAllBefore:
                _pipe.Insert(0, _oldSelText);
                break;
        }

        _pipe.Select(_oldSelStart, _oldSelLength);
    }

    private static string SafeSubstring(string text, int start, int length)
    {
        start = Math.Clamp(start, 0, text.Length);
        return text.Substring(start, Math.Clamp(length, 0, text.Length - start));
    }
}
