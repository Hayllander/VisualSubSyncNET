using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Waveform;

/// <summary>TSelMode: smCoolEdit (padrão) ou smSSA (modo Timing com "EnableSSATimingMode").</summary>
public enum WaveformSelectionMode
{
    CoolEdit,
    Ssa,
}

/// <summary>TDynamicEditMode: borda de início, de fim ou um marcador de karaokê sob o mouse.</summary>
public enum DynamicEditMode
{
    None,
    Start,
    Stop,
    Karaoke,
}

/// <summary>Botões e teclas de TShiftState que o mouse da forma de onda usa.</summary>
[Flags]
public enum WaveformInput
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4,
    Shift = 8,
    Ctrl = 16,
}

/// <summary>Uma legenda na forma de onda (TRange: StartTime, StopTime e os marcadores SubTime).</summary>
public readonly record struct TimeRange(int StartMs, int StopMs)
{
    private readonly int[]? _subTimes;

    /// <summary>TRange.SubTime: os marcadores de karaokê, em ordem.</summary>
    public int[] SubTimes
    {
        get => _subTimes ?? [];
        init => _subTimes = value;
    }
}

/// <summary>TMinBlankInfo: o espaço mínimo antes do início ou depois do fim de uma legenda vizinha.</summary>
public readonly record struct MinBlankInfo(int RangeIndex, bool IsStart)
{
    public static readonly MinBlankInfo None = new(-1, false);

    public bool Exists => RangeIndex >= 0;

    public int GetStart(TimeRange range, int minBlank) => IsStart ? range.StartMs - minBlank : range.StopMs;

    public int GetStop(TimeRange range, int minBlank) => IsStart ? range.StartMs : range.StopMs + minBlank;

    public int GetSnappingPoint(TimeRange range, int minBlank) => IsStart ? GetStart(range, minBlank) : GetStop(range, minBlank);
}

/// <summary>
/// Comportamento do mouse de TWAVDisplayer (WAVDisplayerUnit.pas): MouseDownCoolEdit, MouseDownSSA,
/// MouseMove (incluindo a "seleção dinâmica" das bordas), MouseUp, DblClick, FindSnappingPoint e o
/// bloqueio de sobreposição. Não depende de WPF: o controle informa a vista e os eventos do mouse em
/// pixels, e esta classe devolve cursor, seleção e alterações das legendas pelos eventos, com os
/// mesmos nomes do original.
/// </summary>
public sealed class WaveformMouse
{
    private const int SnappingDistancePixel = 8; // SNAPPING_DISTANCE_PIXEL
    private const int MinimumSelection = 40;      // MouseUp: seleção menor que 40 ms é desfeita

    private readonly List<TimeRange> _ranges = [];
    private readonly List<TimeRange> _voRanges = []; // FRangeListVO (Editable = False)
    private bool _inVo; // GetDisplayRangeListAt(Y) devolveu a lista do VO
    private int _selectionOrigin = -1; // FSelectionOrigin
    private int _minSelTime = -1, _maxSelTime = -1;
    private bool _needToSortSelectedSub;
    private int _rangeOldStart = -1, _rangeOldStop = -1;
    private int _dynamicRangeIndex = NoRange; // FDynamicSelRange
    private bool _dynamicIsSelection;
    private int _scrollOrigin = -1;
    private int _oldKaraokeSubTime = -1; // FOldKaraokeSubTime

    /// <summary>Índice "nenhuma legenda" (nil no original).</summary>
    public const int NoRange = -1;

    // ---------------------------------------------------------------- vista (informada pelo controle)

    public int PositionMs { get; set; }

    public int PageSizeMs { get; set; }

    public int LengthMs { get; set; }

    /// <summary>Largura da área da onda, em pixels do dispositivo.</summary>
    public int WidthPx { get; set; }

    /// <summary>GetWavCanvasHeight, em pixels do dispositivo.</summary>
    public int WavHeightPx { get; set; }

    /// <summary>Pixels do dispositivo por pixel a 96 DPI: as distâncias do original (8, 4 e 2 px) são escaladas.</summary>
    public double Scale { get; set; } = 1;

    // ---------------------------------------------------------------- preferências

    public WaveformSelectionMode SelectionMode { get; set; } = WaveformSelectionMode.CoolEdit;

    /// <summary>EnableMouseSnapping (padrão verdadeiro).</summary>
    public bool SnappingEnabled { get; set; } = true;

    /// <summary>EnableMouseAntiOverlapping (padrão falso).</summary>
    public bool AntiOverlapping { get; set; }

    /// <summary>MinimumBlank = SpaceKeyBlankBetweenSubtitles (padrão 120 ms).</summary>
    public int MinimumBlankMs { get; set; } = 120;

    public bool SceneChangeEnabled { get; set; }

    public int SceneChangeStartOffset { get; set; } = 130;

    public int SceneChangeStopOffset { get; set; } = 130;

    public int SceneChangeFilterOffset { get; set; } = 250;

    /// <summary>FSceneChangeList, em ordem crescente.</summary>
    public IReadOnlyList<int> SceneChanges { get; set; } = [];

    // ---------------------------------------------------------------- estado

    /// <summary>FRangeList, ordenada como TRangeList (início e depois fim).</summary>
    public IReadOnlyList<TimeRange> Ranges => _ranges;

    public int CursorMs { get; private set; }

    public int SelectionStartMs { get; private set; }

    /// <summary>0 = sem seleção (SelectionIsEmpty).</summary>
    public int SelectionStopMs { get; private set; }

    /// <summary>FSelectedRange: a legenda presa à seleção, ou <see cref="NoRange"/>.</summary>
    public int SelectedRangeIndex { get; private set; } = NoRange;

    public DynamicEditMode DynamicEditMode { get; private set; }

    /// <summary>Tempo da borda sob o mouse na seleção dinâmica (FDynamicEditTime).</summary>
    public int DynamicEditTime { get; private set; }

