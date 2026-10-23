using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using VisualSubSync.App.Services;
using VisualSubSync.Core.TextPipe;

namespace VisualSubSync.App.Controls;

/// <summary>
/// O MemoTextPipe (TTntRichEdit) sobre um RichTextBox do WPF. As posições contam os caracteres
/// dos Runs e duas para cada quebra (fim de parágrafo, menos o último, e LineBreak), como o texto
/// "\r\n" que o TntRichEdit devolve.
/// </summary>
internal sealed class RichTextPipe : ITextPipeView
{
    private readonly RichTextBox box;

    public RichTextPipe(RichTextBox box)
    {
        this.box = box;
        box.Document = NewDocument();
    }

    private FlowDocument Document => box.Document;

    public string Text
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            foreach (var piece in Pieces())
            {
                sb.Append(piece.Run ?? "\r\n");
            }

            return sb.ToString();
        }
    }

    public int SelStart => OffsetOf(box.Selection.Start);

    public int SelLength => OffsetOf(box.Selection.End) - SelStart;

    public bool IsFocused => box.IsKeyboardFocusWithin;

    public void Select(int start, int length) =>
        box.Selection.Select(PositionAt(start), PositionAt(start + length));

    public IReadOnlyList<TextPipeColorRun> GetColors(int start, int length)
    {
        var runs = new List<TextPipeColorRun>();
        int end = start + length;
        int count = 0;
        foreach (var piece in Pieces())
        {
            int pieceStart = count;
            count += piece.Length;
            if (piece.Run is null || count <= start || pieceStart >= end)
            {
                continue;
            }

            int s = Math.Max(start, pieceStart);
            int l = Math.Min(end, count) - s;
            int color = piece.Position.Parent is TextElement { Foreground: SolidColorBrush brush }
                ? ToTColor(brush.Color)
                : ToTColor(((box.Foreground as SolidColorBrush)?.Color) ?? Colors.Black);
            if (runs.Count > 0 && runs[^1].Color == color && runs[^1].Start + runs[^1].Length == s)
            {
                runs[^1] = runs[^1] with { Length = runs[^1].Length + l };
            }
            else
            {
                runs.Add(new TextPipeColorRun(s, l, color));
            }
        }

        return runs;
    }

    public void SetColor(int start, int length, int color) =>
        new TextRange(PositionAt(start), PositionAt(start + length))
            .ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(FromTColor(color)));

    public void Delete(int start, int length) =>
        new TextRange(PositionAt(start), PositionAt(start + length)).Text = string.Empty;

    public void Insert(int start, string text)
    {
        var position = PositionAt(start);
        new TextRange(position, position).Text = text;
    }

    public void Clear() => box.Document = NewDocument();

    public void SetText(string text)
    {
        var document = NewDocument();
        document.Blocks.Clear();
        foreach (string line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            document.Blocks.Add(new Paragraph(new Run(line.TrimEnd('\r'))));
        }

        box.Document = document;
    }

    public void ReplaceSelection(string text) => box.Selection.Text = text;

    public void LoadRtf(byte[] rtf)
    {
        var document = NewDocument();
        using var stream = new MemoryStream(rtf);
        new TextRange(document.ContentStart, document.ContentEnd).Load(stream, DataFormats.Rtf);
        box.Document = document;
    }

    public byte[] SaveRtf()
    {
        using var stream = new MemoryStream();
        new TextRange(Document.ContentStart, Document.ContentEnd).Save(stream, DataFormats.Rtf);
        return stream.ToArray();
    }

    public void ScrollToPosition(int position)
    {
        var pointer = PositionAt(position);
        box.Selection.Select(pointer, pointer);
        box.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            box.UpdateLayout();
            var rect = pointer.GetCharacterRect(LogicalDirection.Forward);
            if (!rect.IsEmpty)
            {
                box.ScrollToVerticalOffset(box.VerticalOffset + rect.Top);
            }
        });
    }

    /// <summary>Um documento novo com a mesma aparência (parágrafos sem margem, como no RichEdit).</summary>
    private static FlowDocument NewDocument() => new(new Paragraph()) { PagePadding = new Thickness(2, 0, 2, 0) };

    /// <summary>TColor é $00BBGGRR.</summary>
    private static int ToTColor(Color c) => c.R | (c.G << 8) | (c.B << 16);

    private static Color FromTColor(int color) => Color.FromRgb((byte)color, (byte)(color >> 8), (byte)(color >> 16));

    private TextPointer PositionAt(int offset)
    {
        int count = 0;
        foreach (var piece in Pieces())
        {
            if (piece.Run is not null)
            {
                if (offset - count <= piece.Length)
                {
                    return piece.Position.GetPositionAtOffset(Math.Max(0, offset - count)) ?? piece.Position;
                }
            }
            else if (offset <= count)
            {
                return piece.Position;
            }

            count += piece.Length;
        }

        return Document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
    }

    private int OffsetOf(TextPointer target)
    {
        int count = 0;
        foreach (var piece in Pieces())
        {
            if (piece.Run is not null)
            {
                var end = piece.Position.GetPositionAtOffset(piece.Length);
                if (end is null || target.CompareTo(end) <= 0)
                {
                    return count + Math.Clamp(piece.Position.GetOffsetToPosition(target), 0, piece.Length);
                }
            }
            else if (target.CompareTo(piece.Position) <= 0)
            {
                return count;
            }

            count += piece.Length;
        }

        return count;
    }

    /// <summary>Os pedaços do texto em ordem: Runs (com o texto) e quebras (Run nulo, 2 caracteres).</summary>
    private IEnumerable<(TextPointer Position, int Length, string? Run)> Pieces()
    {
        var last = Document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward)?.Paragraph;
        for (var p = Document.ContentStart; p is not null; p = p.GetNextContextPosition(LogicalDirection.Forward))
        {
            switch (p.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                    string run = p.GetTextInRun(LogicalDirection.Forward);
                    yield return (p, run.Length, run);
                    break;
                case TextPointerContext.ElementStart when p.GetAdjacentElement(LogicalDirection.Forward) is LineBreak:
                    yield return (p, 2, null);
                    break;
                case TextPointerContext.ElementEnd when p.GetAdjacentElement(LogicalDirection.Forward) is Paragraph para && para != last:
                    yield return (p, 2, null);
                    break;
            }
        }
    }
}
