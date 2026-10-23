using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VisualSubSync.App.ViewModels;
using VisualSubSync.Core.Audio;

namespace VisualSubSync.App.Views;

/// <summary>
/// TSilentZoneForm (SilentZoneFormUnit.pas): lista os trechos silenciosos da onda com pelo menos a
/// duração pedida; o duplo clique seleciona o trecho na onda.
/// </summary>
public partial class SilentZoneWindow : Window
{
    private readonly MainViewModel _viewModel;

    public SilentZoneWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _viewModel = viewModel;
        // CloseProject: SilentZoneForm.Clear.
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Peaks) && viewModel.Peaks is null)
            {
                Clear();
            }
        };
    }

    /// <summary>Clear.</summary>
    public void Clear() => lbSilentZones.Items.Clear();

    /// <summary>UpdateData: DetectSilentZone(threshold, 100) e FillZone com a duração mínima.</summary>
    public void UpdateData()
    {
        Clear();
        if (_viewModel.Peaks is not { } peaks)
        {
            return;
        }

        int minDuration = udDuration.Position;
        foreach (var zone in SilentZones.Detect(peaks, udThreshold.Position))
        {
            if (zone.StopMs - zone.StartMs >= minDuration)
            {
                lbSilentZones.Items.Add(new ListBoxItem { Content = zone.Format(CultureInfo.CurrentCulture), Tag = zone });
            }
        }
    }

    /// <summary>bttUpdateClick.</summary>
    private void OnUpdate(object sender, RoutedEventArgs e) => UpdateData();

    /// <summary>lbSilentZonesDblClick: TMainForm.SetSelection.</summary>
    private void OnZoneDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (lbSilentZones.SelectedItem is ListBoxItem { Tag: SilentZone zone })
        {
            _viewModel.SetSelection(zone.StartMs, zone.StopMs);
        }
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
