using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.App.ViewModels;
using VisualSubSync.Core.Hotkeys;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.App.Views;

/// <summary>
/// Atalhos das ações (TCustomActionList.IsShortCut + SetShortcut): a tecla é comparada com o atalho
/// do modo atual antes de chegar ao controle focado, como a VCL faz em CN_KEYDOWN. Ações ainda não
/// portadas não têm comando e deixam a tecla passar, como uma ação desabilitada.
/// </summary>
public partial class MainWindow
{
    private Dictionary<string, (ICommand Command, object? Parameter)> _actions = [];

    private void InitializeHotkeys(MainViewModel vm)
    {
        DelphiShortcut.SpecialName = WindowsKeyName;
        _actions = new()
        {
            ["ActionUndo"] = (vm.UndoCommand, null),
            ["ActionRedo"] = (vm.RedoCommand, null),
            ["ActionMerge"] = (vm.MergeCommand, null),
            ["ActionDelay"] = (vm.DelayCommand, null),
            ["ActionFind"] = (vm.FindCommand, null),
            ["ActionFindNext"] = (vm.FindNextCommand, null),
            ["ActionGoto"] = (vm.GotoCommand, null),
            ["ActionMergeDialog"] = (vm.MergeDialogCommand, null),
            ["ActionMergeOnOneLine"] = (vm.MergeOnOneLineCommand, null),
            ["ActionMergeWithPrevious"] = (vm.MergeWithPreviousCommand, null),
            ["ActionMergeWithNext"] = (vm.MergeWithNextCommand, null),
            ["ActionSplitAtCursor"] = (vm.SplitAtCursorCommand, null),
            ["ActionInsertKaraokeMarker"] = (vm.InsertKaraokeMarkerCommand, null),
            ["ActionPlaceKaraokeCursorsAtEnd"] = (vm.PlaceKaraokeCursorsAtEndCommand, null),
            ["ActionShowHideTextPipe"] = (vm.ShowHideTextPipeCommand, null),
            ["ActionLoadTextPipe"] = (vm.LoadTextPipeCommand, null),
            ["ActionClearTextPipe"] = (vm.ClearTextPipeCommand, null),
            ["ActionAddSubFromPipe"] = (vm.AddSubFromPipeCommand, null),
            ["ActionReplaceFromPipe"] = (vm.ReplaceFromPipeCommand, null),
            ["ActionTextItalic"] = (vm.TextItalicCommand, null),
            ["ActionTextBold"] = (vm.TextBoldCommand, null),
            ["ActionTextUnderline"] = (vm.TextUnderlineCommand, null),
            ["ActionTextColor"] = (vm.TextColorCommand, null),
            ["ActionTextSize"] = (vm.TextSizeCommand, null),
            ["ActionStripTags"] = (vm.StripTagsCommand, null),
            ["ActionInsertSceneChange"] = (vm.InsertSceneChangeAtCursorCommand, null),
            ["ActionDeleteSceneChange"] = (vm.DeleteSceneChangesInSelectionCommand, null),
            ["ActionReload"] = (vm.ReloadCommand, null),
            ["ActionOpenProjectFolder"] = (vm.OpenProjectFolderCommand, null),
            ["ActionInsertTextFile"] = (vm.InsertTextFileCommand, null),
            ["ActionExportToWAV"] = (vm.ExportToWavCommand, null),
            ["ActionShowSuggestions"] = (vm.ShowSuggestionsCommand, null),
            ["ActionPreferences"] = (vm.PreferencesCommand, null),
            ["ActionStyles"] = (vm.StylesCommand, null),
            ["ActionSpellCheck"] = (vm.SpellCheckCommand, null),
            ["ActionCheckErrors"] = (vm.CheckErrorsCommand, null),
            ["ActionShowErrorReport"] = (vm.ShowErrorReportCommand, null),
            ["ActionNextError"] = (vm.NextErrorCommand, null),
            ["ActionFixErrorMain"] = (vm.FixErrorMainCommand, null),
            ["ActionFixSelectedErrors"] = (vm.FixSelectedErrorsCommand, null),
            ["ActionZoomVertical"] = (vm.ZoomVerticalCommand, null),
            ["ActionShowSilentZones"] = (vm.ShowSilentZonesCommand, null),
            ["ActionShowHideLogs"] = (vm.ShowHideLogsCommand, null),
            ["ActionShowHideSubs"] = (vm.ShowHideSubsCommand, null),
            ["ActionZoomIn"] = (vm.ZoomInCommand, null),
            ["ActionZoomOut"] = (vm.ZoomOutCommand, null),
            ["ActionZoomSelection"] = (vm.ZoomSelectionCommand, null),
            ["ActionZoomAll"] = (vm.ZoomAllCommand, null),
            ["ActionPlay"] = (vm.PlayCommand, null),
            ["ActionStop"] = (vm.StopCommand, null),
            ["ActionLoop"] = (vm.LoopCommand, null),
            ["ActionShowHideVideo"] = (vm.ShowHideVideoCommand, null),
            ["ActionSave"] = (vm.SaveCommand, null),
            ["ActionSaveAs"] = (vm.SaveAsCommand, null),
            ["ActionProjectProperties"] = (vm.ProjectPropertiesCommand, null),
            ["ActionNextSub"] = (vm.NextSubCommand, null),
            ["ActionPreviousSub"] = (vm.PreviousSubCommand, null),
            ["ActionClose"] = (vm.CloseCommand, null),
            ["ActionClearSelection"] = (vm.ClearSelectionCommand, null),
            ["ActionLoopSelStart"] = (vm.LoopSelectionStartCommand, null),
            ["ActionLoopSelEnd"] = (vm.LoopSelectionEndCommand, null),
            ["ActionAddSubtitle"] = (vm.AddSubtitleCommand, null),
            ["ActionSetSubtitleTime"] = (vm.SetFocusedSubtitleTimeCommand, null),
            ["ActionSetPlaybackRate60"] = (vm.SetPlaybackRateCommand, "60"),
            ["ActionSetPlaybackRate70"] = (vm.SetPlaybackRateCommand, "70"),
            ["ActionSetPlaybackRate80"] = (vm.SetPlaybackRateCommand, "80"),
            ["ActionSetPlaybackRate90"] = (vm.SetPlaybackRateCommand, "90"),
            ["ActionSetPlaybackRate100"] = (vm.SetPlaybackRateCommand, "100"),
            ["ActionShiftStartPlus100"] = (vm.ShiftStartTimeCommand, "100"),
            ["ActionShiftStartMinus100"] = (vm.ShiftStartTimeCommand, "-100"),
            ["ActionShiftStopPlus100"] = (vm.ShiftStopTimeCommand, "100"),
            ["ActionShiftStopMinus100"] = (vm.ShiftStopTimeCommand, "-100"),
            ["ActionShiftStartPlus10"] = (vm.ShiftStartTimeCommand, "10"),
            ["ActionShiftStartMinus10"] = (vm.ShiftStartTimeCommand, "-10"),
            ["ActionShiftStopPlus10"] = (vm.ShiftStopTimeCommand, "10"),
            ["ActionShiftStopMinus10"] = (vm.ShiftStopTimeCommand, "-10"),
            ["ActionStartSub"] = (vm.StartSubCommand, null),
            ["ActionStopSub"] = (vm.StopSubCommand, null),
            ["ActionStopAndStartSub"] = (vm.StopAndStartSubCommand, null),
            ["ActionShowStartFrame"] = (vm.ShowStartFrameCommand, null),
            ["ActionShowStopFrame"] = (vm.ShowStopFrameCommand, null),
            ["ActionShowFrameAtCursor"] = (vm.ShowFrameAtCursorCommand, null),
            ["ActionPlaySelStart"] = (vm.PlaySelectionStartCommand, null),
            ["ActionPlaySelEnd"] = (vm.PlaySelectionEndCommand, null),
            ["ActionPlayToEnd"] = (vm.PlayToEndCommand, null),
            ["ActionPlay1sBefore"] = (vm.Play1sBeforeCommand, null),
            ["ActionSelectNextSub"] = (vm.SelectNextSubCommand, null),
            ["ActionSelectPreviousSub"] = (vm.SelectPreviousSubCommand, null),
            ["ActionSetSubtitleStartTime"] = (vm.SetSubtitleStartTimeCommand, null),
            ["ActionSetSubtitleStopTime"] = (vm.SetSubtitleStopTimeCommand, null),
            ["ActionSetSubtitleStopTimeAndGoToNext"] = (vm.SetSubtitleStopTimeAndGoToNextCommand, null),
            ["ActionPause"] = (vm.PauseCommand, null),
            ["ActionToggleTimingMode"] = (vm.ToggleTimingModeCommand, null),
            ["ActionSetEditorFocus"] = (vm.SetEditorFocusCommand, null),
            ["ActionSetEditorFocusAndSelect"] = (vm.SetEditorFocusAndSelectCommand, null),
            ["ActionWAVDisplayScrollRight"] = (vm.WavDisplayScrollRightCommand, null),
            ["ActionWAVDisplayScrollLeft"] = (vm.WavDisplayScrollLeftCommand, null),
            ["ActionShowHideSceneChange"] = (vm.ShowHideSceneChangeCommand, null),
            ["ActionPlay1sBeforeToEnd"] = (vm.Play1sBeforeToEndCommand, null),
        };

        // As ações [js] do plugin geral (TJavascriptAction, Tag = 1).
        foreach (var action in vm.JsActions)
        {
            _actions[action.Name] = (vm.ExecuteJsActionCommand, action.Name);
        }

        vm.WaveformFocusRequested += (_, _) => WAVDisplayer.Focus();
        // pmiSelectAllXXX/YYY do Error report: MainForm.vtvSubsList.Selected[Node] := True.
        vm.SubtitleSelectionAddRequested += (_, items) =>
        {
            foreach (var item in items)
            {
                vtvSubsList.SelectedItems.Add(item);
            }
        };
        // MemoSubtitleText.Focused: com o menu do editor aberto o foco do teclado está no menu, mas o
        // foco lógico da janela continua no editor.
        vm.IsEditorFocused = () => MemoSubtitleText.IsKeyboardFocused || ReferenceEquals(FocusManager.GetFocusedElement(this), MemoSubtitleText);
        vm.EditorFocusRequested += (_, selectAll) =>
        {
            if (MemoSubtitleText.IsEnabled)
            {
                MemoSubtitleText.Focus();
            }

            if (selectAll)
            {
                MemoSubtitleText.SelectAll();
            }
        };
        // vtvSubsList.IsVisible[Node] de ShowMatchingSubOnly.
        vm.SubtitleFilterChanged += (_, _) =>
        {
            var view = CollectionViewSource.GetDefaultView(vm.Subtitles);
            view.Filter = o => o is SubtitleItemViewModel item && vm.IsSubtitleVisible(item);
        };
        vm.EditorSelectionRequested += (_, selection) =>
        {
            int start = Math.Min(selection.Start, MemoSubtitleText.Text.Length);
            MemoSubtitleText.Select(start, Math.Min(selection.Length, MemoSubtitleText.Text.Length - start));
        };

        // SaveSelectionInfo: a seleção de antes de cada mudança de texto vai para a tarefa de desfazer.
        // A atualização é adiada para que, durante a própria mudança, o modelo ainda tenha a anterior.
        MemoSubtitleText.SelectionChanged += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            vm.UpdateEditorSelection(MemoSubtitleText.SelectionStart, MemoSubtitleText.SelectionLength));
        // ActionCopy/ActionPaste (Ctrl+C, Ctrl+V) com a lista focada; no editor o TextBox já copia e cola.
        vtvSubsList.InputBindings.Add(new KeyBinding(new RelayCommand(CopySubtitles), Key.C, ModifierKeys.Control));
        vtvSubsList.InputBindings.Add(new KeyBinding(new RelayCommand(() => PasteSubtitles(atCursor: false)), Key.V, ModifierKeys.Control));
        Loaded += (_, _) => UpdateMenuShortcuts();
    }

    /// <summary>CopyVTVToClipboard.</summary>
    private void CopySubtitles()
    {
        if (_viewModel.CopySelectedSubtitles() is not { } copy)
        {
            return;
        }

        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, copy.Text);
        data.SetData(SubtitleClipboard.FormatName, new MemoryStream(copy.Data));
        try
        {
            Clipboard.SetDataObject(data, copy: true);
        }
        catch (ExternalException ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Clipboard: {ex.Message}");
        }
    }

    /// <summary>PasteClipboard: o formato próprio primeiro e, sem legendas, o texto (SRT ou SSA).</summary>
    private void PasteSubtitles(bool atCursor)
    {
        var subs = new List<Subtitle>();
        try
        {
            if (Clipboard.ContainsData(SubtitleClipboard.FormatName) &&
                Clipboard.GetData(SubtitleClipboard.FormatName) is MemoryStream stream)
            {
                subs = SubtitleClipboard.Deserialize(stream.ToArray());
            }

            if (subs.Count == 0 && Clipboard.ContainsText())
            {
                subs = SubtitleClipboard.ParseText(Clipboard.GetText());
            }
        }
        catch (ExternalException ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Clipboard: {ex.Message}");
        }

        _viewModel.PasteSubtitles(subs, atCursor);
    }

    /// <summary>ActionPasteAtCursorExecute.</summary>
    private void PasteAtCursor_Click(object sender, RoutedEventArgs e) => PasteSubtitles(atCursor: true);

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPreviewKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        int shortcut = ToShortcut(key);
        if (shortcut != 0 && _viewModel.Settings.Hotkeys.Find(shortcut, _viewModel.IsTimingMode) is { } entry)
        {
            // IsShortCut para na primeira ação com o atalho; só a executada consome a tecla, senão ela segue.
            if (_actions.TryGetValue(entry.ActionName, out var action) && action.Command.CanExecute(action.Parameter))
            {
                action.Command.Execute(action.Parameter);
                e.Handled = true;
                return;
            }
        }

        // TntFormKeyDown (KeyPreview no modo Timing).
        if (key == Key.Space && _viewModel.OnTimingSpaceDown())
        {
            e.Handled = true;
        }
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPreviewKeyUp(e);
        if (!e.Handled && e.Key == Key.Space && _viewModel.OnTimingSpaceUp())
        {
            e.Handled = true;
        }
    }

    /// <summary>SetShortcut: os menus mostram o atalho do modo atual (o TMenuItem segue a TAction).</summary>
    private void UpdateMenuShortcuts()
    {
        var menus = MainMenu.Items.OfType<MenuItem>()
            .Concat(vtvSubsList.ContextMenu.Items.OfType<MenuItem>())
            .Concat(WAVDisplayer.ContextMenu.Items.OfType<MenuItem>())
            .Concat(MemoTextPipe.ContextMenu.Items.OfType<MenuItem>())
            .Concat(MemoSubPopupMenu.Items.OfType<MenuItem>());
        foreach (var item in menus)
        {
            UpdateMenuShortcuts(item);
        }
    }

    private void UpdateMenuShortcuts(MenuItem item)
    {
        string? actionName = item.Tag as string;
        if (actionName is null && item.Command is { } command)
        {
            actionName = _actions.FirstOrDefault(a => ReferenceEquals(a.Value.Command, command) &&
                Equals(a.Value.Parameter, item.CommandParameter)).Key;
        }

        if (actionName is not null && _viewModel.Settings.Hotkeys.Get(actionName) is { } entry)
        {
            item.InputGestureText = DelphiShortcut.ToText(entry.ShortcutFor(_viewModel.IsTimingMode));
        }

        foreach (var child in item.Items.OfType<MenuItem>())
        {
            UpdateMenuShortcuts(child);
        }
    }

    private static int ToShortcut(Key key)
    {
        if (key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.None)
        {
            return 0;
        }

        var modifiers = Keyboard.Modifiers;
        return DelphiShortcut.Make(KeyInterop.VirtualKeyFromKey(key), modifiers.HasFlag(ModifierKeys.Shift),
            modifiers.HasFlag(ModifierKeys.Control), modifiers.HasFlag(ModifierKeys.Alt));
    }

    /// <summary>GetSpecialName de Menus.pas: GetKeyNameText(MapVirtualKey(vk, 0) shl 16).</summary>
    private static unsafe string? WindowsKeyName(int virtualKey)
    {
        uint scanCode = MapVirtualKeyW((uint)virtualKey, 0);
        if (scanCode == 0)
        {
            return null;
        }

        char* buffer = stackalloc char[64];
        int length = GetKeyNameTextW((int)(scanCode << 16), buffer, 64);
        return length > 0 ? new string(buffer, 0, length) : null;
    }

    [LibraryImport("user32.dll")]
    private static partial uint MapVirtualKeyW(uint code, uint mapType);

    [LibraryImport("user32.dll")]
    private static unsafe partial int GetKeyNameTextW(int lParam, char* buffer, int size);
}
