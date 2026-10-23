using System.Windows;
using System.Windows.Controls;

namespace VisualSubSync.App.Controls;

/// <summary>Conteúdo de TSpeedButton com glifo à esquerda e texto (Layout = blGlyphLeft).</summary>
public sealed class GlyphCaption : StackPanel
{
    public static readonly DependencyProperty NameKeyProperty = DependencyProperty.Register(
        nameof(NameKey), typeof(string), typeof(GlyphCaption), new PropertyMetadata(null, (d, e) => ((GlyphCaption)d)._glyph.NameKey = (string?)e.NewValue));

    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(GlyphCaption), new PropertyMetadata(string.Empty, (d, e) => ((GlyphCaption)d)._text.Text = (string)e.NewValue));

    private readonly Glyph _glyph = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _text = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };

    public GlyphCaption()
    {
        Orientation = Orientation.Horizontal;
        HorizontalAlignment = HorizontalAlignment.Center;
        Children.Add(_glyph);
        Children.Add(_text);
    }

    public string? NameKey
    {
        get => (string?)GetValue(NameKeyProperty);
        set => SetValue(NameKeyProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }
}
