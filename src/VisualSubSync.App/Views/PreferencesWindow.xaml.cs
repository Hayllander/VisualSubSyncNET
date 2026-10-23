using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VisualSubSync.App.Services;
using VisualSubSync.Core.Hotkeys;
using VisualSubSync.Core.Plugins;
using VisualSubSync.Core.Settings;

namespace VisualSubSync.App.Views;

/// <summary>
/// TPreferencesForm (PreferencesFormUnit.pas): LoadConfig copia as preferências para a janela, que edita
/// cópias dos plugins e dos atalhos; OK chama SaveConfig. As associações de arquivo mudam na hora, como
/// no original.
/// </summary>
public partial class PreferencesWindow : Window
{
    private readonly VssSettings _settings;
    private readonly List<PluginRow> _plugins;
    private readonly HotkeyEditor _hotkeys;
    private readonly List<HotkeyRow> _hotkeyRows;
    private FontSpec _subListFont;
    private FontSpec _subTextFont;
    private ParamRow? _editing;
    private int _hotKey;
    private bool _updatingAssociations;

    public PreferencesWindow(VssSettings settings, string? tabName)
    {
        ArgumentNullException.ThrowIfNull(settings);
        InitializeComponent();
        _settings = settings;
        _subListFont = settings.SubListFont;
        _subTextFont = settings.SubTextFont;

        // FormCreate: a primeira aba; ShowPreferences(TabSheet) troca para a pedida.
        PageControlPreferences.SelectedItem = tabName is not null && FindName(tabName) is TabItem tab ? tab : tsGeneral;

        _plugins = [.. (settings.Plugins?.Plugins ?? []).Select(p => new PluginRow(p.Clone()))
            .OrderBy(p => p.Info.Name, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true))];
        _hotkeys = new HotkeyEditor(settings.Hotkeys);
        _hotkeyRows = [.. _hotkeys.Entries.Select(e => new HotkeyRow(e))];
        LoadConfig();
    }

    /// <summary>A aba aberta quando a janela fechou, para a próxima vez (o original reaproveita o formulário).</summary>
    public string SelectedTabName => (PageControlPreferences.SelectedItem as TabItem)?.Name ?? tsGeneral.Name;

    // ---------------------------------------------------------------- LoadConfig / SaveConfig

    private void LoadConfig()
    {
        var c = _settings;
        chkSwapSubList.IsChecked = c.SwapSubtitlesList;
        chkDisableSubEditionInTimingMode.IsChecked = c.DisableSubtitleEdition;
        chkEnableSubCreationWithSpaceKey.IsChecked = c.EnableToggleCreation;
        chkEnableMouseAntiOverlapping.IsChecked = c.EnableMouseAntiOverlapping;
        chkEnableMouseSnapping.IsChecked = c.EnableMouseSnapping;
        chkSpaceKeyModifyTiming.IsChecked = c.SpaceKeyModifyTiming;
        EditCPSTarget.Position = c.SpaceKeyCpsTarget;
        EditMinimalDuration.Position = c.SpaceKeyMinimalDuration;
        EditBlankBetweenSub.Position = c.SpaceKeyBlankBetweenSubtitles;
        EditMaximalDuration.Position = c.MaximumSubtitleDuration;
        chkLoadMostRecentProjectOnStartup.IsChecked = c.LoadMostRecentProjectOnStartup;
        chkCheckUpdates.IsChecked = c.CheckUpdates;

        EditServerPort.Position = c.WebServerPort;
        chkEnableCompression.IsChecked = c.WebServerEnableCompression;

        // ListErrorChecking: ordenada pelo nome; o primeiro item fica selecionado.
        ListErrorChecking.ItemsSource = _plugins;
        if (_plugins.Count > 0)
        {
            ListErrorChecking.SelectedIndex = 0;
        }

        ListHotkeys.ItemsSource = _hotkeyRows;
        SetHotKeyField(0);

        ComboWheelTimeScrollModifier.SelectedIndex = (int)c.MouseWheelTimeScrollModifier;
        ComboWheelVZoomModifier.SelectedIndex = (int)c.MouseWheelVZoomModifier;
        ComboWheelHZoomModifier.SelectedIndex = (int)c.MouseWheelHZoomModifier;
        chkEnableSSATimingMode.IsChecked = c.MouseEnableSsaTimingMode;

        chkCreateBackup.IsChecked = c.EnableBackup;
        EditBackupTime.Position = c.AutoBackupEvery;
        chkAutoSaveWhenPlaying.IsChecked = c.AutoSaveWhenPlaying;

        ShowFont(EditSubListFont, _subListFont);
        ShowFont(EditSubTextFont, _subTextFont);

        chkSceneChange.IsChecked = c.ShowSceneChange;
        EditSCStartOffset.Position = c.SceneChangeStartOffset;
        EditSCStopOffset.Position = c.SceneChangeStopOffset;
        EditSCFilterOffset.Position = c.SceneChangeFilterOffset;
        chkShowTextInWAVDisplay.IsChecked = c.ShowTextInWavDisplay;
    }

    private void SaveConfig()
    {
        var c = _settings;
        c.SwapSubtitlesList = chkSwapSubList.IsChecked == true;
        c.DisableSubtitleEdition = chkDisableSubEditionInTimingMode.IsChecked == true;
        c.EnableToggleCreation = chkEnableSubCreationWithSpaceKey.IsChecked == true;
        c.EnableMouseAntiOverlapping = chkEnableMouseAntiOverlapping.IsChecked == true;
        c.EnableMouseSnapping = chkEnableMouseSnapping.IsChecked == true;
        c.SpaceKeyModifyTiming = chkSpaceKeyModifyTiming.IsChecked == true;
        c.SpaceKeyCpsTarget = EditCPSTarget.Position;
        c.SpaceKeyMinimalDuration = EditMinimalDuration.Position;
        c.SpaceKeyBlankBetweenSubtitles = EditBlankBetweenSub.Position;
        c.MaximumSubtitleDuration = EditMaximalDuration.Position;
        c.LoadMostRecentProjectOnStartup = chkLoadMostRecentProjectOnStartup.IsChecked == true;
        c.CheckUpdates = chkCheckUpdates.IsChecked == true;

        c.WebServerPort = EditServerPort.Position;
        c.WebServerEnableCompression = chkEnableCompression.IsChecked == true;

        c.Plugins?.Assign(_plugins.Select(p => p.Info));
        c.Hotkeys.Assign(_hotkeys.Entries);

        c.MouseWheelTimeScrollModifier = (WheelModifier)ComboWheelTimeScrollModifier.SelectedIndex;
        c.MouseWheelVZoomModifier = (WheelModifier)ComboWheelVZoomModifier.SelectedIndex;
        c.MouseWheelHZoomModifier = (WheelModifier)ComboWheelHZoomModifier.SelectedIndex;
        c.MouseEnableSsaTimingMode = chkEnableSSATimingMode.IsChecked == true;

        c.EnableBackup = chkCreateBackup.IsChecked == true;
        c.AutoBackupEvery = EditBackupTime.Position;
        c.AutoSaveWhenPlaying = chkAutoSaveWhenPlaying.IsChecked == true;

        c.SubListFont = _subListFont;
        c.SubTextFont = _subTextFont;

        c.ShowSceneChange = chkSceneChange.IsChecked == true;
        c.SceneChangeStartOffset = EditSCStartOffset.Position;
        c.SceneChangeStopOffset = EditSCStopOffset.Position;
        c.SceneChangeFilterOffset = EditSCFilterOffset.Position;
        c.ShowTextInWavDisplay = chkShowTextInWAVDisplay.IsChecked == true;
    }

    /// <summary>bttOkClick: termina a edição de parâmetro em curso antes de gravar.</summary>
    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (_editing is not null && !EndEdit())
        {
            return;
        }

        SaveConfig();
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>FormKeyDown: Esc fecha, a não ser durante a edição de um parâmetro ou no campo de atalho.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _editing is null && !HotKey1.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            DialogResult = false;
        }
    }

    // ---------------------------------------------------------------- General

    /// <summary>FormActivate: UpdateAssociationCheckBoxes.</summary>
    private void OnActivated(object? sender, EventArgs e) => UpdateAssociationCheckBoxes();

    private static readonly (string Extension, string DocType, int IconIndex)[] Associations =
    [
        ("vssprj", "Document", 0),
        ("srt", "Document.srt", 0),
        ("ssa", "Document.ssa", 0),
        ("ass", "Document.ass", 0),
    ];

    private CheckBox[] AssociationBoxes => [chkAssociateExtVSSPRJ, chkAssociateExtSRT, chkAssociateExtSSA, chkAssociateExtASS];

    private void UpdateAssociationCheckBoxes()
    {
        _updatingAssociations = true;
        var boxes = AssociationBoxes;
        for (int i = 0; i < boxes.Length; i++)
        {
            boxes[i].IsChecked = ShellAssociation.IsRegistered(Associations[i].Extension, Associations[i].DocType, ExecutablePath);
        }

        _updatingAssociations = false;
    }

    private static string ExecutablePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "VisualSubSyncNET.exe");

    /// <summary>chkAssociateExt*Click: grava ou apaga a associação no HKEY_CLASSES_ROOT na hora.</summary>
    private void OnAssociateClick(object sender, RoutedEventArgs e)
    {
        if (_updatingAssociations)
        {
            return;
        }

        int i = Array.IndexOf(AssociationBoxes, sender);
        if (i < 0)
        {
            return;
        }

        // CheckRegistryAccess.
        if (!ShellAssociation.IsClassesRootWritable())
        {
            MessageBox.Show(this, "Sorry, but Administrator access rights are needed to change file association. Nothing has been changed.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateAssociationCheckBoxes();
            return;
        }

        var (extension, docType, icon) = Associations[i];
        if (AssociationBoxes[i].IsChecked == true)
        {
            ShellAssociation.Register(extension, docType, ExecutablePath, icon);
        }
        else
        {
            ShellAssociation.Unregister(extension, docType);
        }
    }

    /// <summary>bttOpenBackupDirClick: CheckBackupDirectory e abre a pasta Backup.</summary>
    private void OnOpenBackupDir(object sender, RoutedEventArgs e)
    {
        // A mesma pasta em que os backups são gravados (g_BackupDirectory): a portátil ou, sem permissão, a do perfil.
        string dir;
        try
        {
            dir = StoragePaths.ResolveBackupDirectory(AppContext.BaseDirectory);
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Explore(dir);
    }

    /// <summary>bttOpenBackupTempDirClick: %TEMP%\VisualSubSync\, se existir.</summary>
    private void OnOpenBackupTempDir(object sender, RoutedEventArgs e)
    {
        string dir = Path.Combine(Path.GetTempPath(), "VisualSubSync");
        if (Directory.Exists(dir))
        {
            Explore(dir);
        }
    }

    private static void Explore(string dir)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Explore {dir}: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- Fonts

    private void OnSubListFont(object sender, RoutedEventArgs e)
    {
        if (FontPicker.Choose(this, _subListFont) is { } font)
        {
            _subListFont = font;
            ShowFont(EditSubListFont, font);
        }
    }

    private void OnSubTextFont(object sender, RoutedEventArgs e)
    {
        if (FontPicker.Choose(this, _subTextFont) is { } font)
        {
            _subTextFont = font;
            ShowFont(EditSubTextFont, font);
        }
    }

    /// <summary>EditSub*Font: o texto Font2String escrito na própria fonte.</summary>
    private static void ShowFont(TextBox edit, FontSpec font)
    {
        edit.Text = font.ToString();
        edit.FontFamily = new FontFamily(font.Name);
        edit.FontSize = font.SizePx;
        edit.FontWeight = font.Bold ? FontWeights.Bold : FontWeights.Normal;
        edit.FontStyle = font.Italic ? FontStyles.Italic : FontStyles.Normal;
        var decorations = new TextDecorationCollection();
        if (font.Underline)
        {
            decorations.Add(TextDecorations.Underline);
        }

        if (font.Strikeout)
        {
            decorations.Add(TextDecorations.Strikethrough);
        }

        edit.TextDecorations = decorations;
        edit.Foreground = font.ToRgb() is int rgb
            ? new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb))
            : SystemColors.WindowTextBrush;
    }

    // ---------------------------------------------------------------- Error checking

    /// <summary>ListErrorCheckingClick: cor, descrição e parâmetros do plugin selecionado.</summary>
    private void OnErrorCheckingSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_editing is not null)
        {
            CancelEdit();
        }

        if (ListErrorChecking.SelectedItem is not PluginRow row)
        {
            return;
        }

        int c = row.Info.Color;
        ShapeErrorColor.Fill = new SolidColorBrush(Color.FromRgb((byte)c, (byte)(c >> 8), (byte)(c >> 16)));
        lbErrorDescription.Text = row.Info.Description;
        ListPluginParam.ItemsSource = row.Info.Params.Select(p => new ParamRow(p)).ToList();
    }

    /// <summary>ListErrorCheckingClickCheck: o item clicado também fica selecionado, como no TCheckListBox.</summary>
    private void OnPluginCheckClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PluginRow row })
        {
            ListErrorChecking.SelectedItem = row;
        }
    }

    /// <summary>Espaço marca ou desmarca o item selecionado (TCheckListBox).</summary>
    private void OnErrorCheckingKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && ListErrorChecking.SelectedItem is PluginRow row)
        {
            row.Enabled = !row.Enabled;
            e.Handled = true;
        }
    }

    private void OnSelectAllPlugins(object sender, RoutedEventArgs e) => _plugins.ForEach(p => p.Enabled = true);

    private void OnUnselectAllPlugins(object sender, RoutedEventArgs e) => _plugins.ForEach(p => p.Enabled = false);

    /// <summary>toEditOnDblClick: edita o valor (ListPluginParamEditing: só tipos conhecidos).</summary>
    private void OnParamDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ListPluginParam.SelectedItem is ParamRow row)
        {
            BeginEdit(row);
        }
    }

    private void OnParamListKeyDown(object sender, KeyEventArgs e)
    {
        if (_editing is null && e.Key == Key.F2 && ListPluginParam.SelectedItem is ParamRow row)
        {
            BeginEdit(row);
            e.Handled = true;
        }
    }

    /// <summary>TPropertyEditLink.PrepareEdit/BeginEdit: caixa de texto, ou lista -1/0 para booleanos.</summary>
    private void BeginEdit(ParamRow row)
    {
        if (row.Param.ParamType == JsPluginParamType.Unknown)
        {
            return;
        }

        if (_editing is not null && !EndEdit())
        {
            return;
        }

        Control editor;
        if (row.Param.ParamType == JsPluginParamType.Boolean)
        {
            // BoolToStr(True) e BoolToStr(False) do Delphi.
            var combo = new ComboBox { IsEditable = true, Text = row.ValueAsString, ItemsSource = new[] { "-1", "0" } };
            combo.Text = row.ValueAsString;
            editor = combo;
        }
        else
        {
            var edit = new TextBox { Text = row.ValueAsString, Padding = new Thickness(0) };
            if (row.Param.ParamType is JsPluginParamType.Integer or JsPluginParamType.Double)
            {
                bool isDouble = row.Param.ParamType == JsPluginParamType.Double;
                // EditIntegerKeyPress / EditFloatKeyPress: só dígitos (e ponto nos números reais).
                edit.PreviewTextInput += (_, args) =>
                    args.Handled = !args.Text.All(ch => char.IsAsciiDigit(ch) || (isDouble && ch == '.'));
                DataObject.AddPastingHandler(edit, (_, args) => args.CancelCommand());
            }

            edit.SelectAll();
            editor = edit;
        }

        editor.Background = SystemColors.WindowBrush;
        editor.PreviewKeyDown += OnEditorKeyDown;
        editor.LostKeyboardFocus += OnEditorLostFocus;
        _editing = row;
        row.Editor = editor;
        editor.Dispatcher.BeginInvoke(() =>
        {
            editor.Focus();
            if (editor is TextBox t)
            {
                t.SelectAll();
            }
        });
    }

    /// <summary>TPropertyEditLink.EditKeyDown: Enter termina, Esc cancela, setas mudam de linha.</summary>
    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                EndEdit();
                e.Handled = true;
                break;
            case Key.Escape:
                CancelEdit();
                e.Handled = true;
                break;
            case Key.Up or Key.Down when Keyboard.Modifiers == ModifierKeys.None && sender is not ComboBox { IsDropDownOpen: true }:
                if (EndEdit())
                {
                    int index = ListPluginParam.SelectedIndex + (e.Key == Key.Up ? -1 : 1);
                    if (index >= 0 && index < ListPluginParam.Items.Count)
                    {
                        ListPluginParam.SelectedIndex = index;
                    }
                }

                e.Handled = true;
                break;
        }
    }

    private void OnEditorLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is Control editor && editor.IsKeyboardFocusWithin)
        {
            return;
        }

        if (_editing is { Editor: { } current } && ReferenceEquals(current, sender))
        {
            EndEdit();
        }
    }

    /// <summary>
    /// TPropertyEditLink.EndEdit: um valor diferente vai para o parâmetro. Um valor que não converte
    /// (StrToInt, StrToFloat, StrToBool) mostra o erro do Delphi e mantém a edição aberta.
    /// </summary>
    private bool EndEdit()
    {
        if (_editing is not { Editor: { } editor } row)
        {
            return true;
        }

        string s = editor is ComboBox combo ? combo.Text : ((TextBox)editor).Text;
        if (s != row.ValueAsString)
        {
            try
            {
                row.Param.SetValueFromString(s);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                string kind = row.Param.ParamType switch
                {
                    JsPluginParamType.Integer => "integer",
                    JsPluginParamType.Double => "floating point",
                    _ => "boolean",
                };
                MessageBox.Show(this, $"'{s}' is not a valid {kind} value", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                editor.Focus();
                return false;
            }

            row.ValueAsString = s;
        }

        CloseEditor(row);
        return true;
    }

    private void CancelEdit()
    {
        if (_editing is { } row)
        {
            CloseEditor(row);
        }
    }

    private void CloseEditor(ParamRow row)
    {
        if (row.Editor is { } editor)
        {
            editor.PreviewKeyDown -= OnEditorKeyDown;
            editor.LostKeyboardFocus -= OnEditorLostFocus;
        }

        _editing = null;
        row.Editor = null;
        ListPluginParam.Focus();
    }

    // ---------------------------------------------------------------- Hotkeys

    private HotkeyRow? FocusedHotkey => ListHotkeys.SelectedItem as HotkeyRow;

    /// <summary>ListHotkeysSelectItem / ComboHotkeyModeSelect: o campo mostra o atalho do modo atual.</summary>
    private void OnHotkeySelected(object sender, SelectionChangedEventArgs e) => SetHotKeyField(_hotkeys.ShortcutOf(FocusedHotkey?.Entry));

    private void OnHotkeyModeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_hotkeys is null)
        {
            return;
        }

        _hotkeys.TimingMode = ComboHotkeyMode.SelectedIndex == 1;
        SetHotKeyField(_hotkeys.ShortcutOf(FocusedHotkey?.Entry));
    }

    private void OnSetHotkey(object sender, RoutedEventArgs e)
    {
        _hotkeys.Set(FocusedHotkey?.Entry, _hotKey);
        RefreshHotkeyRows();
    }

    private void OnClearHotkey(object sender, RoutedEventArgs e)
    {
        _hotkeys.Clear(FocusedHotkey?.Entry);
        RefreshHotkeyRows();
    }

    private void OnResetAllHotkeys(object sender, RoutedEventArgs e)
    {
        _hotkeys.ResetAll();
        RefreshHotkeyRows();
    }

    private void RefreshHotkeyRows() => _hotkeyRows.ForEach(r => r.Refresh());

    /// <summary>
    /// THotKey: guarda a tecla com os modificadores. Backspace sozinho limpa ("None") e Tab passa para
    /// o próximo controle, como no controle de atalho do Windows.
    /// </summary>
    private void OnHotKeyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;
        if (key == Key.Tab && (mods & ~ModifierKeys.Shift) == ModifierKeys.None)
        {
            return;
        }

        e.Handled = true;
        if (key == Key.Back && mods == ModifierKeys.None)
        {
            SetHotKeyField(0);
            return;
        }

        if (key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            HotKey1.Text = ModifierText(mods);
            return;
        }

        SetHotKeyField(DelphiShortcut.Make(KeyInterop.VirtualKeyFromKey(key), mods.HasFlag(ModifierKeys.Shift),
            mods.HasFlag(ModifierKeys.Control), mods.HasFlag(ModifierKeys.Alt)));
    }

    private void OnHotKeyPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.None || (_hotKey & 0xFF) != 0)
        {
            SetHotKeyField(_hotKey);
        }
    }

    private void SetHotKeyField(int shortcut)
    {
        _hotKey = shortcut;
        HotKey1.Text = HotkeyText(shortcut);
        HotKey1.CaretIndex = HotKey1.Text.Length;
    }

    /// <summary>O texto do controle de atalho do Windows: "Ctrl + Shift + Alt + tecla", ou "None".</summary>
    private static string HotkeyText(int shortcut)
    {
        int vk = shortcut & 0xFF;
        if (vk == 0)
        {
            return "None";
        }

        string name = KeyName(vk);
        return ((shortcut & DelphiShortcut.Ctrl) != 0 ? "Ctrl + " : string.Empty) +
               ((shortcut & DelphiShortcut.Shift) != 0 ? "Shift + " : string.Empty) +
               ((shortcut & DelphiShortcut.Alt) != 0 ? "Alt + " : string.Empty) + name;
    }

    private static string ModifierText(ModifierKeys mods)
    {
        string text = (mods.HasFlag(ModifierKeys.Control) ? "Ctrl + " : string.Empty) +
                      (mods.HasFlag(ModifierKeys.Shift) ? "Shift + " : string.Empty) +
                      (mods.HasFlag(ModifierKeys.Alt) ? "Alt + " : string.Empty);
        return text.Length > 0 ? text : "None";
    }

    private static string KeyName(int vk)
    {
        string text = DelphiShortcut.ToText(vk);
        return text.Length > 0 ? text : "#" + vk.ToString(CultureInfo.InvariantCulture);
    }

    // ---------------------------------------------------------------- linhas das listas

    private sealed class PluginRow(PluginInfo info) : INotifyPropertyChanged
    {
        public PluginInfo Info { get; } = info;

        public string Name => Info.Name;

        public bool Enabled
        {
            get => Info.Enabled;
            set
            {
                if (Info.Enabled != value)
                {
                    Info.Enabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>TParamData: o parâmetro e o texto mostrado (ValueAsString).</summary>
    private sealed class ParamRow(JsPluginParam param) : INotifyPropertyChanged
    {
        private string _valueAsString = param.ValueAsString;
        private Control? _editor;

        public JsPluginParam Param { get; } = param;

        public string Name => Param.Name;

        public string Unit => Param.Unit;

        public string Description => Param.Description;

        public string ValueAsString
        {
            get => _valueAsString;
            set => Set(ref _valueAsString, value);
        }

        public Control? Editor
        {
            get => _editor;
            set => Set(ref _editor, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string name = "")
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    private sealed class HotkeyRow(HotkeyEntry entry) : INotifyPropertyChanged
    {
        public HotkeyEntry Entry { get; } = entry;

        public string Caption => Entry.Caption;

        public string NormalText => DelphiShortcut.ToText(Entry.NormalShortcut);

        public string TimingText => DelphiShortcut.ToText(Entry.TimingShortcut);

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Refresh()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NormalText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimingText)));
        }
    }
}
