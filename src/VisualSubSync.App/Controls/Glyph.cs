using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VisualSubSync.App.Controls;

/// <summary>
/// Glifo de TSpeedButton: a imagem normal e, quando desabilitado, a segunda metade do bitmap
/// (NumGlyphs = 2) ou a versão em relevo cinza que a VCL gera. Os arquivos ficam em Assets/Glyphs
/// com o sufixo "_Disabled".
/// </summary>
public sealed class Glyph : Image
{
    public static readonly DependencyProperty NameKeyProperty = DependencyProperty.Register(
        nameof(NameKey), typeof(string), typeof(Glyph), new PropertyMetadata(null, (d, _) => ((Glyph)d).Update()));

    static Glyph()
    {
        IsEnabledProperty.OverrideMetadata(typeof(Glyph), new UIPropertyMetadata((d, _) => ((Glyph)d).Update()));
    }

    public Glyph()
    {
        Stretch = Stretch.None;
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    /// <summary>Nome do botão sem o prefixo "btt", por exemplo "Play".</summary>
    public string? NameKey
    {
        get => (string?)GetValue(NameKeyProperty);
        set => SetValue(NameKeyProperty, value);
    }

    private void Update()
    {
        if (NameKey is not { Length: > 0 } key)
        {
            Source = null;
            return;
        }

        string file = IsEnabled ? key : key + "_Disabled";
        Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Glyphs/{file}.png"));
    }
}
