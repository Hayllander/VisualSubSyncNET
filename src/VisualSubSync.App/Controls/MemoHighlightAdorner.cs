using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using VisualSubSync.Core.Spelling;

namespace VisualSubSync.App.Controls;

/// <summary>
/// As cores do TagHighlight sobre o MemoSubtitleText. O TextBox do WPF não tem cor por trecho, então
/// o texto dele fica transparente e este adorno o desenha por cima, linha a linha, nas posições que o
/// próprio TextBox informa: texto em clWindowText, tags em clGrayText, a tag de karaokê selecionada em
/// vermelho e o sublinhado ondulado vermelho nas palavras recusadas.
/// </summary>
internal sealed class MemoHighlightAdorner : Adorner
{
    private readonly TextBox _box;
    private readonly Func<IReadOnlyList<HighlightSpan>> _computeSpans;
    private bool _pending;

    private MemoHighlightAdorner(TextBox box, Func<IReadOnlyList<HighlightSpan>> computeSpans)
        : base(box)
    {
        _box = box;
        _computeSpans = computeSpans;
        IsHitTestVisible = false;
        box.TextChanged += (_, _) => Refresh();
        box.SizeChanged += (_, _) => Refresh();
        box.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => Refresh()));
    }

    /// <summary>Esconde o texto do TextBox e põe o adorno na camada dele.</summary>
    public static MemoHighlightAdorner Attach(TextBox box, Func<IReadOnlyList<HighlightSpan>> computeSpans)
    {
        ArgumentNullException.ThrowIfNull(box);
        var adorner = new MemoHighlightAdorner(box, computeSpans);
        AdornerLayer.GetAdornerLayer(box)!.Add(adorner);
        adorner.HideBoxText();
        return adorner;
    }

    /// <summary>Depois de ApplyFont (que muda o Foreground), o texto do TextBox volta a ficar transparente.</summary>
    public void HideBoxText()
    {
        _box.Foreground = Brushes.Transparent;
        _box.CaretBrush = SystemColors.WindowTextBrush;
        Refresh();
    }

    /// <summary>TagHighlight: redesenha depois do layout do TextBox.</summary>
    public void Refresh()
    {
        if (_pending)
        {
            return;
        }

        _pending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            _pending = false;
            InvalidateVisual();
        });
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        string text = _box.Text;
        if (text.Length == 0 || ContentRect() is not { } clip)
        {
            return;
        }

        var spans = _computeSpans();
        var kinds = new HighlightKind?[text.Length];
        foreach (var span in spans.Where(s => s.Kind != HighlightKind.Misspelled))
        {
            for (int i = span.Start; i < span.Start + span.Length && i < text.Length; i++)
            {
                kinds[i] = span.Kind;
            }
        }

        var typeface = new Typeface(_box.FontFamily, _box.FontStyle, _box.FontWeight, _box.FontStretch);
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var mode = TextOptions.GetTextFormattingMode(_box);
        drawingContext.PushClip(new RectangleGeometry(clip));

        // Cada linha é desenhada em trechos da mesma cor, a partir da posição do primeiro caractere.
        int lineStart = 0;
        while (lineStart <= text.Length)
        {
            int lineEnd = text.IndexOf("\r\n", lineStart, StringComparison.Ordinal);
            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }

            int runStart = lineStart;
            while (runStart < lineEnd)
            {
                var kind = kinds[runStart];
                int runEnd = runStart + 1;
                while (runEnd < lineEnd && kinds[runEnd] == kind)
                {
                    runEnd++;
                }

                var origin = _box.GetRectFromCharacterIndex(runStart);
                if (!origin.IsEmpty && origin.Bottom >= clip.Top && origin.Top <= clip.Bottom)
                {
                    var ft = new FormattedText(text[runStart..runEnd], CultureInfo.CurrentUICulture, _box.FlowDirection,
                        typeface, _box.FontSize, BrushFor(kind), null, mode, pixelsPerDip);
                    drawingContext.DrawText(ft, origin.TopLeft);
                }

                runStart = runEnd;
            }

            lineStart = lineEnd + 2;
        }

        // SetReUnderline: CFU_UNDERLINEWAVE.
        var pen = new Pen(Brushes.Red, 1);
        pen.Freeze();
        double baseline = new FormattedText("x", CultureInfo.CurrentUICulture, _box.FlowDirection, typeface, _box.FontSize,
            Brushes.Black, null, mode, pixelsPerDip).Baseline;
        foreach (var span in spans.Where(s => s.Kind == HighlightKind.Misspelled && s.Length > 0))
        {
            var first = _box.GetRectFromCharacterIndex(span.Start);
            var last = _box.GetRectFromCharacterIndex(span.Start + span.Length);
            if (first.IsEmpty || last.IsEmpty || last.Right <= first.Left)
            {
                continue;
            }

            DrawWave(drawingContext, pen, Math.Round(first.Left), Math.Round(last.Left), Math.Round(first.Top + baseline) + 1.5);
        }

        drawingContext.Pop();
    }

    private static SolidColorBrush BrushFor(HighlightKind? kind) => kind switch
    {
        HighlightKind.Tag => SystemColors.GrayTextBrush,
        HighlightKind.SelectedKaraokeTag => Brushes.Red,
        _ => SystemColors.WindowTextBrush,
    };

    /// <summary>Zigue-zague de 2 px de altura e 4 px de período.</summary>
    private static void DrawWave(DrawingContext dc, Pen pen, double x1, double x2, double y)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(x1, y), false, false);
            bool down = true;
            for (double x = x1 + 2; x <= x2; x += 2)
            {
                ctx.LineTo(new Point(x, down ? y + 2 : y), true, false);
                down = !down;
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    /// <summary>A área do texto (o ScrollContentPresenter), sem as barras de rolagem e as bordas.</summary>
    private Rect? ContentRect()
    {
        if (_box.Template?.FindName("PART_ContentHost", _box) is not ScrollViewer host
            || host.Template?.FindName("PART_ScrollContentPresenter", host) is not FrameworkElement presenter
            || !presenter.IsDescendantOf(_box))
        {
            return null;
        }

        return presenter.TransformToAncestor(_box).TransformBounds(new Rect(0, 0, presenter.ActualWidth, presenter.ActualHeight));
    }
}
