using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using VisualSubSync.App.ViewModels;

namespace VisualSubSync.App.Views;

/// <summary>A coluna VO, o MemoSubtitleVO e o SplitterSubtitleVO (TurnOnVO/TurnOffVO e UpdateMemoVO).</summary>
public partial class MainWindow
{
    private void InitializeVo(MainViewModel viewModel)
    {
        var binding = new MultiBinding { Converter = new VoColumnConverter(viewModel) };
        binding.Bindings.Add(new Binding());
        binding.Bindings.Add(new Binding(nameof(SubtitleItemViewModel.Revision)));
        binding.Bindings.Add(new Binding("DataContext." + nameof(MainViewModel.VoRevision)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Window), 1) });
        VOColumn.Binding = binding;
        MemoSubtitleVO.Document.PagePadding = new Thickness(0);
        MemoSubtitleVO.Width = Math.Max(30, viewModel.Settings.MemoSubtitleVOWidth);
        viewModel.PropertyChanged += OnVoPropertyChanged;
    }

    private void OnVoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsVoVisible))
        {
            // TurnOnVO / TurnOffVO.
            var visibility = _viewModel.IsVoVisible ? Visibility.Visible : Visibility.Collapsed;
            MemoSubtitleVO.Visibility = visibility;
            SplitterSubtitleVO.Visibility = visibility;
            ShowColumn(VOColumn, _viewModel.IsVoVisible, fromUser: false);
        }
        else if (e.PropertyName == nameof(MainViewModel.MemoVo))
        {
            ShowMemoVo();
        }
    }

    /// <summary>UpdateMemoVO: antes e depois em clGrayText, a parte da legenda em clWindowText.</summary>
    private void ShowMemoVo()
    {
        var memo = _viewModel.MemoVo;
        string text = memo.Text;
        int first = Math.Clamp(memo.CurrentStart, 0, text.Length);
        int last = Math.Clamp(memo.CurrentStop, first, text.Length);
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        AddRuns(paragraph, text[..first], SystemColors.GrayTextBrush);
        AddRuns(paragraph, text[first..last], SystemColors.WindowTextBrush);
        AddRuns(paragraph, text[last..], SystemColors.GrayTextBrush);
        MemoSubtitleVO.Document.Blocks.Clear();
        MemoSubtitleVO.Document.Blocks.Add(paragraph);
        MemoSubtitleVO.ScrollToHome();
    }

    private static void AddRuns(Paragraph paragraph, string text, System.Windows.Media.Brush brush)
    {
        string[] lines = text.Split("\r\n");
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            if (lines[i].Length > 0)
            {
                paragraph.Inlines.Add(new Run(lines[i]) { Foreground = brush });
            }
        }
    }

    /// <summary>SplitterSubtitleVO (alRight, MinSize 30): arrastar para a esquerda alarga o MemoSubtitleVO.</summary>
    private void SplitterSubtitleVO_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double max = Math.Max(30, EditorPanel.ActualWidth - MemoLinesCounter.ActualWidth - SplitterSubtitleVO.ActualWidth - 30);
        MemoSubtitleVO.Width = Math.Clamp(MemoSubtitleVO.Width - e.HorizontalChange, 30, max);
    }

    /// <summary>vtvSubsListGetText na coluna VO: refeito quando a legenda ou o VO mudam.</summary>
    private sealed class VoColumnConverter(MainViewModel viewModel) : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
            values.Length > 0 && values[0] is SubtitleItemViewModel item ? viewModel.GetVoColumnText(item) : string.Empty;

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
