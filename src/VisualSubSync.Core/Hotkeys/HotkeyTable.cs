using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Hotkeys;

/// <summary>THotkeyListItemData: atalho de uma ação nos modos Normal e Timing.</summary>
public sealed class HotkeyEntry(string actionName, string caption, int normalShortcut, int timingShortcut)
{
    public string ActionName { get; } = actionName;

    /// <summary>Action.Caption: o nome mostrado na aba Hotkeys das preferências.</summary>
    public string Caption { get; } = caption;

    public int NormalShortcut { get; set; } = normalShortcut;

    public int TimingShortcut { get; set; } = timingShortcut;

    /// <summary>ListDefaultHotkeys: os atalhos antes da leitura do ini (Reset all).</summary>
    public int DefaultNormalShortcut { get; } = normalShortcut;

    public int DefaultTimingShortcut { get; } = timingShortcut;

    public int ShortcutFor(bool timingMode) => timingMode ? TimingShortcut : NormalShortcut;

    public void SetShortcutFor(bool timingMode, int shortcut)
    {
        if (timingMode)
        {
            TimingShortcut = shortcut;
        }
        else
        {
            NormalShortcut = shortcut;
        }
    }

    /// <summary>THotkeyListItemData.Assign (a cópia local das preferências).</summary>
    public HotkeyEntry Clone() => new(ActionName, Caption, DefaultNormalShortcut, DefaultTimingShortcut)
    {
        NormalShortcut = NormalShortcut,
        TimingShortcut = TimingShortcut,
    };
}

