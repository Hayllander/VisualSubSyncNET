using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VisualSubSync.App.ViewModels;

namespace VisualSubSync.App.Views;

/// <summary>
/// TSuggestionForm (SuggestionFormUnit.pas): as sugestões recebidas pelo servidor web. O duplo clique
/// seleciona a legenda com os mesmos tempos.
/// </summary>
public partial class SuggestionWindow : Window
{
    private readonly MainViewModel _viewModel;

    public SuggestionWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        ApplyFontNames();
        viewModel.PreferencesApplied += (_, e) => { if (e.FontsChanged) ApplyFontNames(); };
    }

    /// <summary>ApplyFontSettings assigns only Font.Name to Suggestions in the 2013 source.</summary>
    private void ApplyFontNames()
    {
        var family = new FontFamily(_viewModel.Settings.SubListFont.Name);
        vtvSuggestionsLst.FontFamily = family;
        MemoSuggPreview.FontFamily = family;
    }

    private SuggestionItem? _focused;

    /// <summary>FocusedNode: a última sugestão que entrou na seleção, enquanto ela estiver selecionada.</summary>
    private SuggestionItem? Focused =>
        _focused is not null && vtvSuggestionsLst.SelectedItems.Contains(_focused) ? _focused
            : vtvSuggestionsLst.SelectedItem as SuggestionItem;

    /// <summary>vtvSuggestionsLstFocusChanged: o texto da sugestão focada vai para o memo.</summary>
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[^1] is SuggestionItem added)
        {
            _focused = added;
        }

        MemoSuggPreview.Text = Focused is { } item ? item.Text : string.Empty;
    }

    /// <summary>vtvSuggestionsLstDblClick: MainForm.SelectNodeFromTimestamps.</summary>
    private void OnSuggestionDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Focused is { } item)
        {
            _viewModel.SelectSubtitleFromTimestamps(item.StartMs, item.StopMs);
        }
    }

    /// <summary>pmiClearSelectedClick.</summary>
    private void OnClearSelected(object sender, RoutedEventArgs e)
    {
        var next = _viewModel.ClearSelectedSuggestions([.. vtvSuggestionsLst.SelectedItems.Cast<SuggestionItem>()]);
        vtvSuggestionsLst.SelectedItems.Clear();
        if (next is not null)
        {
            vtvSuggestionsLst.SelectedItem = next;
        }
    }

    /// <summary>pmiClearAllClick.</summary>
    private void OnClearAll(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearAllSuggestions();
        vtvSuggestionsLst.SelectedItems.Clear();
        MemoSuggPreview.Text = string.Empty;
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