    /// <summary>Legenda cuja borda está sob o mouse (desenhada com linha cheia), ou <see cref="NoRange"/>.</summary>
    public int DynamicRangeIndex => _dynamicIsSelection ? SelectedRangeIndex : _dynamicRangeIndex;

    public bool MouseIsDown { get; private set; }

    /// <summary>Verdadeiro durante o arraste com o botão do meio (Cursor = crHandPoint).</summary>
    public bool IsScrolling => _scrollOrigin != -1;

    /// <summary>FSelectedKaraokeIndex: o trecho de karaokê selecionado (0 = do início ao primeiro marcador), ou -1.</summary>
    public int SelectedKaraokeIndex { get; private set; } = -1;

    /// <summary>FSelectedKaraokeRange: a legenda do trecho de karaokê selecionado, ou <see cref="NoRange"/>.</summary>
    public int SelectedKaraokeRangeIndex { get; private set; } = NoRange;

    public MinBlankInfo MinBlank1 { get; private set; } = MinBlankInfo.None;

    public MinBlankInfo MinBlank2 { get; private set; } = MinBlankInfo.None;

    public bool SelectionIsEmpty => SelectionStopMs == 0;

    // ---------------------------------------------------------------- eventos (nomes do original)

    /// <summary>OnCursorChange.</summary>
    public event EventHandler? CursorChanged;

    /// <summary>OnSelectionChange.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>OnSelectedRange(Range, IsDynamic): o duplo clique (ou a edição pela borda) escolheu uma legenda.</summary>
    public event EventHandler<SelectedRangeInfo>? RangeSelected;

    /// <summary>OnSelectedRangeChange: os tempos da legenda selecionada mudaram durante o arraste.</summary>
    public event EventHandler<RangeTimesChange>? SelectedRangeTimesChanging;

    /// <summary>OnSelectedRangeChanged(OldStart, OldStop, NeedSort): fim do arraste, a lista precisa ser reordenada.</summary>
    public event EventHandler<RangeTimesChange>? SelectedRangeTimesChanged;

    /// <summary>Um marcador de karaokê está sendo arrastado (o original muda o TRange na hora).</summary>
    public event EventHandler<KaraokeChange>? KaraokeTimeChanging;

    /// <summary>OnKaraokeChanged(Range, SubTimeIndex, OldTime): fim do arraste de um marcador.</summary>
    public event EventHandler<KaraokeChange>? KaraokeTimeChanged;

    /// <summary>OnSelectedKaraokeRange: um trecho de karaokê foi selecionado (índice da legenda) ou solto (-1).</summary>
    public event EventHandler<int>? KaraokeRangeSelected;

    /// <summary>OnRangeStartDblClick / OnRangeStopDblClick (usados pelos plugins).</summary>
    public event EventHandler<RangeEdgeInfo>? RangeEdgeDoubleClicked;

    /// <summary>Pedido de Windows.SetCursorPos para o X dado (pixels do dispositivo, relativos ao controle).</summary>
    public event EventHandler<int>? MoveMouseRequested;

    /// <summary>Pedido de ClipCursor entre os X dados; nulo libera o mouse.</summary>
    public event EventHandler<(int Left, int Right)?>? ClipMouseRequested;

    /// <summary>Pedido de rolagem da vista (botão do meio): nova posição.</summary>
    public event EventHandler<int>? ScrollRequested;

    // ---------------------------------------------------------------- dados

    /// <summary>Troca a lista de legendas (já ordenada). Mantém o índice selecionado quando ainda é válido.</summary>
    public void SetRanges(IEnumerable<TimeRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        _ranges.Clear();
        _ranges.AddRange(ranges);
        if (SelectedRangeIndex >= _ranges.Count)
        {
            SelectedRangeIndex = NoRange;
        }

        if (_dynamicRangeIndex >= _ranges.Count)
        {
            _dynamicRangeIndex = NoRange;
        }

        if (SelectedKaraokeRangeIndex >= _ranges.Count)
        {
            SelectedKaraokeRangeIndex = NoRange;
            SelectedKaraokeIndex = -1;
        }

        MinBlank1 = Valid(MinBlank1);
        MinBlank2 = Valid(MinBlank2);
    }

    /// <summary>FRangeListVO: as legendas de referência (VO), só para mostrar.</summary>
    public void SetVoRanges(IEnumerable<TimeRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        _voRanges.Clear();
        _voRanges.AddRange(ranges);
    }

    /// <summary>ShowVO: a lista do VO está em FDisplayRangeLists (faixa de cima).</summary>
    public bool ShowVO { get; set; }

    /// <summary>
    /// GetDisplayRangeListAt: com o VO à mostra, a área da onda tem duas faixas de mesma altura; a de
    /// cima é a do VO.
    /// </summary>
    public bool IsVoBandAt(int y)
    {
        if (!ShowVO || WavHeightPx <= 0)
        {
            return false;
        }

        int i = Math.Clamp((int)Math.Floor(y / (WavHeightPx / 2.0)), 0, 1);
        return i == 0;
    }

    /// <summary>A lista sob o mouse (ARangeList do original).</summary>
    private List<TimeRange> MouseList => _inVo ? _voRanges : _ranges;

    /// <summary>SetCursorPos (sem disparar o evento: quem chama já sabe).</summary>
    public void SetCursor(int ms) => CursorMs = Math.Clamp(ms, 0, Math.Max(0, LengthMs));

    /// <summary>Seleção definida de fora (campos de tempo, zoom), sem legenda presa.</summary>
    public void SetSelection(int startMs, int stopMs)
    {
        SelectionStartMs = startMs;
        SelectionStopMs = stopMs;
        if (SelectedRangeIndex != NoRange && (_ranges[SelectedRangeIndex].StartMs != startMs || _ranges[SelectedRangeIndex].StopMs != stopMs))
        {
            SelectedRangeIndex = NoRange;
        }
    }