/// <summary>
/// TConfigObject.ListHotkeys: as ações com Tag = 1 do TMainForm do Enhanced, na ordem da ActionList.
/// O atalho Normal é o do formulário; o Timing vem de DefaultTimingShortcuts. A seção [Hotkeys] do
/// VisualSubSync.ini sobrepõe os dois (Acao[Normal] e Acao[Timing]).
/// </summary>
public sealed class HotkeyTable
{
    /// <summary>Ações com Tag = 1, com o Caption e o ShortCut do DFM (docs/enhanced/TMAINFORM.txt).</summary>
    private static readonly (string Name, string Caption, int Shortcut)[] FormActions =
    [
        ("ActionZoomIn", "Zoom In", 0),
        ("ActionZoomOut", "Zoom Out", 0),
        ("ActionZoomSelection", "Zoom Selection", 0),
        ("ActionZoomAll", "Zoom All", 0),
        ("ActionPlay", "Play", 112),
        ("ActionStop", "Stop", 27),
        ("ActionLoop", "Loop", 113),
        ("ActionShowHideVideo", "Show/Hide Video", 115),
        ("ActionFind", "Find", 16454),
        ("ActionSave", "Save", 16467),
        ("ActionSaveAs", "Save As", 0),
        ("ActionProjectProperties", "Project Properties", 0),
        ("ActionFindNext", "Find next", 114),
        ("ActionCheckErrors", "Check errors", 0),
        ("ActionFixCommonErrors", "Fix common errors", 0),
        ("ActionRemoveTextForHi", "Remove text for hearing impareid", 0),
        ("ActionDelay", "Delay", 16452),
        ("ActionShowErrorReport", "Show error report", 0),
        ("ActionNextSub", "Play next sub", 117),
        ("ActionPreviousSub", "Play previous sub", 116),
        ("ActionGoto", "Goto", 16455),
        ("ActionPreferences", "Preferences", 0),
        ("ActionClose", "Close", 0),
        ("ActionShowSuggestions", "Show suggestions", 0),
        ("ActionClearSelection", "Clear selection", 118),
        ("ActionLoopSelStart", "Loop selection start", 0),
        ("ActionLoopSelEnd", "Loop selection end", 0),
        ("ActionAddSubtitle", "Add subtitle", 16471),
        ("ActionSetSubtitleTime", "Set subtitle time", 16468),
        ("ActionSetPlaybackRate60", "Set playback rate at 60%", 0),
        ("ActionSetPlaybackRate70", "Set playback rate at 70%", 0),
        ("ActionSetPlaybackRate80", "Set playback rate at 80%", 0),
        ("ActionSetPlaybackRate90", "Set playback rate at 90%", 0),
        ("ActionSetPlaybackRate100", "Set playback rate at 100%", 0),
        ("ActionShiftStartPlus100", "Shift start time by +100ms", 0),
        ("ActionShiftStartMinus100", "Shift start time by -100ms", 0),
        ("ActionShiftStopPlus100", "Shift stop time by +100ms", 0),
        ("ActionShiftStopMinus100", "Shift stop time by -100ms", 0),
        ("ActionShiftStartPlus10", "Shift start time by +10ms", 0),
        ("ActionShiftStartMinus10", "Shift start time by -10ms", 0),
        ("ActionShiftStopPlus10", "Shift stop time by +10ms", 0),
        ("ActionShiftStopMinus10", "Shift stop time by -10ms", 0),
        ("ActionZoomVertical", "Zoom Vertical", 0),
        ("ActionShowHideTextPipe", "Show/Hide Text Pipe", 0),
        ("ActionLoadTextPipe", "Load text pipe...", 0),
        ("ActionClearTextPipe", "Clear text pipe", 0),
        ("ActionAddSubFromPipe", "Add subtitle from pipe", 0),
        ("ActionReplaceFromPipe", "Replace subtitle from pipe", 0),
        ("ActionDetachVideo", "Detach video window", 0),
        ("ActionShowHideLogs", "Show/Hide Logs", 0),
        ("ActionFixErrorMain", "Fix errors", 0),
        ("ActionStartSub", "Start new subtitle", 0),
        ("ActionStopSub", "Stop current subtitle", 0),
        ("ActionStopAndStartSub", "Stop current sub and start new sub", 0),
        ("ActionShowStartFrame", "Show start frame", 0),
        ("ActionShowStopFrame", "Show stop frame", 0),
        ("ActionShowFrameAtCursor", "Show frame at cursor", 0),
        ("ActionPlaySelStart", "Play selection start", 0),
        ("ActionPlaySelEnd", "Play selection end", 0),
        ("ActionPlaceKaraokeCursorsAtEnd", "Place karaoke cursors at end", 0),
        ("ActionPlayToEnd", "Play to end", 0),
        ("ActionPlay1sBefore", "Play 1s before start", 0),
        ("ActionSelectNextSub", "Select next sub", 0),
        ("ActionSelectPreviousSub", "Select previous sub", 0),
        ("ActionSetSubtitleStartTime", "Set subtitle start time", 0),
        ("ActionSetSubtitleStopTime", "Set subtitle stop time", 0),
        ("ActionSetSubtitleStopTimeAndGoToNext", "Set subtitle stop time / go to next", 0),
        ("ActionStyles", "Styles", 0),
        ("ActionPause", "Pause", 0),
        ("ActionToggleTimingMode", "Toggle between normal and timing mode", 0),
        ("ActionToggleVO", "Show/Hide Reference VO", 0),
        ("ActionShowHideSubs", "Show/Hide subtitles", 0),
        ("ActionSetEditorFocus", "Set editor focus", 0),
        ("ActionSetEditorFocusAndSelect", "Set editor focus and select", 0),
        ("ActionNextError", "Next error", 0),
        ("ActionUndo", "Undo", 16474),
        ("ActionRedo", "Redo", 16473),
        ("ActionWAVDisplayScrollRight", "WAVDisplay scroll right", 0),
        ("ActionWAVDisplayScrollLeft", "WAVDisplay scroll left", 0),
        ("ActionShowHideSceneChange", "Show/Hide scene change", 0),
        ("ActionTextItalic", "Text in italic", 16457),
        ("ActionTextBold", "Text in bold", 0),
        ("ActionTextUnderline", "Text in underline", 0),
        ("ActionStripTags", "Strip tags", 0),
        ("ActionMerge", "Merge", 0),
        ("ActionSplitAtCursor", "Split at cursor", 0),
        ("ActionPlay1sBeforeToEnd", "Play 1s before start to end", 0),
        ("ActionDeleteSceneChange", "Delete scene change", 0),
        ("ActionReload", "Reload", 0),
        ("ActionMergeWithPrevious", "Merge with previous", 0),
        ("ActionMergeWithNext", "Merge with next", 0),
        ("ActionMergeDialog", "Merge dialog", 0),
        ("ActionFixSelectedErrors", "Fix selected errors", 0),
        ("ActionMergeOnOneLine", "Merge on 1 line", 0),
        ("ActionShowSilentZones", "Show silent zones", 0),
        ("ActionSpellCheck", "Spell check", 0),
        ("ActionInsertSceneChange", "Insert scene change", 0),
        ("ActionDelaySynchToCursor", "Shift all subtitles from current to cursor", 0),
        ("ActionDelayMoveNegative200", "Shift all subtitles from current by -200 Ms", 0),
        ("ActionDelayMoveNegative100", "Shift all subtitles from current by -100 Ms", 0),
        ("ActionDelayMoveNegative50", "Shift all subtitles from current by -50 Ms", 0),
        ("ActionDelayMoveNegative10", "Shift all subtitles from current by -10 Ms", 0),
        ("ActionDelayMovePositive10", "Shift all subtitles from current by +10 Ms", 0),
        ("ActionDelayMovePositive50", "Shift all subtitles from current by +50 Ms", 0),
        ("ActionDelayMovePositive100", "Shift all subtitles from current by +100 Ms", 0),
        ("ActionDelayMovePositive200", "Shift all subtitles from current by +200 Ms", 0),
        ("ActionScrollTextPipeUp", "Scroll Text Pipe Up", 0),
        ("ActionScrollTextPipeDown", "Scroll Text Pipe Down", 0),
        ("ActionMoveForwardToClosestSilentZone", "Move forward to closest silent zone", 0),
        ("ActionMoveBackwardToClosestSilentZone", "Move backward to closest silent zone", 0),
        ("ActionShiftForwardWaveForm", "Shift wave form forward from cursor", 24646),
        ("ActionShiftBackwardWaveForm", "Shift wave form backward to the cursor", 24642),
        ("ActionResynchToolsReset", "Reset all desynch tool checkpoints", 0),
        ("ActionResynchToolsSetPos", "Insert a new desynch tool checkpoint", 0),
        ("ActionResynchToolsDelPos", "Delete a desynch tool checkpoint", 0),
        ("ActionTextBottomLeft", "Text to bottom left", 0),
        ("ActionTextBottomMiddle", "Text to bottom middle (default)", 0),
        ("ActionTextBottomRight", "Text to bottom right", 0),
        ("ActionTextTopLeft", "Text to top left", 0),
        ("ActionTextTopMiddle", "Text to top middle", 0),
        ("ActionTextTopRight", "Text to top right", 0),
        ("ActionTextMiddleRight", "Text to middle right", 0),
        ("ActionTextMiddleLeft", "Text to middle left", 0),
        ("ActionTextMiddleMiddle", "Text to middle middle", 0),
        ("ActionMouseAntiOverlapping", "Enable / Disable mouse anti-overlapping", 0),
        ("ActionRangeStopDblClick", "Double click event on subtitle stop marker", 0),
        ("ActionRangeStartDblClick", "Double click event on subtitle start marker", 0),
    ];

