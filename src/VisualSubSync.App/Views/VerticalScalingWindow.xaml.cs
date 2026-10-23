using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using VisualSubSync.App.ViewModels;

namespace VisualSubSync.App.Views;

/// <summary>
/// TVerticalScalingForm (VerticalScalingFormUnit.pas): mostra e muda a escala vertical da onda; criada
/// uma vez, ActionZoomVertical alterna entre mostrar e esconder.
/// </summary>
public partial class VerticalScalingWindow : Window
{
    private readonly MainViewModel _viewModel;

    public VerticalScalingWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _viewModel = viewModel;
        // FormShow: a barra vai para a escala atual (presa entre 50 e 800, o que pode mudar a escala).
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                ScalingTrackBar.Value = _viewModel.VerticalScaling;
                UpdateFromTrackBar();
            }
        };
    }

    /// <summary>bttResetClick.</summary>
    private void OnReset(object sender, RoutedEventArgs e) => ScalingTrackBar.Value = 100;

    /// <summary>ScalingTrackBarChange.</summary>
    private void OnScalingChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IsLoaded || IsVisible)
        {
            UpdateFromTrackBar();
        }
    }

    private void UpdateFromTrackBar()
    {
        int position = (int)Math.Round(ScalingTrackBar.Value, MidpointRounding.ToEven);
        _viewModel.VerticalScaling = position;
        GroupCaption.Text = string.Format(CultureInfo.CurrentCulture, " Scaling : {0}% ", position);
    }

    /// <summary>FormKeyDown (KeyPreview): Esc fecha.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    /// <summary>Close de um TForm não destruído: só esconde.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnClosing(e);
        e.Cancel = true;
        Hide();
        Owner?.Activate();
    }
}