    /// <summary>
    /// SetSelectedRangeEx: prende a seleção a uma legenda (ou a solta, zerando a seleção) e dispara
    /// OnSelectionChange quando muda.
    /// </summary>
    public void SetSelectedRange(int index)
    {
        if (index < 0 || index >= _ranges.Count)
        {
            index = NoRange;
        }

        if (index == NoRange)
        {
            SelectionStartMs = 0;
            SelectionStopMs = 0;
        }

        if (SelectedRangeIndex != index)
        {
            SelectedRangeIndex = index;
            if (index != NoRange)
            {
                SelectionStartMs = _ranges[index].StartMs;
                SelectionStopMs = _ranges[index].StopMs;
                SetMinBlankAt(_ranges[index].StartMs);
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>ClearSelection.</summary>
    public void ClearSelection()
    {
        SetSelectedRange(NoRange);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---------------------------------------------------------------- conversões

    public int PixelToTime(int pixel) =>
        PageSizeMs == 0 || WidthPx == 0 ? 0 : (int)Math.Round(pixel * (PageSizeMs / (double)WidthPx));

    public int TimeToPixel(int time) =>
        PageSizeMs == 0 || WidthPx == 0 ? 0 : (int)Math.Round(time / (PageSizeMs / (double)WidthPx));

    private int Px(int pixels96) => Math.Max(1, (int)Math.Round(pixels96 * Scale));

    // ---------------------------------------------------------------- mouse

    /// <summary>TWAVDisplayer.MouseDown.</summary>
    public void MouseDown(WaveformInput input, int x, int y)
    {
        if (LengthMs == 0 || x < 0 || x > WidthPx)
        {
            return;
        }

        MouseIsDown = true;
        _inVo = IsVoBandAt(y);
        if (SelectionMode == WaveformSelectionMode.CoolEdit)
        {
            MouseDownCoolEdit(input, x);
        }
        else
        {
            MouseDownSsa(input, x);
        }

        if (input.HasFlag(WaveformInput.Middle))
        {
            _scrollOrigin = PixelToTime(x);
        }
    }

    /// <summary>TWAVDisplayer.MouseMove.</summary>
    public void MouseMove(WaveformInput input, int x, int y)
    {
        if (LengthMs == 0)
        {
            return;
        }

        _inVo = IsVoBandAt(y);
        if (MouseIsDown)
        {
            if (SelectionMode == WaveformSelectionMode.CoolEdit)
            {
                if (input.HasFlag(WaveformInput.Left))
                {
                    DragCoolEdit(input, Math.Clamp(x, 0, WidthPx));
                }
            }
            else if (input.HasFlag(WaveformInput.Left) || input.HasFlag(WaveformInput.Right))
            {
                DragSsa(input, Math.Clamp(x, 0, WidthPx));
            }

            if (input.HasFlag(WaveformInput.Middle) && _scrollOrigin != -1)
            {
                // Rolagem "pixel a pixel"; com Shift, 4 vezes mais rápida.
                int multiplier = input.HasFlag(WaveformInput.Shift) ? 4 : 1;
                int diff = PixelToTime(x) - _scrollOrigin;
                ScrollRequested?.Invoke(this, PositionMs - (diff * multiplier));
                _scrollOrigin = PixelToTime(x);
            }

            return;
        }

        if (_inVo)
        {
            // Lista não editável: só apaga o espaço mínimo e a seleção dinâmica.
            SetMinBlankAt(-1);
            DynamicEditMode = DynamicEditMode.None;
            _dynamicRangeIndex = NoRange;
            _dynamicIsSelection = false;
            return;
        }

        int cursorPos = PixelToTime(Math.Clamp(x, 0, WidthPx)) + PositionMs;
        // "Seleção dinâmica": só no modo CoolEdit e sem botão nem tecla (Shift = []).
        if (SelectionMode == WaveformSelectionMode.CoolEdit && input == WaveformInput.None)
        {
            // Primeira passada: só dentro das legendas.
            int window = Math.Max(1, PixelToTime(Px(4)));
            foreach (int i in FindRangesAt(cursorPos, 0))
            {
                if (CheckSubtitleForDynamicSelection(i, _ranges[i], cursorPos, window, y))
                {
                    return;
                }
            }

            // Segunda passada: busca mais larga.
            window = Math.Max(1, PixelToTime(Px(2)));
            foreach (int i in FindRangesAt(cursorPos, window))
            {
                if (CheckSubtitleForDynamicSelection(i, _ranges[i], cursorPos, window, y))
                {
                    return;
                }
            }

            if (!SelectionIsEmpty)
            {
                window = Math.Max(1, PixelToTime(Px(4)));
                if (CheckSubtitleForDynamicSelection(-2, new TimeRange(SelectionStartMs, SelectionStopMs), cursorPos, window, y))
                {
                    return;
                }
            }
        }

        SetMinBlankAt(cursorPos);
        DynamicEditMode = DynamicEditMode.None;
        _dynamicRangeIndex = NoRange;
        _dynamicIsSelection = false;
    }

    /// <summary>TWAVDisplayer.MouseUp.</summary>
    public void MouseUp()
    {
        if (SelectionStopMs - SelectionStartMs < MinimumSelection && SelectedRangeIndex == NoRange && DynamicRangeIndex == NoRange &&
            SelectedKaraokeRangeIndex == NoRange)
        {
            ClearSelection();
        }

        if (_needToSortSelectedSub)
        {
            if (SelectedRangeIndex != NoRange)
            {
                var r = _ranges[SelectedRangeIndex];
                var args = new RangeTimesChange(SelectedRangeIndex, r.StartMs, r.StopMs, _rangeOldStart, _rangeOldStop);
                FullSort();
                SelectedRangeTimesChanged?.Invoke(this, args);
            }
            else
            {
                FullSort();
            }

            _needToSortSelectedSub = false;
        }

        if (DynamicEditMode == DynamicEditMode.Karaoke && DynamicRangeIndex != NoRange &&
            DynamicEditTime < _ranges[DynamicRangeIndex].SubTimes.Length)
        {
            KaraokeTimeChanged?.Invoke(this, new KaraokeChange(DynamicRangeIndex, DynamicEditTime,
                _ranges[DynamicRangeIndex].SubTimes[DynamicEditTime], _oldKaraokeSubTime));
        }

        _selectionOrigin = -1;
        _scrollOrigin = -1;
        _rangeOldStart = -1;
        _rangeOldStop = -1;
        _oldKaraokeSubTime = -1;
        MouseIsDown = false;
        DynamicEditMode = DynamicEditMode.None;
        _dynamicRangeIndex = NoRange;
        _dynamicIsSelection = false;
        _minSelTime = -1;
        _maxSelTime = -1;
        ClipMouseRequested?.Invoke(this, null);
    }

    /// <summary>TWAVDisplayer.DblClick: no meio da altura de uma legenda com karaokê, seleciona o trecho.</summary>
    public void DoubleClick(int y)
    {
        if (LengthMs == 0 || SelectionMode == WaveformSelectionMode.Ssa || IsVoBandAt(y))
        {
            return;
        }

        if (DynamicRangeIndex != NoRange && DynamicEditMode is DynamicEditMode.Start or DynamicEditMode.Stop)
        {
            // O legado entrega aos plugins e não muda a seleção.
            RangeEdgeDoubleClicked?.Invoke(this, new RangeEdgeInfo(DynamicRangeIndex, DynamicEditMode == DynamicEditMode.Start));
            return;
        }

        int idx = GetRangeIdxAt(CursorMs);
        if (idx == NoRange)
        {
            return;
        }

        var range = _ranges[idx];
        if (range.SubTimes.Length > 0 && IsInKaraokeBand(y))
        {
            SelectedKaraokeIndex = Karaoke.GetSubTimeRange(range.StartMs, range.StopMs, range.SubTimes, CursorMs, out int kStart, out int kStop);
            SelectionStartMs = kStart;
            SelectionStopMs = kStop;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            SelectedRangeIndex = NoRange;
            SelectedKaraokeRangeIndex = idx;
            KaraokeRangeSelected?.Invoke(this, idx);
            return;
        }

        SetSelectedRange(idx);
        RangeSelected?.Invoke(this, new SelectedRangeInfo(idx, IsDynamic: false));
    }

    private void MouseDownCoolEdit(WaveformInput input, int x)
    {
        if (!input.HasFlag(WaveformInput.Left))
        {
            return;
        }

        if (DynamicEditMode == DynamicEditMode.Karaoke && DynamicRangeIndex != NoRange)
        {
            var range = _ranges[DynamicRangeIndex];
            if (DynamicEditTime >= 0 && DynamicEditTime < range.SubTimes.Length)
            {
                _oldKaraokeSubTime = range.SubTimes[DynamicEditTime];
                MoveMouseRequested?.Invoke(this, TimeToPixel(_oldKaraokeSubTime - PositionMs));
                // O mouse fica entre o marcador anterior (ou o início) e o seguinte (ou o fim).
                int x1 = TimeToPixel((DynamicEditTime == 0 ? range.StartMs : range.SubTimes[DynamicEditTime - 1]) - PositionMs);
                int x2 = TimeToPixel((DynamicEditTime < range.SubTimes.Length - 1 ? range.SubTimes[DynamicEditTime + 1] : range.StopMs) - PositionMs);
                ClipMouseRequested?.Invoke(this, (x1 + Px(3), x2 - Px(2)));
            }

            return;
        }

        ClearKaraokeSelection();

        // Reajusta o mouse sobre a borda que vai ser arrastada.
        if (DynamicEditMode is DynamicEditMode.Start or DynamicEditMode.Stop)
        {
            x = TimeToPixel(DynamicEditTime - PositionMs);
            MoveMouseRequested?.Invoke(this, x);
        }

        int newCursorPos = PixelToTime(x) + PositionMs;
        if (!input.HasFlag(WaveformInput.Ctrl) && SnappingEnabled)
        {
            int snap = FindCorrectedSnappingPoint(newCursorPos);
            if (snap != -1)
            {
                newCursorPos = snap;
            }
        }

        if (input.HasFlag(WaveformInput.Shift) || DynamicEditMode is DynamicEditMode.Start or DynamicEditMode.Stop)
        {
            int dynamicIndex = DynamicRangeIndex;
            if (dynamicIndex != NoRange && dynamicIndex != SelectedRangeIndex)
            {
                SetSelectedRangeSilently(dynamicIndex);
                RangeSelected?.Invoke(this, new SelectedRangeInfo(dynamicIndex, IsDynamic: true));
            }

            // Shift (ou a borda): mexe no lado da seleção mais próximo.
            if (newCursorPos > SelectionStartMs + ((SelectionStopMs - SelectionStartMs) / 2))
            {
                if (SelectionIsEmpty)
                {
                    if (newCursorPos > CursorMs)
                    {
                        SelectionStopMs = newCursorPos;
                        SelectionStartMs = CursorMs;
                    }
                    else
                    {
                        SelectionStopMs = CursorMs;
                        SelectionStartMs = newCursorPos;
                    }
                }
                else
                {
                    SelectionStopMs = newCursorPos;
                }

                _selectionOrigin = SelectionStartMs;
            }
            else
            {
                SelectionStartMs = newCursorPos;
                _selectionOrigin = SelectionStopMs;
            }

            if (SelectedRangeIndex != NoRange)
            {
                _needToSortSelectedSub = true;
                _rangeOldStart = _ranges[SelectedRangeIndex].StartMs;
                _rangeOldStop = _ranges[SelectedRangeIndex].StopMs;
                _ranges[SelectedRangeIndex] = _ranges[SelectedRangeIndex] with { StartMs = SelectionStartMs, StopMs = SelectionStopMs };
                if (DynamicEditMode == DynamicEditMode.None)
                {
                    RaiseTimesChanging();
                }
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            bool hadSelection = SelectionStartMs != SelectionStopMs;
            _selectionOrigin = newCursorPos;
            SetSelectedRangeSilently(NoRange);
            if (hadSelection)
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        if (AntiOverlapping)
        {
            ClipToNeighbours(newCursorPos);
        }

        if (CursorMs != newCursorPos && DynamicEditMode == DynamicEditMode.None)
        {
            CursorMs = newCursorPos;
            CursorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>EnableMouseAntiOverlapping: o mouse não passa da legenda anterior nem da seguinte.</summary>
    private void ClipToNeighbours(int newCursorPos)
    {
        int x1 = 0, x2 = WidthPx;
        _minSelTime = -1;
        _maxSelTime = -1;
        var list = MouseList;
        int i = FindInsertPos(list, newCursorPos, -1);
        if (i >= 0)
        {
            if (SelectedRangeIndex != NoRange)
            {
                var selected = _ranges[SelectedRangeIndex];
                if (newCursorPos == selected.StartMs)
                {
                    if (i > 0)
                    {
                        _minSelTime = list[i - 1].StopMs + 1;
                        x1 = TimeToPixel(_minSelTime - PositionMs);
                    }

                    _maxSelTime = selected.StopMs - 1;
                    x2 = TimeToPixel(_maxSelTime - PositionMs);
                }
                else
                {
                    _minSelTime = selected.StartMs + 1;
                    x1 = TimeToPixel(_minSelTime - PositionMs);
                    if (i < list.Count)
                    {
                        _maxSelTime = list[i].StartMs - 1;
                        x2 = TimeToPixel(_maxSelTime - PositionMs);
                    }
                }
            }
            else if (!(i > 0 && newCursorPos >= list[i - 1].StartMs && newCursorPos <= list[i - 1].StopMs))
            {
                // Seleção só FORA das legendas (dentro de uma legenda não há limite).
                if (i > 0)
                {
                    _minSelTime = list[i - 1].StopMs + 1;
                    x1 = TimeToPixel(_minSelTime - PositionMs);
                }

                if (i < list.Count)
                {
                    _maxSelTime = list[i].StartMs - 1;
                    x2 = TimeToPixel(_maxSelTime - PositionMs);
                }
            }
        }

        x1 = Math.Clamp(x1, 0, WidthPx);
        x2 = Math.Clamp(x2, 0, WidthPx);

        // Deixa o mouse ir um pouco além; o corte de verdade é por _minSelTime/_maxSelTime.
        ClipMouseRequested?.Invoke(this, (x1 - Px(2), x2 + Px(3)));
    }

    private void MouseDownSsa(WaveformInput input, int x)
    {
        int newCursorPos = PixelToTime(x) + PositionMs;
        ClearKaraokeSelection();
        if (!input.HasFlag(WaveformInput.Ctrl) && SnappingEnabled)
        {
            int snap = FindCorrectedSnappingPoint(newCursorPos);
            if (snap != -1)
            {
                newCursorPos = snap;
            }
        }

        if (input.HasFlag(WaveformInput.Left))
        {
            if (SelectionIsEmpty)
            {
                if (newCursorPos < CursorMs)
                {
                    SelectionStartMs = newCursorPos;
                    SelectionStopMs = CursorMs;
                    _selectionOrigin = CursorMs;
                }
                else
                {
                    _selectionOrigin = newCursorPos;
                }
            }
            else if (newCursorPos < SelectionStopMs)
            {
                SelectionStartMs = newCursorPos;
                _selectionOrigin = SelectionStopMs;
            }
            else
            {
                _selectionOrigin = newCursorPos;
                SetSelectedRangeSilently(NoRange);
            }
        }

        if (input.HasFlag(WaveformInput.Right) && !input.HasFlag(WaveformInput.Shift))
        {
            if (SelectionIsEmpty)
            {
                if (newCursorPos > CursorMs)
                {
                    SelectionStopMs = newCursorPos;
                    SelectionStartMs = CursorMs;
                    _selectionOrigin = CursorMs;
                }
                else
                {
                    _selectionOrigin = newCursorPos;
                }
            }
            else if (newCursorPos > SelectionStartMs)
            {
                SelectionStopMs = newCursorPos;
                _selectionOrigin = SelectionStartMs;
            }
            else
            {
                _selectionOrigin = newCursorPos;
                SetSelectedRangeSilently(NoRange);
            }
        }

        if (input.HasFlag(WaveformInput.Right) || input.HasFlag(WaveformInput.Left))
        {
            if (SelectedRangeIndex != NoRange)
            {
                _needToSortSelectedSub = true;
                _rangeOldStart = _ranges[SelectedRangeIndex].StartMs;
                _rangeOldStop = _ranges[SelectedRangeIndex].StopMs;
                _ranges[SelectedRangeIndex] = _ranges[SelectedRangeIndex] with { StartMs = SelectionStartMs, StopMs = SelectionStopMs };
                RaiseTimesChanging();
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        if (CursorMs != newCursorPos && !input.HasFlag(WaveformInput.Middle))
        {
            CursorMs = newCursorPos;
            CursorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DragCoolEdit(WaveformInput input, int x)
    {
        int newCursorPos = PixelToTime(x) + PositionMs;
        if (_minSelTime != -1)
        {
            newCursorPos = Math.Max(newCursorPos, _minSelTime);
        }

        if (_maxSelTime != -1)
        {
            newCursorPos = Math.Clamp(newCursorPos, 0, _maxSelTime);
        }

        if (!input.HasFlag(WaveformInput.Ctrl) && DynamicEditMode != DynamicEditMode.Karaoke && SnappingEnabled)
        {
            int snap = FindCorrectedSnappingPoint(newCursorPos);
            if (snap != -1)
            {
                newCursorPos = snap;
            }
        }

        if (DynamicEditMode == DynamicEditMode.Karaoke && DynamicRangeIndex != NoRange)
        {
            var range = _ranges[DynamicRangeIndex];
            if (DynamicEditTime >= 0 && DynamicEditTime < range.SubTimes.Length)
            {
                range.SubTimes[DynamicEditTime] = newCursorPos;
                if (SelectedKaraokeIndex != -1 &&
                    Karaoke.GetSubTimeRangeAt(range.StartMs, range.StopMs, range.SubTimes, SelectedKaraokeIndex, out int kStart, out int kStop))
                {
                    // A seleção acompanha o trecho de karaokê selecionado.
                    SelectionStartMs = kStart;
                    SelectionStopMs = kStop;
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }

                KaraokeTimeChanging?.Invoke(this, new KaraokeChange(DynamicRangeIndex, DynamicEditTime, newCursorPos, _oldKaraokeSubTime));
            }
        }

        if (_selectionOrigin != -1 && _selectionOrigin != newCursorPos)
        {
            UpdateSelectionFromOrigin(newCursorPos, onlyWhenChanged: true);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        if (CursorMs != newCursorPos && DynamicEditMode == DynamicEditMode.None)
        {
            CursorMs = newCursorPos;
            CursorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DragSsa(WaveformInput input, int x)
    {
        int newCursorPos = PixelToTime(x) + PositionMs;
        if (!input.HasFlag(WaveformInput.Ctrl) && SnappingEnabled)
        {
            int snap = FindCorrectedSnappingPoint(newCursorPos);
            if (snap != -1)
            {
                newCursorPos = snap;
            }
        }

        if (CursorMs == newCursorPos)
        {
            return;
        }

        CursorMs = newCursorPos;
        if (_selectionOrigin != -1)
        {
            UpdateSelectionFromOrigin(CursorMs, onlyWhenChanged: false);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        CursorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSelectionFromOrigin(int pos, bool onlyWhenChanged)
    {
        if (pos > _selectionOrigin)
        {
            SelectionStartMs = _selectionOrigin;
            SelectionStopMs = pos;
        }
        else
        {
            SelectionStartMs = pos;
            SelectionStopMs = _selectionOrigin;
        }

        if (SelectedRangeIndex == NoRange)
        {
            return;
        }

        _needToSortSelectedSub = true;
        var r = _ranges[SelectedRangeIndex];
        if (!onlyWhenChanged || r.StartMs != SelectionStartMs || r.StopMs != SelectionStopMs)
        {
            _ranges[SelectedRangeIndex] = _ranges[SelectedRangeIndex] with { StartMs = SelectionStartMs, StopMs = SelectionStopMs };
            RaiseTimesChanging();
        }
    }

    private void RaiseTimesChanging()
    {
        var r = _ranges[SelectedRangeIndex];
        SelectedRangeTimesChanging?.Invoke(this, new RangeTimesChange(SelectedRangeIndex, r.StartMs, r.StopMs, _rangeOldStart, _rangeOldStop));
    }

    /// <summary>SetSelectedRangeEx(Value, False) sem disparar OnSelectionChange (quem chama dispara depois).</summary>
    private void SetSelectedRangeSilently(int index)
    {
        if (index == NoRange)
        {
            SelectionStartMs = 0;
            SelectionStopMs = 0;
        }

        if (SelectedRangeIndex != index)
        {
            SelectedRangeIndex = index;
            if (index != NoRange)
            {
                SelectionStartMs = _ranges[index].StartMs;
                SelectionStopMs = _ranges[index].StopMs;
                SetMinBlankAt(_ranges[index].StartMs);
            }
        }
    }

    /// <summary>CheckSubtitleForDynamicSelection: o mouse sobre a borda de uma legenda (ou da seleção, índice -2).</summary>
    private bool CheckSubtitleForDynamicSelection(int index, TimeRange range, int cursorPosMs, int window, int y)
    {
        var mode = DynamicEditMode.None;
        if ((range.StopMs - range.StartMs) / (double)window > 2 && y > 0 && y < WavHeightPx)
        {
            if (Math.Abs(cursorPosMs - range.StartMs) < window)
            {
                mode = DynamicEditMode.Start;
                DynamicEditTime = range.StartMs;
            }
            else if (Math.Abs(range.StopMs - cursorPosMs) < window)
            {
                mode = DynamicEditMode.Stop;
                DynamicEditTime = range.StopMs;
            }
            else if (IsInKaraokeBand(y))
            {
                // Karaokê: o primeiro marcador perto do mouse.
                for (int i = 0; i < range.SubTimes.Length; i++)
                {
                    if (Math.Abs(cursorPosMs - range.SubTimes[i]) < window)
                    {
                        mode = DynamicEditMode.Karaoke;
                        DynamicEditTime = i;
                        break;
                    }
                }
            }
        }

        int oldDynamic = DynamicRangeIndex;
        if (mode != DynamicEditMode.None)
        {
            DynamicEditMode = mode;
            if (index >= 0)
            {
                _dynamicRangeIndex = index;
                _dynamicIsSelection = false;
            }
            else
            {
                // A borda da própria seleção: vale a legenda presa a ela, se houver.
                _dynamicRangeIndex = NoRange;
                _dynamicIsSelection = SelectedRangeIndex != NoRange;
            }
        }

        if (DynamicRangeIndex != oldDynamic && DynamicRangeIndex != NoRange && mode is DynamicEditMode.Start or DynamicEditMode.Stop)
        {
            SetMinBlankAt(_ranges[DynamicRangeIndex].StartMs);
        }

        return mode != DynamicEditMode.None;
    }

    // ---------------------------------------------------------------- karaokê

    /// <summary>SelectNextKaraoke: o trecho seguinte, ou o primeiro da próxima legenda com karaokê.</summary>
    public void SelectNextKaraoke()
    {
        int idx = SelectedKaraokeRangeIndex;
        if (idx == NoRange)
        {
            return;
        }

        if (SelectedKaraokeIndex < _ranges[idx].SubTimes.Length)
        {
            SelectKaraoke(idx, SelectedKaraokeIndex + 1);
        }
        else if (idx + 1 < _ranges.Count && _ranges[idx + 1].SubTimes.Length > 0)
        {
            SelectKaraoke(idx + 1, 0);
        }
    }

    /// <summary>SelectPreviousKaraoke: o trecho anterior, ou o último da legenda anterior com karaokê.</summary>
    public void SelectPreviousKaraoke()
    {
        int idx = SelectedKaraokeRangeIndex;
        if (idx == NoRange)
        {
            return;
        }

        if (SelectedKaraokeIndex > 0)
        {
            SelectKaraoke(idx, SelectedKaraokeIndex - 1);
        }
        else if (idx - 1 >= 0 && _ranges[idx - 1].SubTimes.Length > 0)
        {
            SelectKaraoke(idx - 1, _ranges[idx - 1].SubTimes.Length);
        }
    }

    /// <summary>SelecteKaraoke: seleciona o trecho de karaokê (a seleção da onda vira os seus limites).</summary>
    public void SelectKaraoke(int rangeIndex, int index)
    {
        SelectedKaraokeIndex = index;
        SelectedKaraokeRangeIndex = rangeIndex;
        if (rangeIndex != NoRange && rangeIndex < _ranges.Count)
        {
            var r = _ranges[rangeIndex];
            if (Karaoke.GetSubTimeRangeAt(r.StartMs, r.StopMs, r.SubTimes, index, out int kStart, out int kStop))
            {
                SelectionStartMs = kStart;
                SelectionStopMs = kStop;
            }
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
        KaraokeRangeSelected?.Invoke(this, rangeIndex);
    }

    /// <summary>MouseDown: um clique na onda solta o trecho de karaokê.</summary>
    private void ClearKaraokeSelection()
    {
        if (SelectedKaraokeIndex != -1)
        {
            SelectedKaraokeIndex = -1;
            SelectedKaraokeRangeIndex = NoRange;
            KaraokeRangeSelected?.Invoke(this, NoRange);
        }
    }

    /// <summary>Os marcadores só são pegos entre 1/10 e 9/10 da altura da onda.</summary>
    private bool IsInKaraokeBand(int y) => y > WavHeightPx / 10 && y < WavHeightPx * 9 / 10;

    // ---------------------------------------------------------------- encaixe (snapping)

    /// <summary>FindSnappingPoint: espaço mínimo das vizinhas e mudanças de cena até 8 px.</summary>
    public int FindSnappingPoint(int posMs)
    {
        int distance = PixelToTime(Px(SnappingDistancePixel));
        if (MinimumBlankMs > 0)
        {
            foreach (var info in (ReadOnlySpan<MinBlankInfo>)[MinBlank1, MinBlank2])
            {
                if (info.Exists)
                {
                    int candidate = info.GetSnappingPoint(_ranges[info.RangeIndex], MinimumBlankMs);
                    if (Math.Abs(candidate - posMs) <= distance)
                    {
                        return candidate;
                    }
                }
            }
        }

        var scenes = SceneChanges;
        if (!SceneChangeEnabled || scenes.Count == 0)
        {
            return -1;
        }

        int idx = BinarySearch(scenes, posMs);
        int forward = idx;
        while (forward < scenes.Count && IsFilteredSceneChange(scenes[forward], _inVo))
        {
            forward++;
        }

        int backward = idx - 1;
        while (backward >= 0 && IsFilteredSceneChange(scenes[backward], _inVo))
        {
            backward--;
        }

        if (_selectionOrigin == -1)
        {
            int result = -1;
            if (forward < scenes.Count && backward >= 0)
            {
                int distForward = scenes[forward] - SceneChangeStartOffset - posMs;
                int distBackward = posMs - scenes[backward] + SceneChangeStopOffset;
                result = distForward < distBackward ? scenes[forward] - SceneChangeStartOffset : scenes[backward] + SceneChangeStopOffset;
            }
            else if (backward >= 0)
            {
                result = scenes[backward] + SceneChangeStopOffset;
            }
            else if (forward < scenes.Count)
            {
                result = scenes[forward] - SceneChangeStartOffset;
            }

            return result != -1 && Math.Abs(result - posMs) <= distance ? result : -1;
        }

        if (forward < scenes.Count && _selectionOrigin < scenes[forward])
        {
            int candidate = scenes[forward] - SceneChangeStartOffset;
            if (Math.Abs(candidate - posMs) <= distance)
            {
                return candidate;
            }
        }

        if (backward >= 0 && _selectionOrigin > scenes[backward])
        {
            int candidate = scenes[backward] + SceneChangeStopOffset;
            if (Math.Abs(candidate - posMs) <= distance)
            {
                return candidate;
            }
        }

        return -1;
    }

    /// <summary>FindCorrectedSnappingPoint: com o bloqueio de sobreposição, o encaixe respeita os limites.</summary>
    public int FindCorrectedSnappingPoint(int posMs)
    {
        int result = FindSnappingPoint(posMs);
        if (result == -1 || !AntiOverlapping)
        {
            return result;
        }

        if (_minSelTime != -1 && result < _minSelTime)
        {
            result = _minSelTime;
        }

        if (_maxSelTime != -1 && result > _maxSelTime)
        {
            result = _maxSelTime;
        }

        return result;
    }

    /// <summary>IsFilteredSceneChange: mudança de cena no meio de uma legenda (longe das bordas) é ignorada.</summary>
    public bool IsFilteredSceneChange(int sceneChange, bool vo = false)
    {
        var list = vo ? _voRanges : _ranges;
        int pos = GetRangeIdxAt(list, sceneChange);
        if (pos >= 0 && SceneChangeFilterOffset > 0)
        {
            var r = list[pos];
            return sceneChange > r.StartMs + SceneChangeFilterOffset && sceneChange < r.StopMs - SceneChangeFilterOffset;
        }

        return false;
    }

    // ---------------------------------------------------------------- espaço mínimo

    /// <summary>SetMinBlankAt: dentro de uma legenda, mostra o espaço mínimo das vizinhas; fora, das duas em volta.</summary>
    public bool SetMinBlankAt(int timeMs)
    {
        var old1 = MinBlank1;
        var old2 = MinBlank2;
        if (timeMs < 0)
        {
            MinBlank1 = MinBlankInfo.None;
            MinBlank2 = MinBlankInfo.None;
            return old1 != MinBlank1 || old2 != MinBlank2;
        }

        int idx = GetRangeIdxAt(timeMs);
        if (idx != NoRange)
        {
            MinBlank1 = idx > 0 ? new MinBlankInfo(idx - 1, IsStart: false) : MinBlankInfo.None;
            MinBlank2 = idx < _ranges.Count - 1 ? new MinBlankInfo(idx + 1, IsStart: true) : MinBlankInfo.None;
        }
        else
        {
            idx = FindInsertPos(timeMs, -1);
            MinBlank1 = idx > 0 ? new MinBlankInfo(idx - 1, IsStart: false) : MinBlankInfo.None;
            MinBlank2 = idx < _ranges.Count ? new MinBlankInfo(idx, IsStart: true) : MinBlankInfo.None;
        }

        return old1 != MinBlank1 || old2 != MinBlank2;
    }

    private MinBlankInfo Valid(MinBlankInfo info) => info.RangeIndex < _ranges.Count ? info : MinBlankInfo.None;

    // ---------------------------------------------------------------- TRangeList

    /// <summary>CompareRanges: por início e depois por fim.</summary>
    private static int Compare(TimeRange a, TimeRange b) =>
        a.StartMs != b.StartMs ? a.StartMs.CompareTo(b.StartMs) : a.StopMs.CompareTo(b.StopMs);

    /// <summary>TRangeList.FindInsertPos(Start, Stop); Stop = -1 vira Start + 1.</summary>
    public int FindInsertPos(int start, int stop) => FindInsertPos(_ranges, start, stop);

    private static int FindInsertPos(List<TimeRange> list, int start, int stop)
    {
        var range = new TimeRange(start, stop == -1 ? start + 1 : stop);
        int min = 0, max = list.Count - 1;
        while (min <= max)
        {
            int mid = (min + max) / 2;
            int c = Compare(list[mid], range);
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

    /// <summary>TRangeList.GetRangeIdxAt: a última legenda (pela ordem) que contém o tempo.</summary>
    public int GetRangeIdxAt(int posMs) => GetRangeIdxAt(_ranges, posMs);

    private static int GetRangeIdxAt(List<TimeRange> list, int posMs)
    {
        if (list.Count == 0)
        {
            return NoRange;
        }

        for (int i = Math.Clamp(FindInsertPos(list, posMs, -1), 0, list.Count - 1); i >= 0; i--)
        {
            if (list[i].StartMs <= posMs && list[i].StopMs >= posMs)
            {
                return i;
            }
        }

        return NoRange;
    }

    /// <summary>FindFirstRangeAt / FindNextRange: legendas que contêm o tempo, com a folga dada.</summary>
    public IEnumerable<int> FindRangesAt(int posMs, int expandBy)
    {
        if (_ranges.Count == 0)
        {
            yield break;
        }

        int idx = Math.Clamp(FindInsertPos(posMs - expandBy, -1), 0, _ranges.Count - 1);
        while (idx > 0 && _ranges[idx].StopMs > posMs + expandBy)
        {
            idx--;
        }

        for (; idx < _ranges.Count; idx++)
        {
            var r = _ranges[idx];
            if (r.StartMs - expandBy > posMs)
            {
                yield break;
            }

            if (r.StopMs + expandBy >= posMs)
            {
                yield return idx;
            }
        }
    }

    /// <summary>TRangeList.FullSort, mantendo o índice da legenda selecionada.</summary>
    private void FullSort()
    {
        if (SelectedRangeIndex == NoRange)
        {
            _ranges.Sort(Compare);
            return;
        }

        var selected = _ranges[SelectedRangeIndex];
        var order = Enumerable.Range(0, _ranges.Count).OrderBy(i => _ranges[i], Comparer<TimeRange>.Create(Compare)).ToArray();
        var sorted = order.Select(i => _ranges[i]).ToArray();
        int newIndex = Array.IndexOf(order, SelectedRangeIndex);
        _ranges.Clear();
        _ranges.AddRange(sorted);
        SelectedRangeIndex = newIndex >= 0 ? newIndex : _ranges.IndexOf(selected);
    }

    /// <summary>BinarySearch de MiscToolsUnit (para frente).</summary>
    private static int BinarySearch(IReadOnlyList<int> values, int value)
    {
        int min = 0, max = values.Count - 1;
        while (min <= max)
        {
            int mid = (min + max) / 2;
            if (values[mid] < value)
            {
                min = mid + 1;
            }
            else if (values[mid] > value)
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
}

public sealed record SelectedRangeInfo(int Index, bool IsDynamic);

/// <summary>Tempos novos da legenda e os anteriores ao arraste (para o desfazer).</summary>
public sealed record RangeTimesChange(int Index, int StartMs, int StopMs, int OldStartMs, int OldStopMs);

public sealed record RangeEdgeInfo(int Index, bool IsStart);

/// <summary>Um marcador de karaokê da legenda <paramref name="RangeIndex"/> foi para <paramref name="TimeMs"/>.</summary>
public sealed record KaraokeChange(int RangeIndex, int SubTimeIndex, int TimeMs, int OldTimeMs);