    /// <summary>DefaultTimingShortcuts (PreferencesFormUnit.pas).</summary>
    private static readonly (string Name, string Shortcut)[] DefaultTimingShortcuts =
    [
        ("ActionStop", "Esc"),
        ("ActionPlay", "F1"),
        ("ActionShowHideVideo", "F4"),
        ("ActionSave", "Ctrl+S"),
        ("ActionUndo", "Ctrl+Z"),
        ("ActionRedo", "Ctrl+Y"),
    ];

    private readonly List<HotkeyEntry> _entries;

    private HotkeyTable(List<HotkeyEntry> entries) => _entries = entries;

    public IReadOnlyList<HotkeyEntry> Entries => _entries;

    /// <summary>
    /// RegisterJavascriptAction: a ação [js] entra no fim da lista (Tag = 1), com o atalho do script no
    /// modo Normal e nenhum no Timing; o [Hotkeys] do ini, se tiver a ação, vale por cima.
    /// </summary>
    public HotkeyEntry AddAction(string actionName, string caption, int normalShortcut, IniDocument? ini)
    {
        var entry = new HotkeyEntry(actionName, caption, normalShortcut, 0);
        _entries.Add(entry);
        if (ini is not null)
        {
            bool useInteger = ini.GetBool("Hotkeys", "UseIntegerShortcut", false);
            entry.NormalShortcut = Read(ini, actionName + "[Normal]", entry.NormalShortcut, useInteger);
            entry.TimingShortcut = Read(ini, actionName + "[Timing]", entry.TimingShortcut, useInteger);
        }

        return entry;
    }

