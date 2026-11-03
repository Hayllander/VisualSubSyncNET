using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VisualSubSync.App.ViewModels;

namespace VisualSubSync.App.Views;

/// <summary>
/// TErrorReportForm (ErrorReportFormUnit.pas): os erros encontrados pelos plugins. Focar ou dar duplo
/// clique num erro seleciona a legenda; o menu corrige, seleciona e limpa erros.
/// </summary>
public partial class ErrorReportWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ErrorReportWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private ErrorItemViewModel? _focused;

    /// <summary>FocusedNode: o último erro que entrou na seleção, enquanto ele estiver na lista.</summary>
    private ErrorItemViewModel? Focused => _focused is not null && _viewModel.Errors.Contains(_focused) ? _focused : null;

    private List<ErrorItemViewModel> SelectedInOrder() =>
        [.. _viewModel.Errors.Where(vtvErrorList.SelectedItems.Contains)];

    /// <summary>vtvErrorListFocusChanged: MainForm.SelectNode.</summary>
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[^1] is ErrorItemViewModel item)
        {
            _focused = item;
            if (!_restoringSelection)
            {
                _viewModel.SelectErrorSubtitle(item);
            }
        }
    }

    /// <summary>vtvErrorListDblClick.</summary>
    private void OnErrorDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Focused is { } item)
        {
            _viewModel.SelectErrorSubtitle(item);
        }
    }

    /// <summary>ErrorListPopupMenuPopup.</summary>
    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var selected = SelectedInOrder();
        bool canFix = selected.Any(s => _viewModel.CanFixError(s.Error.PluginFileName));
        pmiFixError.IsEnabled = canFix;
        pmiFixAllXXX.IsEnabled = canFix && selected.Count == 1;
        pmiFixAllYYY.IsEnabled = canFix && selected.Count == 1 && Focused is { } f && f.Error.ErrorMessage.Length > 0;
        pmiSelectAllXXX.IsEnabled = Focused is not null;
        pmiSelectAllYYY.IsEnabled = Focused is not null;
        // Itens do Enhanced, que ele deixa sempre ativos; aqui dependem de haver seleção ou foco.
        miClearAndSkip.IsEnabled = selected.Count > 0;
        pmiClearAllYYY.IsEnabled = Focused is not null;
        pmiClearAllXXX.IsEnabled = Focused is not null;
    }

    /// <summary>FixSelectedErrors (pmiFixError e ActionFixSelectedErrors).</summary>
    public void FixSelectedErrors()
    {
        var selected = SelectedInOrder();
        if (selected.Count > 0)
        {
            Fix(selected);
        }
    }

    /// <summary>Enquanto a seleção é refeita depois de uma correção, a legenda da janela principal não muda.</summary>
    private bool _restoringSelection;

    /// <summary>
    /// FixErrorInList. A lista é refeita depois da correção (melhoria de 27/10/2026). Como no Enhanced, a
    /// janela principal continua na legenda em que se estava trabalhando: o foco volta para um erro que
    /// ainda exista nessa legenda ou, se não houver, para o que ficou na posição do erro focado, sem
    /// mudar a legenda selecionada.
    /// </summary>
    private void Fix(List<ErrorItemViewModel> errors)
    {
        var focused = Focused ?? (errors.Count > 0 ? errors[0] : null);
        int position = focused is null ? -1 : _viewModel.Errors.IndexOf(focused);
        _viewModel.FixErrors(errors);
        _restoringSelection = true;
        try
        {
            vtvErrorList.SelectedItems.Clear();
            _focused = null;
            var list = _viewModel.Errors;
            var target = focused is null ? null : list.FirstOrDefault(x => ReferenceEquals(x.Error.Subtitle, focused.Error.Subtitle));
            if (target is null && position >= 0 && list.Count > 0)
            {
                target = list[Math.Min(position, list.Count - 1)];
            }

            if (target is not null)
            {
                SelectAndFocus(target);
            }
        }
        finally
        {
            _restoringSelection = false;
        }
    }

    private void OnFixSelected(object sender, RoutedEventArgs e) => FixSelectedErrors();

    /// <summary>pmiFixAllXXXClick: todos os erros do mesmo plugin.</summary>
    private void OnFixAllSameType(object sender, RoutedEventArgs e)
    {
        if (Focused is { } focused)
        {
            Fix([.. _viewModel.Errors.Where(x => x.Error.PluginName == focused.Error.PluginName)]);
        }
    }

    /// <summary>pmiFixAllYYYClick: do mesmo plugin e com a mesma mensagem.</summary>
    private void OnFixAllSameMessage(object sender, RoutedEventArgs e)
    {
        if (Focused is { } focused)
        {
            Fix([.. _viewModel.Errors.Where(x => SameMessage(x, focused))]);
        }
    }

    private static bool SameMessage(ErrorItemViewModel a, ErrorItemViewModel b) =>
        a.Error.PluginName == b.Error.PluginName && a.Error.ErrorMessage == b.Error.ErrorMessage;

    /// <summary>pmiSelectAllXXXClick: seleciona na lista principal as legendas desses erros.</summary>
    private void OnSelectAllSameType(object sender, RoutedEventArgs e)
    {
        if (Focused is { } focused)
        {
            _viewModel.SelectErrorSubtitles(_viewModel.Errors.Where(x => x.Error.PluginName == focused.Error.PluginName));
        }
    }

    private void OnSelectAllSameMessage(object sender, RoutedEventArgs e)
    {
        if (Focused is { } focused)
        {
            _viewModel.SelectErrorSubtitles(_viewModel.Errors.Where(x => SameMessage(x, focused)));
        }
    }

    /// <summary>
    /// miClearClick: apaga os selecionados; o foco vai para o primeiro não selecionado a partir do
    /// primeiro selecionado ou, se não houver, para o anterior a ele.
    /// </summary>
    private void OnClear(object sender, RoutedEventArgs e) => ClearSelected();

    /// <summary>
    /// miClearAndSkipClick (Enhanced): o texto das legendas selecionadas vai para o projeto e os erros
    /// delas somem também nas próximas verificações; depois, como Clear.
    /// </summary>
    private void OnClearAndForget(object sender, RoutedEventArgs e)
    {
        var selected = SelectedInOrder();
        if (selected.Count > 0)
        {
            _viewModel.ForgetErrors(selected);
            ClearSelected();
        }
    }

    /// <summary>pmiClearAllYYYClick (Enhanced): fica só com os erros do mesmo plugin e mesma mensagem.</summary>
    private void OnClearDifferentMessage(object sender, RoutedEventArgs e)
    {
        if (Focused is { } focused)
        {
            RemoveErrors(x => !SameMessage(x, focused));
        }
    }

    /// <summary>pmiClearAllXXXClick (Enhanced): tira os erros do mesmo plugin e mesma mensagem.</summary>
    private void OnClearSameMessage(object sender, RoutedEventArgs e)
    {
        if (Focused is { } focused)
        {
            RemoveErrors(x => SameMessage(x, focused));
        }
    }

    private void RemoveErrors(Func<ErrorItemViewModel, bool> match)
    {
        var errors = _viewModel.Errors;
        for (int i = errors.Count - 1; i >= 0; i--)
        {
            if (match(errors[i]))
            {
                errors.RemoveAt(i);
            }
        }

        _viewModel.UpdateErrorCountStatus();
    }

    private void ClearSelected()
    {
        var errors = _viewModel.Errors;
        var selected = SelectedInOrder();
        if (selected.Count == 0)
        {
            return;
        }

        int first = errors.IndexOf(selected[0]);
        var cursor = errors.Skip(first).FirstOrDefault(x => !selected.Contains(x));
        if (cursor is null && first > 0)
        {
            cursor = errors[first - 1];
        }

        foreach (var item in selected)
        {
            errors.Remove(item);
        }

        vtvErrorList.SelectedItems.Clear();
        if (cursor is not null)
        {
            SelectAndFocus(cursor);
        }

        _viewModel.UpdateErrorCountStatus();
    }

    /// <summary>pmiClearAllClick / miClearAllClick: Clear.</summary>
    private void OnClearAll(object sender, RoutedEventArgs e)
    {
        _viewModel.Errors.Clear();
        _viewModel.UpdateErrorCountStatus();
    }

    private void OnExportToHtml(object sender, RoutedEventArgs e) => _viewModel.ExportErrorsToHtml();

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    /// <summary>GoToNextError: o próximo erro passa a ser o único selecionado e o focado.</summary>
    public void GoToNextError() => MoveFocus(1);

    /// <summary>GoToPreviousError.</summary>
    public void GoToPreviousError() => MoveFocus(-1);

    private void MoveFocus(int delta)
    {
        if (Focused is not { } focused)
        {
            return;
        }

        int index = _viewModel.Errors.IndexOf(focused) + delta;
        if (index >= 0 && index < _viewModel.Errors.Count)
        {
            vtvErrorList.SelectedItems.Remove(focused);
            SelectAndFocus(_viewModel.Errors[index]);
        }
    }

    private void SelectAndFocus(ErrorItemViewModel item)
    {
        vtvErrorList.SelectedItems.Add(item);
        vtvErrorList.ScrollIntoView(item);
        if (IsKeyboardFocusWithin && vtvErrorList.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container)
        {
            container.Focus();
        }
    }

    /// <summary>
    /// FormKeyDown (KeyPreview): Esc fecha. Del faz Clear &amp; Forget nos erros selecionados (atalho novo do
    /// VisualSubSyncNET, pedido pelo usuário em 29/10/2026).
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
        {
            OnClearAndForget(sender, e);
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
