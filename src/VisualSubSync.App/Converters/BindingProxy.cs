using System.Windows;

namespace VisualSubSync.App.Converters;

/// <summary>
/// Dá acesso ao DataContext da janela em elementos fora da árvore visual, como colunas do DataGrid.
/// </summary>
public sealed class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