    /// <summary>SetDefaultHotKeys.</summary>
    public static HotkeyTable CreateDefault() =>
        new([.. FormActions.Select(a => new HotkeyEntry(a.Name, a.Caption, a.Shortcut,
            DelphiShortcut.FromText(DefaultTimingShortcuts.FirstOrDefault(t => t.Name == a.Name).Shortcut)))]);

    public HotkeyEntry? Get(string actionName) => Entries.FirstOrDefault(e => e.ActionName == actionName);

    /// <summary>
    /// TCustomActionList.IsShortCut: a primeira ação da lista com esse atalho no modo atual. Atalho 0
    /// não corresponde a nada.
    /// </summary>
    public HotkeyEntry? Find(int shortcut, bool timingMode) =>
        shortcut == 0 ? null : Entries.FirstOrDefault(e => e.ShortcutFor(timingMode) == shortcut);

    /// <summary>TConfigObject.LoadIni, parte [Hotkeys]: em texto, ou em inteiro com UseIntegerShortcut.</summary>
    public void Load(IniDocument ini)
    {
        ArgumentNullException.ThrowIfNull(ini);
        bool useInteger = ini.GetBool("Hotkeys", "UseIntegerShortcut", false);
        foreach (var entry in Entries)
        {
            entry.NormalShortcut = Read(ini, entry.ActionName + "[Normal]", entry.NormalShortcut, useInteger);
            entry.TimingShortcut = Read(ini, entry.ActionName + "[Timing]", entry.TimingShortcut, useInteger);
        }
    }

    /// <summary>TConfigObject.SaveIni, parte [Hotkeys]: sempre em inteiro, com UseIntegerShortcut.</summary>
    public void Save(IniDocument ini)
    {
        ArgumentNullException.ThrowIfNull(ini);
        ini.SetBool("Hotkeys", "UseIntegerShortcut", true);
        foreach (var entry in Entries)
        {
            ini.SetInt("Hotkeys", entry.ActionName + "[Normal]", entry.NormalShortcut);
            ini.SetInt("Hotkeys", entry.ActionName + "[Timing]", entry.TimingShortcut);
        }
    }

    /// <summary>SaveConfig, parte Hotkeys: copia os atalhos editados de volta, ação por ação.</summary>
    public void Assign(IEnumerable<HotkeyEntry> edited)
    {
        ArgumentNullException.ThrowIfNull(edited);
        foreach (var e in edited)
        {
            if (Get(e.ActionName) is { } entry)
            {
                entry.NormalShortcut = e.NormalShortcut;
                entry.TimingShortcut = e.TimingShortcut;
            }
        }
    }

    private static int Read(IniDocument ini, string key, int current, bool useInteger)
    {
        // IniFile.ValueExists: só uma chave presente muda o atalho (vazia remove).
        if (ini.Get("Hotkeys", key) is not { } value)
        {
            return current;
        }

        return useInteger ? ini.GetInt("Hotkeys", key, current) : DelphiShortcut.FromText(value);
    }
}
