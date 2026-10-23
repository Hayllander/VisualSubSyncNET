using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using VisualSubSync.App.Converters;
using VisualSubSync.App.ViewModels;
using VisualSubSync.Core.Settings;

namespace VisualSubSync.App.Views;

/// <summary>
/// Colunas da lista: as extras do plugin geral (InitVTVExtraColumns), o menu do cabeçalho
/// (SubListHeaderPopupMenu, pmiToggleColumn, ShowColumn) e o menu JS Tools.
/// </summary>
public partial class MainWindow
{
    /// <summary>Posição de cada coluna na lista do original (ColumnsWidth e os índices do plugin).</summary>
    private readonly Dictionary<DataGridColumn, int> _columnIniIndex = [];
    private readonly List<DataGridColumn> _extraColumns = [];
    private List<string> _hiddenByUserCoreColumns = [];
    private ContextMenu? _headerMenu;

    private DataGridColumn StyleColumn => vtvSubsList.Columns[3];

    /// <summary>
    /// AutoFitSubsListColumns: Index e Start até Style, e as colunas depois do VO, ficam com a largura do
    /// conteúdo; Text e VO não mudam. A grade só mede as linhas criadas, isto é, as visíveis.
    /// </summary>
    private void AutoFitSubsListColumns()
    {
        var columns = _columnIniIndex.Where(c => c.Value != TextColumnIndex && c.Value != VoColumnIndex).Select(c => c.Key).ToList();
        foreach (var column in columns)
        {
            column.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
        }

        // Fixa a largura medida, para que o conteúdo novo não a mude sozinho (Column.Width do VTV).
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            foreach (var column in columns)
            {
                if (column.ActualWidth > 0)
                {
                    column.Width = Math.Max(column.MinWidth, Math.Round(column.ActualWidth));
                }
            }
        });
    }

    private const int TextColumnIndex = 4, VoColumnIndex = 5;

    private void InitializeColumns(VssSettings settings)
    {
        for (int i = 0; i < vtvSubsList.Columns.Count; i++)
        {
            _columnIniIndex[vtvSubsList.Columns[i]] = i;
        }

        foreach (var info in _viewModel.ExtraColumns)
        {
            var column = new DataGridTextColumn
            {
                Header = info.Title,
                MinWidth = info.MinWidth,
                Width = Math.Max(50, info.MinWidth),
                Binding = ExtraBinding(info.Index, background: false),
            };
            if (info.BackgroundColorized)
            {
                // BeforeCellPaint pinta a cor e a seleção não a mistura (SelectionBlendFactor 2);
                // PaintText força clWindowText.
                var style = new Style(typeof(DataGridCell), (Style)FindResource("SubListCell"));
                style.Setters.Add(new Setter(BackgroundProperty, ExtraBinding(info.Index, background: true)));
                var selected = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
                selected.Setters.Add(new Setter(BackgroundProperty, ExtraBinding(info.Index, background: true)));
                selected.Setters.Add(new Setter(ForegroundProperty, SystemColors.WindowTextBrush));
                style.Triggers.Add(selected);
                style.Setters.Add(new Setter(ForegroundProperty, SystemColors.WindowTextBrush));
                column.CellStyle = style;
            }

            vtvSubsList.Columns.Add(column);
            _columnIniIndex[column] = info.Index;
            _extraColumns.Add(column);
        }

        // LoadColumnsSettings: escondidas pelo usuário; as extras só aparecem se estiverem em VisibleExtraColumns.
        _hiddenByUserCoreColumns = [.. settings.HiddenByUserCoreColumns];
        foreach (var column in vtvSubsList.Columns)
        {
            bool visible = !_hiddenByUserCoreColumns.Contains(HeaderText(column));
            if (_extraColumns.Contains(column))
            {
                visible &= settings.VisibleExtraColumns.Contains(HeaderText(column));
            }
            else if (column == StyleColumn)
            {
                visible &= _viewModel.ShowStyleColumn;
            }
            else if (column == VOColumn)
            {
                // Coluna VO: coVisible fora; só TurnOnVO a mostra.
                visible = false;
            }

            column.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        _headerMenu = new ContextMenu();
        foreach (var column in vtvSubsList.Columns)
        {
            var item = new MenuItem { Header = HeaderText(column), Tag = column };
            item.Click += (_, _) => ShowColumn(column, column.Visibility != Visibility.Visible, fromUser: true);
            _headerMenu.Items.Add(item);
        }

        _headerMenu.Opened += (_, _) => UpdateHeaderMenu();
        var headerStyle = new Style(typeof(DataGridColumnHeader), (Style)FindResource("SubListHeader"));
        headerStyle.Setters.Add(new Setter(ContextMenuProperty, _headerMenu));
        vtvSubsList.ColumnHeaderStyle = headerStyle;

        // MenuItemJSTools: um item por ação [js], sem o prefixo.
        foreach (var action in _viewModel.JsActions)
        {
            MenuItemJSTools.Items.Add(new MenuItem { Header = action.Caption, Tag = action.Name, Command = _viewModel.ExecuteJsActionCommand, CommandParameter = action.Name });
        }
    }

    private MultiBinding ExtraBinding(int columnIndex, bool background)
    {
        var binding = new MultiBinding { Converter = new ExtraColumnConverter(_viewModel, columnIndex, background) };
        binding.Bindings.Add(new Binding());
        binding.Bindings.Add(new Binding(nameof(SubtitleItemViewModel.Revision)));
        return binding;
    }

    private static string HeaderText(DataGridColumn column) => column.Header as string ?? string.Empty;

    /// <summary>
    /// ShowColumn: pelo usuário, a escolha vale até ele mudar (HiddenByUserCoreColumns); pelo programa
    /// (a coluna Style), a coluna só aparece se o usuário não a escondeu.
    /// </summary>
    private void ShowColumn(DataGridColumn column, bool show, bool fromUser)
    {
        string name = HeaderText(column);
        bool hiddenByUser = _hiddenByUserCoreColumns.Contains(name);
        if (fromUser)
        {
            column.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                _hiddenByUserCoreColumns.Remove(name);
            }
            else if (!hiddenByUser)
            {
                _hiddenByUserCoreColumns.Add(name);
            }
        }
        else
        {
            column.Visibility = show && !hiddenByUser ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateHeaderMenu()
    {
        foreach (var item in _headerMenu!.Items.OfType<MenuItem>())
        {
            item.IsChecked = ((DataGridColumn)item.Tag).Visibility == Visibility.Visible;
        }
    }

    /// <summary>SaveSettings: as extras à mostra e as escondidas pelo usuário.</summary>
    private void StoreColumnSettings(VssSettings settings)
    {
        settings.VisibleExtraColumns = [.. _extraColumns.Where(c => c.Visibility == Visibility.Visible).Select(HeaderText)];
        settings.HiddenByUserCoreColumns = [.. _hiddenByUserCoreColumns];
    }
}
