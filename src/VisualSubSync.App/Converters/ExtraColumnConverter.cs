using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VisualSubSync.App.ViewModels;

namespace VisualSubSync.App.Converters;

/// <summary>
/// Texto ou cor de fundo de uma coluna extra do plugin geral. Recebe a linha e a sua
/// <see cref="SubtitleItemViewModel.Revision"/>, para recalcular quando a legenda muda.
/// </summary>
public sealed class ExtraColumnConverter(MainViewModel viewModel, int column, bool background) : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0 || values[0] is not SubtitleItemViewModel item)
        {
            return background ? DependencyProperty.UnsetValue : string.Empty;
        }

        if (!background)
        {
            return viewModel.ExtraColumnText(item, column);
        }

        if (viewModel.ExtraColumnColor(item, column) is not int c)
        {
            return DependencyProperty.UnsetValue;
        }

        var brush = new SolidColorBrush(Color.FromRgb((byte)(c & 0xFF), (byte)((c >> 8) & 0xFF), (byte)((c >> 16) & 0xFF)));
        brush.Freeze();
        return brush;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
