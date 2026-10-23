using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VisualSubSync.App.ViewModels;

namespace VisualSubSync.App.Views;

/// <summary>
/// TFindForm (FindFormUnit.pas): janela sem modo, criada uma vez; fechar só a esconde, guardando o
/// histórico da busca e as opções.
/// </summary>
public partial class FindWindow : Window
{
    private readonly MainViewModel _viewModel;

    public FindWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        chkWholeWord.IsEnabled = !viewModel.FindUseRegExp;
        SetMemoMode(viewModel.IsFindInTextPipe);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsFindInTextPipe))
            {
                SetMemoMode(viewModel.IsFindInTextPipe);
            }
        };
        Activated += (_, _) => cbFind.Focus();
    }

    /// <summary>SetMemoMode: no text pipe, "Find text" e sem "Show matching sub only".</summary>
    private void SetMemoMode(bool memoMode)
    {
        chkMatchingOnly.IsEnabled = !memoMode;
        Title = memoMode ? "Find text" : "Find subtitle";
    }

    /// <summary>FormClose: a lista volta a mostrar todas as legendas; a janela só é escondida.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnClosing(e);
        e.Cancel = true;
        if (chkMatchingOnly.IsEnabled)
        {
            _viewModel.ShowMatchingSubOnly(displayAll: true);
        }

        Hide();
        Owner?.Activate();
    }

    /// <summary>bttCloseClick.</summary>
    private void OnClose(object sender, RoutedEventArgs e) => Close();

    /// <summary>FormKeyUp (KeyPreview): Esc fecha.</summary>
    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    /// <summary>cbFindKeyDown: Enter busca, Esc fecha.</summary>
    private void OnComboKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            e.Handled = true;
            OnFind(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    /// <summary>bttFindClick: guarda o texto no histórico e busca a próxima.</summary>
    private void OnFind(object sender, RoutedEventArgs e)
    {
        Historize(cbFind);
        _viewModel.FindNextCommand.Execute(null);
        UpdateReplaceButtons();
    }

    /// <summary>cbFindChange.</summary>
    private void OnFindTextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.ResetFind();
        if (chkMatchingOnly.IsEnabled && chkMatchingOnly.IsChecked == true)
        {
            _viewModel.ShowMatchingSubOnly(displayAll: false);
        }

        UpdateReplaceButtons();
    }

    /// <summary>chkMatchingOnlyClick.</summary>
    private void OnMatchingOnlyClick(object sender, RoutedEventArgs e) =>
        _viewModel.ShowMatchingSubOnly(chkMatchingOnly.IsChecked != true);

    /// <summary>chkMatchCaseClick.</summary>
    private void OnMatchCaseClick(object sender, RoutedEventArgs e)
    {
        if (chkMatchingOnly.IsEnabled && chkMatchingOnly.IsChecked == true)
        {
            _viewModel.ShowMatchingSubOnly(displayAll: false);
        }
    }

    /// <summary>chkRegExpClick.</summary>
    private void OnRegExpClick(object sender, RoutedEventArgs e) => chkWholeWord.IsEnabled = chkRegExp.IsChecked != true;

    /// <summary>bttReplaceClick.</summary>
    private void OnReplace(object sender, RoutedEventArgs e)
    {
        _viewModel.ReplaceSelection();
        bttReplace.IsEnabled = false;
        bttReplaceFind.IsEnabled = false;
    }

    /// <summary>bttReplaceFindClick.</summary>
    private void OnReplaceFind(object sender, RoutedEventArgs e)
    {
        OnReplace(sender, e);
        OnFind(sender, e);
    }

    /// <summary>bttReplaceAllClick.</summary>
    private void OnReplaceAll(object sender, RoutedEventArgs e) => _viewModel.ReplaceAll();

    private void UpdateReplaceButtons()
    {
        bttReplace.IsEnabled = _viewModel.LastFindSucceeded;
        bttReplaceFind.IsEnabled = _viewModel.LastFindSucceeded;
    }

    /// <summary>HistorizeComboBox: o texto vai (ou volta) para o topo do histórico.</summary>
    private static void Historize(ComboBox combo)
    {
        string text = combo.Text;
        int index = combo.Items.IndexOf(text);
        if (index == -1)
        {
            combo.Items.Insert(0, text);
        }
        else if (index > 0)
        {
            combo.Items.RemoveAt(index);
            combo.Items.Insert(0, text);
            combo.Text = text;
        }
    }
}
