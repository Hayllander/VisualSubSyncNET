using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Timing;
using VisualSubSync.Core.Waveform;

namespace VisualSubSync.App.ViewModels;

/// <summary>
/// Modo Normal/Timing e as ações de sincronização de main.pas (bttWorkingMode, ActionToggleTimingMode,
/// SetSubtitleStartTime, SetFocusedSubtitleStopTime, Shift*, Start/StopSub, criação com Espaço).
/// O desfazer dessas ações entra com o M5 (TUndoable*Task).
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>StartSubtitleTime (ActionStartSub/ActionStopSub).</summary>
    private int _startSubtitleTime = -1;

    /// <summary>ToggleStartSubtitleTime (Espaço no modo Timing).</summary>
    private int _toggleStartSubtitleTime = -1;

    /// <summary>bttWorkingMode.Tag = 1.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkingModeCaption), nameof(WaveformSelectionMode), nameof(IsSubtitleEditionEnabled))]
    private bool _isTimingMode;

    /// <summary>PanelWAVDisplay.SetFocus das ações de reprodução no modo Timing.</summary>
    public event EventHandler? WaveformFocusRequested;

    /// <summary>ActionSetEditorFocus(AndSelect): o argumento diz se o texto é todo selecionado.</summary>
    public event EventHandler<bool>? EditorFocusRequested;

    /// <summary>AutoFitSubsListColumns, pedido quando a lista passa a ter a primeira legenda.</summary>
    public event EventHandler? AutoFitColumnsRequested;

    /// <summary>bttWorkingMode.Caption.</summary>
    public string WorkingModeCaption => IsTimingMode ? "Timing" : "Normal";

    /// <summary>WAVDisplayer.SelMode: SSA só no modo Timing com [Mouse] EnableSSATimingMode.</summary>
    public WaveformSelectionMode WaveformSelectionMode =>
        IsTimingMode && Settings.MouseEnableSsaTimingMode ? WaveformSelectionMode.Ssa : WaveformSelectionMode.CoolEdit;

    /// <summary>MemoSubtitleText.Enabled := MemoLinesCounter.Enabled and (not DisableSubtitleEdition or IsNormalMode).</summary>
    public bool IsSubtitleEditionEnabled => IsProjectOpen && (!Settings.DisableSubtitleEdition || !IsTimingMode);

    /// <summary>ActionToggleTimingModeExecute (habilitada junto com ActionPlay).</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void ToggleTimingMode() => IsTimingMode = !IsTimingMode;

    /// <summary>ActionClearSelectionExecute.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ClearSelection() => Waveform?.ClearSelection();

    /// <summary>ActionSetSubtitleTimeExecute: a legenda focada recebe a seleção (de ao menos 100 ms).</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void SetFocusedSubtitleTime()
    {
        if (SelectionIsEmpty || SelectionStopMs - SelectionStartMs < 100 || SelectedSubtitle is not { } item)
        {
            return;
        }

        DoAndPush(new SetTimeTask(this, Subtitles.IndexOf(item), item.Model.StartMs, item.Model.StopMs,
            SelectionStartMs, SelectionStopMs));
    }

    /// <summary>ActionSetSubtitleStartTimeExecute: SetSubtitleStartTime(False).</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void SetSubtitleStartTime() => SetSubtitleStartTime(setStopTime: false);

    /// <summary>ActionSetSubtitleStopTimeExecute: SetFocusedSubtitleStopTime(False, False).</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void SetSubtitleStopTime() => SetFocusedSubtitleStopTime(advance: false, keepIfSuperiorToCpsTarget: false);

    /// <summary>ActionSetSubtitleStopTimeAndGoToNextExecute: SetFocusedSubtitleStopTime(True, False).</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void SetSubtitleStopTimeAndGoToNext() => SetFocusedSubtitleStopTime(advance: true, keepIfSuperiorToCpsTarget: false);

    /// <summary>ActionShiftStart{Plus,Minus}{100,10}Execute: OffsetCurrentSubtitleStartTime.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ShiftStartTime(string? offset)
    {
        if (int.TryParse(offset, out int karaokeMs) && OffsetKaraokeMarker(karaokeMs, start: true))
        {
            return;
        }

        if (SelectedSubtitle is { } item && int.TryParse(offset, out int ms) && TimingRules.CanOffsetStart(item.Model, ms))
        {
            DoAndPush(new DelayTask(this, [Subtitles.IndexOf(item)], ms, DelayShiftType.StartTimeOnly));
        }
    }

    /// <summary>ActionShiftStop{Plus,Minus}{100,10}Execute: OffsetCurrentSubtitleStopTime.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ShiftStopTime(string? offset)
    {
        if (int.TryParse(offset, out int karaokeMs) && OffsetKaraokeMarker(karaokeMs, start: false))
        {
            return;
        }

        if (SelectedSubtitle is { } item && int.TryParse(offset, out int ms) && TimingRules.CanOffsetStop(item.Model, ms))
        {
            DoAndPush(new DelayTask(this, [Subtitles.IndexOf(item)], ms, DelayShiftType.StopTimeOnly));
        }
    }

    /// <summary>ActionSelectNextSubExecute: SelectNextSub.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void SelectNextSub() => SelectSibling(+1);

    /// <summary>ActionSelectPreviousSubExecute: SelectPreviousSub.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void SelectPreviousSub() => SelectSibling(-1);

    /// <summary>ActionPlay1sBeforeExecute: da seleção (ou da legenda focada) menos 1 s até o fim dela.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play1sBefore() => PlayOneSecondBefore(toEnd: false);

    /// <summary>ActionPlay1sBeforeToEndExecute: idem, até o fim do arquivo.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play1sBeforeToEnd() => PlayOneSecondBefore(toEnd: true);

    /// <summary>ActionWAVDisplayScrollRightExecute: Scroll(25).</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void WavDisplayScrollRight() => Waveform?.Scroll(25);

    /// <summary>ActionWAVDisplayScrollLeftExecute: Scroll(-25).</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void WavDisplayScrollLeft() => Waveform?.Scroll(-25);

    /// <summary>ActionAddSubtitleExecute: nova legenda com a seleção, já selecionada na onda.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void AddSubtitle()
    {
        DoAndPush(new AddTask(this, SelectionStartMs, SelectionStopMs, autoSelect: true));
        if (!IsTimingMode && IsSubtitleEditionEnabled)
        {
            EditorFocusRequested?.Invoke(this, false);
        }

        // Adjust column size when adding the first subtitle
        if (Subtitles.Count == 1)
        {
            AutoFitColumnsRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>ActionStartSubExecute: marca o início na posição da reprodução.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void StartSub() => _startSubtitleTime = IsPlaying ? PlayCursorMs : -1;

    /// <summary>ActionStopSubExecute: cria a legenda do início marcado até a reprodução.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void StopSub()
    {
        if (CanCreateFromStart())
        {
            AddSubtitle(_startSubtitleTime, PlayCursorMs, autoSelect: false);
        }

        _startSubtitleTime = -1;
    }

    /// <summary>ActionStopAndStartSubExecute: cria a legenda e já marca o início da próxima.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void StopAndStartSub()
    {
        if (CanCreateFromStart())
        {
            AddSubtitle(_startSubtitleTime, PlayCursorMs, autoSelect: false);
            _startSubtitleTime = PlayCursorMs + 1;
        }
        else
        {
            _startSubtitleTime = -1;
        }
    }

    /// <summary>ActionSetEditorFocusExecute.</summary>
    [RelayCommand]
    private void SetEditorFocus() => EditorFocusRequested?.Invoke(this, false);

    /// <summary>ActionSetEditorFocusAndSelectExecute.</summary>
    [RelayCommand]
    private void SetEditorFocusAndSelect() => EditorFocusRequested?.Invoke(this, true);

    /// <summary>TntFormKeyDown: Espaço no modo Timing, tocando, marca o início (e ajusta a legenda focada).</summary>
    public bool OnTimingSpaceDown()
    {
        if (!IsToggleCreationActive())
        {
            return false;
        }

        if (_toggleStartSubtitleTime == -1)
        {
            _toggleStartSubtitleTime = PlayCursorMs;
            if (Settings.SpaceKeyModifyTiming)
            {
                SetSubtitleStartTime(setStopTime: true);
            }
        }

        return true;
    }

    /// <summary>TntFormKeyUp: soltar o Espaço ajusta o fim da legenda focada ou cria uma nova.</summary>
    public bool OnTimingSpaceUp()
    {
        if (!IsToggleCreationActive())
        {
            return false;
        }

        if (_toggleStartSubtitleTime != -1)
        {
            if (Settings.SpaceKeyModifyTiming)
            {
                SetFocusedSubtitleStopTime(advance: true, keepIfSuperiorToCpsTarget: true);
            }
            else if (PlayCursorMs > _toggleStartSubtitleTime + TimingRules.MinimumCreatedDurationMs)
            {
                AddSubtitle(_toggleStartSubtitleTime, PlayCursorMs, autoSelect: false);
            }

            _toggleStartSubtitleTime = -1;
        }

        return true;
    }

    private bool IsToggleCreationActive() => IsTimingMode && Settings.EnableToggleCreation && IsPlaying;

    private bool CanCreateFromStart() =>
        IsPlaying && _startSubtitleTime != -1 && PlayCursorMs > _startSubtitleTime + TimingRules.MinimumCreatedDurationMs;

    /// <summary>if IsTimingMode then PanelWAVDisplay.SetFocus.</summary>
    private void FocusWaveformInTimingMode()
    {
        if (IsTimingMode)
        {
            WaveformFocusRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>SetSubtitleStartTime: início na reprodução (ou no cursor), espaço mínimo e reordenação.</summary>
    private void SetSubtitleStartTime(bool setStopTime)
    {
        if (SelectedSubtitle is not { } item)
        {
            return;
        }

        int index = Subtitles.IndexOf(item);
        var previous = index > 0 ? Subtitles[index - 1] : null;
        ChangeTimes(item, sort: true, () =>
        {
            TimingRules.SetStartTime(item.Model, previous?.Model, IsPlaying ? PlayCursorMs : CursorMs, setStopTime,
                Settings.SpaceKeyBlankBetweenSubtitles, Settings.SpaceKeyCpsTarget, Settings.SpaceKeyMinimalDuration);
            return true;
        });
        previous?.RefreshTimes();
    }

    /// <summary>SetFocusedSubtitleStopTime: fim na reprodução (ou no cursor) e, se pedido, foco na próxima.</summary>
    private void SetFocusedSubtitleStopTime(bool advance, bool keepIfSuperiorToCpsTarget)
    {
        if (SelectedSubtitle is not { } item)
        {
            return;
        }

        var result = StopTimeResult.Aborted;
        ChangeTimes(item, sort: false, () =>
        {
            result = TimingRules.SetStopTime(item.Model, IsPlaying ? PlayCursorMs : CursorMs, IsPlaying,
                keepIfSuperiorToCpsTarget, Settings.SpaceKeyCpsTarget, Settings.SpaceKeyMinimalDuration);
            return result == StopTimeResult.Updated;
        });
        if (result != StopTimeResult.Aborted && advance)
        {
            // AdvanceToNextSubtitleAfterFocus: só o foco da lista muda.
            int index = Subtitles.IndexOf(item);
            if (index + 1 < Subtitles.Count)
            {
                SelectedSubtitle = Subtitles[index + 1];
            }
        }
    }

    /// <summary>
    /// Aplica uma mudança de tempos na legenda: a lista é reordenada quando pedido
    /// (FullSortTreeAndSubList) e o projeto fica alterado. SetSubtitleStartTime e
    /// SetFocusedSubtitleStopTime não mexem na seleção (nem no trecho em reprodução) e não geram tarefa
    /// de desfazer, como no original.
    /// </summary>
    private void ChangeTimes(SubtitleItemViewModel item, bool sort, Func<bool> change)
    {
        if (!change())
        {
            return;
        }

        if (sort)
        {
            SortSubtitles();
        }

        item.RefreshTimes();
        IsDirty = true;
        RefreshRanges();
        UpdateDurationWithSubOnly();
        UpdateEditorStatus(_caret.Line, _caret.Column, _caret.FirstVisible);
    }

    /// <summary>
    /// AddSubtitle(StartTime, StopTime, '', True, AutoSelect): insere na ordem (TRangeList.FindInsertPos),
    /// foca a nova legenda e, com AutoSelect no modo CoolEdit, prende a seleção a ela; devolve o índice.
    /// </summary>
    internal int AddSubtitle(int startMs, int stopMs, bool autoSelect, string text = "")
    {
        int index = InsertSubtitle(new Subtitle { StartMs = startMs, StopMs = stopMs, Text = text });

        // FocusNode(NewNode, AutoSelect).
        SelectedSubtitle = Subtitles[index];
        if (autoSelect && WaveformSelectionMode == WaveformSelectionMode.CoolEdit)
        {
            Waveform?.SelectRange(startMs, stopMs);
        }

        return index;
    }

    /// <summary>TRangeList.FindInsertPos(Range): busca binária por início e depois fim.</summary>
    private int InsertPosition(int startMs, int stopMs)
    {
        int min = 0, max = _document.Items.Count - 1;
        while (min <= max)
        {
            int mid = (min + max) / 2;
            var s = _document.Items[mid];
            int c = s.StartMs != startMs ? s.StartMs.CompareTo(startMs) : s.StopMs.CompareTo(stopMs);
            if (c < 0)
            {
                min = mid + 1;
            }
            else if (c > 0)
            {
                max = mid - 1;
            }
            else
            {
                return mid;
            }
        }

        return min;
    }

    /// <summary>SelectNextSub/SelectPreviousSub: para a reprodução e seleciona a vizinha (ou a mesma).</summary>
    private void SelectSibling(int step)
    {
        if (SelectSiblingKaraoke(step))
        {
            return;
        }

        if (Subtitles.Count == 0)
        {
            return;
        }

        int current = SelectedSubtitle is { } s ? Subtitles.IndexOf(s) : 0;
        _engine?.Stop();
        int next = current + step;
        SelectNodeAtIndex(next >= 0 && next < Subtitles.Count ? next : current);
    }

    private void PlayOneSecondBefore(bool toEnd)
    {
        int start, stop;
        if (!SelectionIsEmpty)
        {
            (start, stop) = (SelectionStartMs, SelectionStopMs);
        }
        else if (SelectedSubtitle is { } item)
        {
            (start, stop) = (item.Model.StartMs, item.Model.StopMs);
        }
        else
        {
            return;
        }

        _playingMode = PlayingModeType.Selection;
        PlayRange(Math.Max(start - 1000, 0), toEnd ? LengthMs : stop, loop: false);
    }

    /// <summary>UpdateDurationWithSubOnly: sem picos nem vídeo, a duração é a última legenda + 1 minuto.</summary>
    private void UpdateDurationWithSubOnly()
    {
        if (Peaks is null && !IsVideoOpen && _document.Items.Count > 0)
        {
            LengthMs = _document.Items[^1].StopMs + 60_000;
        }
    }

}
