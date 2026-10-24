using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VisualSubSync.App.Controls;
using VisualSubSync.App.ViewModels;
using VisualSubSync.Core.Settings;
using VisualSubSync.Core.Spelling;
using VisualSubSync.Core.TextPipe;

namespace VisualSubSync.App.Views;

/// <summary>
/// TMainForm: o que é só de apresentação (fontes, troca lista/editor, tamanhos e posição da
/// janela) fica aqui; o resto está no <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Title = viewModel.WindowTitle;
        viewModel.Waveform = WAVDisplayer;
        viewModel.TextPipe = new RichTextPipe(MemoTextPipe);
        InitializeColumns(viewModel.Settings);
        InitializeVo(viewModel);
        // OnRangeStartDblClick / OnRangeStopDblClick: o plugin geral.
        WAVDisplayer.RangeEdgeDoubleClicked += (_, e) => viewModel.OnWaveformRangeEdgeDoubleClick(e.Index, e.IsStart);
        // OnSelectedRange / OnSelectedRangeChange / OnSelectedRangeChanged do TWAVDisplayer.
        WAVDisplayer.RangeSelected += (_, e) =>
        {
            viewModel.OnWaveformRangeSelected(e.Index, e.IsDynamic);
            vtvSubsList.ScrollIntoView(viewModel.SelectedSubtitle);
            if (MemoSubtitleText.IsEnabled && !MemoSubtitleText.IsFocused)
            {
                MemoSubtitleText.Focus();
            }
        };
        // OnAutoScrollChange: rolar a vista à mão desmarca o "Auto-scroll WAV display".
        WAVDisplayer.UserScrolled += (_, _) => viewModel.AutoScrollWavDisplay = false;
        WAVDisplayer.SelectedRangeChanging += (_, e) => viewModel.OnWaveformRangeChanging(e.Change);
        WAVDisplayer.SelectedRangeChanged += (_, e) => viewModel.OnWaveformRangeChanged(e.Change);
        // OnKaraokeChanged / OnSelectedKaraokeRange.
        WAVDisplayer.KaraokeChanging += (_, e) => viewModel.OnKaraokeChanging(e);
        WAVDisplayer.KaraokeChanged += (_, e) => viewModel.OnKaraokeChanged(e);
        WAVDisplayer.KaraokeRangeSelected += (_, index) => viewModel.OnKaraokeRangeSelected(index);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        InitializeHotkeys(viewModel);

        // VideoRenderer.SetDisplayWindow(PanelVideo.Handle): o motor só é criado quando o HWND existe.
        Video.HandleCreated += (_, _) => viewModel.AttachMedia(Video.VideoHandle);
        InitializeDetachedVideo(viewModel);
        SplitterWAVDisplayVideo.DragCompleted += (_, _) => SaveVideoPanelSize();
        ApplySettings(viewModel.Settings);
        viewModel.PreferencesApplied += OnPreferencesApplied;
        viewModel.AutoFitColumnsRequested += (_, _) => AutoFitSubsListColumns();
        FillRecentMenu();
        FillPresetsMenu();
        FillDictionaryMenu();
        InitializeMemoHighlight(viewModel);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CheckedPreset))
            {
                UpdatePresetsMenuCheck();
            }
            else if (e.PropertyName is nameof(MainViewModel.CheckedDictionaryIndex) or nameof(MainViewModel.IsDictionaryLoading))
            {
                UpdateDictionaryMenu();
            }
        };
        viewModel.RecentProjectsChanged += (_, _) => FillRecentMenu();
        ApplyTextPipeVisibility();
    }

    /// <summary>Largura do MemoTextPipe enquanto escondido (o TRichEdit guarda a sua).</summary>
    private double _textPipeWidth = 250;

    /// <summary>ActionShowHideTextPipeExecute: mostra/esconde o splitter e o MemoTextPipe.</summary>
    private void ApplyTextPipeVisibility()
    {
        bool showing = _viewModel.IsTextPipeVisible;
        if (!showing && TextPipeColumn.ActualWidth > 0)
        {
            _textPipeWidth = TextPipeColumn.ActualWidth;
        }

        MemoTextPipe.Visibility = showing ? Visibility.Visible : Visibility.Collapsed;
        SplitterMemoTextPipe.Visibility = MemoTextPipe.Visibility;
        TextPipeColumn.MinWidth = showing ? 50 : 0;
        TextPipeColumn.Width = new GridLength(showing ? _textPipeWidth : 0);
        TextPipeSplitterColumn.Width = new GridLength(showing ? 4 : 0);
    }

    /// <summary>ActionShowHideVideoExecute: mostra/esconde o splitter e o painel de vídeo (ou a janela destacada).</summary>
    private void ApplyVideoPanelVisibility()
    {
        bool detached = _viewModel.IsVideoDetached;
        PlaceVideoSurface(_viewModel.ShowingVideo, detached);
        bool showing = _viewModel.ShowingVideo && !detached;
        SplitterWAVDisplayVideo.Visibility = showing ? Visibility.Visible : Visibility.Collapsed;
        VideoSplitterColumn.Width = showing ? new GridLength(4) : new GridLength(0);
        VideoColumn.Width = showing ? new GridLength(ResolveVideoPanelWidth()) : new GridLength(0);
    }

    /// <summary>UpdateVideoRendererWindow: largura salva no projeto ou calculada pela proporção (16/9, 201 de reserva).</summary>
    private double ResolveVideoPanelWidth()
    {
        if (_viewModel.VideoPanelWidth > 0)
        {
            return _viewModel.VideoPanelWidth;
        }

        double height = PanelVideo.ActualHeight > 0 ? PanelVideo.ActualHeight : 223;
        return _viewModel.VideoAspectRatio > 0 ? Math.Round(_viewModel.VideoAspectRatio * height) : 201;
    }

    /// <summary>PanelVideoResize: guarda a largura arrastada no splitter para o projeto.</summary>
    private void SaveVideoPanelSize()
    {
        int width = (int)Math.Round(VideoColumn.ActualWidth);
        if (_viewModel.IsProjectOpen && width > 0)
        {
            _viewModel.SaveVideoPanelSize(width, (int)Math.Round(PanelVideo.ActualHeight));
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.WindowTitle):
                Title = _viewModel.WindowTitle;
                break;
            case nameof(MainViewModel.SelectedSubtitle) when _viewModel.SelectedSubtitle is { } item:
                vtvSubsList.ScrollIntoView(item);
                break;
            // ShowColumn(StyleColumn, ...): pelo programa, respeitando a escolha do usuário.
            case nameof(MainViewModel.ShowStyleColumn):
                ShowColumn(StyleColumn, _viewModel.ShowStyleColumn, fromUser: false);
                break;
            case nameof(MainViewModel.IsTimingMode):
                UpdateMenuShortcuts();
                break;
            case nameof(MainViewModel.IsTextPipeVisible):
                ApplyTextPipeVisibility();
                break;
            case nameof(MainViewModel.ShowingVideo):
                ApplyVideoPanelVisibility();
                break;
            // A proporção chega depois da mídia abrir; refaz a largura quando ela ainda não foi escolhida.
            case nameof(MainViewModel.VideoAspectRatio) when _viewModel.ShowingVideo && !_viewModel.IsVideoDetached && _viewModel.VideoPanelWidth == 0:
                ApplyVideoPanelVisibility();
                break;
        }
    }

    /// <summary>
    /// TMRUList.FillMenuItem: um item por projeto (o caminho como texto), um separador, "Clear list" e
    /// "Clear dead entries"; o submenu fica desabilitado sem projetos.
    /// </summary>
    private void FillRecentMenu()
    {
        var root = MenuItemOpenRecentRoot;
        root.Items.Clear();
        root.IsEnabled = _viewModel.RecentProjects.Count > 0;
        foreach (string path in _viewModel.RecentProjects)
        {
            // Um TextBlock no cabeçalho para que "_" no caminho não vire tecla de acesso.
            root.Items.Add(new MenuItem { Header = new TextBlock { Text = path }, Command = _viewModel.OpenRecentProjectCommand, CommandParameter = path });
        }

        root.Items.Add(new Separator());
        root.Items.Add(new MenuItem { Header = "Clear list", Command = _viewModel.ClearRecentProjectsCommand });
        root.Items.Add(new MenuItem { Header = "Clear dead entries", Command = _viewModel.ClearDeadRecentProjectsCommand });
    }

    /// <summary>FillPresetsMenu: um item por arquivo, um separador e "Browse...".</summary>
    private void FillPresetsMenu()
    {
        var root = MenuItemLoadPresets;
        root.Items.Clear();
        foreach (string name in MainViewModel.GetPresetFiles())
        {
            root.Items.Add(new MenuItem { Header = new TextBlock { Text = name }, Tag = name, Command = _viewModel.LoadPresetCommand, CommandParameter = name });
        }

        root.Items.Add(new Separator());
        root.Items.Add(new MenuItem { Header = "Browse...", Command = _viewModel.BrowsePresetsCommand });
        UpdatePresetsMenuCheck();
    }

    /// <summary>LoadPresetFile: marca o item com o nome do arquivo carregado.</summary>
    private void UpdatePresetsMenuCheck()
    {
        foreach (var item in MenuItemLoadPresets.Items.OfType<MenuItem>())
        {
            item.IsChecked = item.Tag is string name && name == _viewModel.CheckedPreset;
        }
    }

    private readonly List<MenuItem> _dictionaryMenuItems = [];

    /// <summary>FormCreate: um item de rádio por dicionário depois de "Live spell check" e do separador.</summary>
    private void FillDictionaryMenu()
    {
        var root = SubMenuItemSpellcheck;
        int insertAt = root.Items.IndexOf(MenuItemLiveSpellcheck) + 2;
        var dictionaries = _viewModel.SpellChecker.Dictionaries;
        for (int i = 0; i < dictionaries.Count; i++)
        {
            var item = new MenuItem { Header = new TextBlock { Text = dictionaries[i] }, Tag = i, Command = _viewModel.LoadDictionaryCommand, CommandParameter = i };
            _dictionaryMenuItems.Add(item);
            root.Items.Insert(insertAt + i, item);
        }

        if (dictionaries.Count == 0)
        {
            root.Items.Insert(insertAt, new MenuItem { Header = "No dictionnary", IsEnabled = false });
        }

        UpdateDictionaryMenu();
    }

    /// <summary>LoadDict / OnLoadDictionnaryTerminate: os itens ficam desabilitados durante a carga; o pedido fica marcado.</summary>
    private void UpdateDictionaryMenu()
    {
        foreach (var item in _dictionaryMenuItems)
        {
            item.IsEnabled = !_viewModel.IsDictionaryLoading;
            item.IsChecked = item.Tag is int index && index == _viewModel.CheckedDictionaryIndex;
        }
    }

    /// <summary>LoadSettings / ApplyFontSettings / FinishLoadSettings.</summary>
    private void ApplySettings(VssSettings settings)
    {
        // LoadFormPosition(MainForm); sem posição gravada, poDefault: o Windows escolhe onde abrir.
        if (settings.MainForm is { } p)
        {
            FormPlacement.Apply(this, p);
        }

        TopRow.Height = new GridLength(Math.Max(TopRow.MinHeight, settings.PanelTopHeight));
        BottomRow.Height = new GridLength(Math.Max(BottomRow.MinHeight, settings.PanelBottomHeight));

        ApplyFontSettings(settings);
        foreach (var (column, index) in _columnIniIndex)
        {
            if (index < settings.ColumnsWidth.Count && settings.ColumnsWidth[index] > 0)
            {
                column.Width = Math.Max(column.MinWidth, settings.ColumnsWidth[index]);
            }
        }

        // LoadColumnsSettings: Column.Position := StrToIntDef(..., MaxInt); empates ficam na ordem original.
        var ordered = _columnIniIndex
            .OrderBy(c => c.Value < settings.ColumnsPosition.Count ? settings.ColumnsPosition[c.Value] : int.MaxValue)
            .ThenBy(c => c.Value)
            .Select(c => c.Key)
            .ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].DisplayIndex = i;
        }

        ApplyMouseSettings(settings);
        if (settings.SwapSubtitlesList)
        {
            SwapSubList(swapSizeAlso: false);
        }
    }

    /// <summary>
    /// ApplyFontSettings: a fonte da lista (com a altura das linhas = TextHeight('Tj') * 1.1) e a do editor,
    /// do contador de linhas, do text pipe e do texto na forma de onda.
    /// </summary>
    private void ApplyFontSettings(VssSettings settings)
    {
        var listFont = settings.SubListFont;
        ApplyFont(vtvSubsList, listFont);
        double rowHeight = Math.Round(TextHeight(vtvSubsList.FontFamily, vtvSubsList.FontSize) * 1.1, MidpointRounding.ToEven);
        vtvSubsList.RowHeight = rowHeight;
        vtvSubsList.ColumnHeaderHeight = rowHeight;

        var textFont = settings.SubTextFont;
        ApplyFont(MemoSubtitleText, textFont);
        ApplyFont(MemoSubtitleVO, textFont);
        _memoHighlight?.HideBoxText();
        ApplyFont(MemoLinesCounter, textFont);
        ApplyFont(MemoTextPipe, textFont);
        WAVDisplayer.TextFontFamily = MemoSubtitleText.FontFamily;
        WAVDisplayer.TextFontSize = MemoSubtitleText.FontSize;
        WAVDisplayer.TextFontWeight = MemoSubtitleText.FontWeight;
        WAVDisplayer.TextFontStyle = MemoSubtitleText.FontStyle;
        MemoLinesCounter.Width = Math.Ceiling(TextWidth("000", MemoLinesCounter)) + (2 * 4);
    }

    private bool _swapped;

    /// <summary>
    /// SwapSubList: troca o editor e a lista de lugar. Trocada, a região de cima (controles e editor)
    /// fica com altura fixa e a lista ocupa o resto; ao desfazer, o editor volta para baixo com a
    /// altura fixa. Com <paramref name="swapSizeAlso"/> o editor mantém a altura que tinha.
    /// </summary>
    private void SwapSubList(bool swapSizeAlso)
    {
        double listHeight = vtvSubsList.ActualHeight;
        double editorHeight = EditorPanel.ActualHeight;
        double middleHeight = MiddleRow.ActualHeight;
        MiddleHost.Child = null;
        BottomHost.Child = null;
        if (_swapped)
        {
            MiddleHost.Child = vtvSubsList;
            BottomHost.Child = EditorPanel;
            MiddleRow.Height = new GridLength(1, GridUnitType.Star);
            if (swapSizeAlso)
            {
                BottomRow.Height = new GridLength(Math.Max(BottomRow.MinHeight, editorHeight));
            }
        }
        else
        {
            MiddleHost.Child = EditorPanel;
            BottomHost.Child = vtvSubsList;
            if (swapSizeAlso)
            {
                MiddleRow.Height = new GridLength(Math.Max(MiddleRow.MinHeight, middleHeight - listHeight + editorHeight));
                BottomRow.Height = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                // Na abertura (FinishLoadSettings) as alturas ainda não existem: fixa a de cima depois de medir.
                Loaded += (_, _) =>
                {
                    double middle = Math.Max(MiddleRow.MinHeight, MiddleRow.ActualHeight);
                    double bottom = BottomRow.ActualHeight;
                    MiddleRow.Height = new GridLength(middle);
                    BottomRow.Height = new GridLength(Math.Max(1, bottom), GridUnitType.Star);
                };
            }
        }

        _swapped = !_swapped;
    }

    /// <summary>ShowPreferences depois do OK: SwapSubList, SetShortcut, ApplyMouseSettings e ApplyFontSettings.</summary>
    private void OnPreferencesApplied(object? sender, PreferencesAppliedEventArgs e)
    {
        var settings = _viewModel.Settings;
        if (e.SwapChanged)
        {
            SwapSubList(swapSizeAlso: true);
        }

        UpdateMenuShortcuts();
        ApplyMouseSettings(settings);
        if (e.FontsChanged)
        {
            ApplyFontSettings(settings);
            AutoFitSubsListColumns();
        }
    }

    /// <summary>ApplyMouseSettings: as teclas da roda (o resto vem das ligações com Settings).</summary>
    private void ApplyMouseSettings(VssSettings settings)
    {
        WAVDisplayer.WheelTimeScroll = settings.MouseWheelTimeScrollModifier;
        WAVDisplayer.WheelVZoom = settings.MouseWheelVZoomModifier;
        WAVDisplayer.WheelHZoom = settings.MouseWheelHZoomModifier;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnClosing(e);
        if (!_viewModel.CanCloseWindow())
        {
            e.Cancel = true;
            return;
        }

        // Libera o motor antes de a janela nativa ser destruída (o HwndHost destrói o filho no fechamento).
        _viewModel.Dispose();
        SaveSettings(_viewModel.Settings);
    }

    /// <summary>SaveSettings: posição da janela, alturas dos painéis e larguras das colunas.</summary>
    private void SaveSettings(VssSettings settings)
    {
        _viewModel.StoreWindowPlacements();
        SaveDetachedVideoSettings(settings);
        var bounds = RestoreBounds.IsEmpty ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        int state = WindowState switch
        {
            WindowState.Maximized => 2,
            WindowState.Minimized => 1,
            _ => 0,
        };
        settings.MainForm = new WindowPlacement((int)bounds.Left, (int)bounds.Top, (int)bounds.Width, (int)bounds.Height, state);
        settings.PanelTopHeight = (int)Math.Round(TopRow.ActualHeight);
        settings.PanelBottomHeight = (int)Math.Round(BottomRow.ActualHeight);
        settings.MemoSubtitleVOWidth = (int)Math.Round(MemoSubtitleVO.Width);
        // Mantém o que o arquivo tem além das colunas conhecidas (VO e colunas de plugins).
        // Uma posição por coluna do original; as que esta versão não tem (VO) ficam com o valor do
        // arquivo ou com a largura padrão do TVirtualTreeColumn (50).
        int count = Math.Max(settings.ColumnsWidth.Count, _columnIniIndex.Values.Max() + 1);
        var widths = Enumerable.Range(0, count).Select(i => i < settings.ColumnsWidth.Count ? settings.ColumnsWidth[i] : 50).ToArray();
        foreach (var (column, index) in _columnIniIndex)
        {
            double actual = column.ActualWidth;
            widths[index] = actual > 0 ? (int)Math.Round(actual) : (int)Math.Round(column.Width.DisplayValue);
        }

        settings.ColumnsWidth = widths;
        var positions = Enumerable.Range(0, count).Select(i => i < settings.ColumnsPosition.Count ? settings.ColumnsPosition[i] : i).ToArray();
        foreach (var (column, index) in _columnIniIndex)
        {
            positions[index] = column.DisplayIndex;
        }

        settings.ColumnsPosition = positions;
        StoreColumnSettings(settings);

        try
        {
            settings.Save();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning($"Could not save {settings.FileName}: {ex.Message}");
            MessageBox.Show(this, $"Could not save {settings.FileName}: {ex.Message}", MainViewModel.ApplicationName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SubsList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _viewModel.UpdateSelectedSubtitles(vtvSubsList.SelectedItems.OfType<SubtitleItemViewModel>());

    /// <summary>SubListPopupMenuPopup.</summary>
    private void SubsList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        pmiSubListDelete.IsEnabled = _viewModel.SelectedCount > 0;
        pmiSubListMerge.IsEnabled = _viewModel.SelectedCount > 1;
        pmiFixError.IsEnabled = _viewModel.CanFixErrorMain;
    }

    /// <summary>pmiFixError (ActionFixErrorMain): ativado só pelo SubListPopupMenuPopup.</summary>
    private void pmiFixError_Click(object sender, RoutedEventArgs e) => _viewModel.FixErrorMainCommand.Execute(null);

    /// <summary>MenuItemCheckUpdatesClick do legado: dispara a verificação manual pelo menu "?".
    /// </summary>
    private void MenuItemCheckUpdates_Click(object sender, RoutedEventArgs e) => _viewModel.CheckUpdatesCommand.Execute(null);

    /// <summary>WAVDisplayPopupMenuPopup.</summary>
    private void WAVDisplayer_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        pmiWAVDispAddSubtitle.IsEnabled = !_viewModel.IsWaveSelectionEmpty;
        pmiWAVDispSetSubtitleTime.IsEnabled = !_viewModel.IsWaveSelectionEmpty && _viewModel.SelectedSubtitle is not null;
        pmiWAVDispDeleteRange.IsEnabled = _viewModel.RangeIndexAtCursor() != -1;
        pmiWAVDispSplitAtCursor.IsEnabled = pmiWAVDispDeleteRange.IsEnabled;
        pmiInsertKaraokeMarker.IsEnabled = pmiWAVDispDeleteRange.IsEnabled;
        pmiDeleteSC.IsEnabled = !_viewModel.IsWaveSelectionEmpty;
        pmiInsertSC.IsEnabled = _viewModel.IsWaveSelectionEmpty;
    }

    /// <summary>MemoTextPipePopupMenuPopup, mais a opção marcada (itens de rádio do grupo 1).</summary>
    private void MemoTextPipe_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        pmiAddAsSubtitle.IsEnabled = _viewModel.CanAddSubFromPipe;
        pmiReplaceSubtitleFromPipe.IsEnabled = _viewModel.CanReplaceFromPipe;
        pmiAutoColorizeText.IsChecked = _viewModel.TextPipeAutoOption == TextPipeAutoOption.Colorize;
        pmiAutoDeleteText.IsChecked = _viewModel.TextPipeAutoOption == TextPipeAutoOption.Delete;
        pmiAutoDeleteAllTextBefore.IsChecked = _viewModel.TextPipeAutoOption == TextPipeAutoOption.DeleteAllBefore;
    }

    private void TextPipeAutoOption_Click(object sender, RoutedEventArgs e) =>
        _viewModel.TextPipeAutoOption = sender == pmiAutoDeleteText ? TextPipeAutoOption.Delete
            : sender == pmiAutoDeleteAllTextBefore ? TextPipeAutoOption.DeleteAllBefore
            : TextPipeAutoOption.Colorize;

    private void SubsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindParent<DataGridRow>(source) is not null)
        {
            _viewModel.ActivateSelectedSubtitle(fromUser: true);
        }
    }

    private void MemoSubtitleText_Changed(object sender, RoutedEventArgs e)
    {
        var memo = MemoSubtitleText;
        int caret = memo.SelectionStart + memo.SelectionLength;
        int line = Math.Max(0, memo.GetLineIndexFromCharacterIndex(caret));
        int lineStart = Math.Max(0, memo.GetCharacterIndexFromLineIndex(line));
        int firstVisible = Math.Max(0, memo.GetFirstVisibleLineIndex());
        _viewModel.UpdateEditorStatus(line, caret - lineStart, firstVisible);

        // O contador acompanha a rolagem do editor, pois começa na primeira linha visível.
        MemoLinesCounter.ScrollToHome();
    }

    private static void ApplyFont(Control control, FontSpec font)
    {
        control.FontFamily = new FontFamily(font.Name);
        control.FontSize = font.SizePx;
        control.FontWeight = font.Bold ? FontWeights.Bold : FontWeights.Normal;
        control.FontStyle = font.Italic ? FontStyles.Italic : FontStyles.Normal;
        if (font.ToRgb() is int rgb)
        {
            control.Foreground = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        }
        else
        {
            control.ClearValue(ForegroundProperty);
        }
    }

    /// <summary>Canvas.TextHeight do Delphi: altura da célula da fonte (ascendente + descendente).</summary>
    private static double TextHeight(FontFamily family, double size) => Math.Ceiling(family.LineSpacing * size);

    private static double TextWidth(string text, Control control)
    {
        var typeface = new Typeface(control.FontFamily, control.FontStyle, control.FontWeight, control.FontStretch);
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, control.FontSize, Brushes.Black, 1.0);
        return ft.WidthIncludingTrailingWhitespace;
    }

    private static T? FindParent<T>(DependencyObject child)
        where T : DependencyObject
    {
        for (var current = child; current is not null;
             current = current is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }
}
