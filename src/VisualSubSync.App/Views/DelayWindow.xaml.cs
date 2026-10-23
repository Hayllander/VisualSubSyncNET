using System.Windows;
using System.Windows.Input;
using VisualSubSync.App.Services;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.App.Views;

/// <summary>TDelayForm (DelayFormUnit.pas): valor, sentido, a que legendas e que tempos o atraso vale.</summary>
public partial class DelayWindow : Window
{
    public DelayWindow(DelayRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        InitializeComponent();
        if (request.SelectedOnly)
        {
            ApplySelected.IsChecked = true;
        }

        if (request.DelayMs is int ms)
        {
            meDelay.Text = TimeFormat.ToVss(ms);
        }
    }

    public DelayResult? Result { get; private set; }

    /// <summary>GetDelayInMs / GetDelayApplyToType / GetDelayShiftType.</summary>
    private void OnApply(object sender, RoutedEventArgs e)
    {
        int ms = TimeFormat.ParseVss(meDelay.Text);
        if (TypeNegative.IsChecked == true)
        {
            ms = -ms;
        }

        var applyTo = ApplySelected.IsChecked == true ? DelayApplyTo.Selected
            : ApplyFromCursor.IsChecked == true ? DelayApplyTo.FromCursor
            : DelayApplyTo.All;
        var shift = ShiftStart.IsChecked == true ? DelayShiftType.StartTimeOnly
            : ShiftStop.IsChecked == true ? DelayShiftType.StopTimeOnly
            : DelayShiftType.BothTimes;
        Result = new DelayResult(ms, applyTo, shift);
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>FormKeyUp (KeyPreview): Esc fecha.</summary>
    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
