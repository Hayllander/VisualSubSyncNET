using VisualSubSync.Core.Waveform;

namespace VisualSubSync.Core.Tests;

public class WaveformMouseTests
{
    // 1000 px para 10 s: 1 px = 10 ms.
    private static WaveformMouse Create(params TimeRange[] ranges)
    {
        var m = new WaveformMouse { PositionMs = 0, PageSizeMs = 10_000, LengthMs = 60_000, WidthPx = 1000, WavHeightPx = 200 };
        m.SetRanges(ranges);
        return m;
    }

    [Fact]
    public void Click_moves_cursor_and_short_drag_is_cleared_on_mouse_up()
    {
        var m = Create(new TimeRange(1000, 2000));
        m.MouseDown(WaveformInput.Left, 300, 50);
        Assert.Equal(3000, m.CursorMs);
        m.MouseMove(WaveformInput.Left, 303, 50); // 30 ms < 40 ms
        Assert.Equal(3000, m.SelectionStartMs);
        Assert.Equal(3030, m.SelectionStopMs);
        m.MouseUp();
        Assert.True(m.SelectionIsEmpty);
        Assert.Equal(WaveformMouse.NoRange, m.SelectedRangeIndex);
    }

    [Fact]
    public void Single_click_inside_a_subtitle_does_not_select_it_double_click_does()
    {
        var m = Create(new TimeRange(1000, 2000), new TimeRange(3000, 4000));
        SelectedRangeInfo? selected = null;
        m.RangeSelected += (_, e) => selected = e;

        m.MouseDown(WaveformInput.Left, 350, 50);
        m.MouseUp();
        Assert.Equal(WaveformMouse.NoRange, m.SelectedRangeIndex);
        Assert.Null(selected);

        m.DoubleClick(50);
        Assert.Equal(1, m.SelectedRangeIndex);
        Assert.Equal((3000, 4000), (m.SelectionStartMs, m.SelectionStopMs));
        Assert.Equal(new SelectedRangeInfo(1, false), selected);
    }

    [Fact]
    public void Hovering_an_edge_and_dragging_changes_the_subtitle_and_sorts_on_mouse_up()
    {
        var m = Create(new TimeRange(1000, 2000), new TimeRange(3000, 4000));
        m.SnappingEnabled = false;
        var changing = new List<RangeTimesChange>();
        RangeTimesChange? changed = null;
        SelectedRangeInfo? selected = null;
        m.SelectedRangeTimesChanging += (_, e) => changing.Add(e);
        m.SelectedRangeTimesChanged += (_, e) => changed = e;
        m.RangeSelected += (_, e) => selected = e;

        m.MouseMove(WaveformInput.None, 301, 50); // perto do início da segunda legenda
        Assert.Equal(DynamicEditMode.Start, m.DynamicEditMode);
        Assert.Equal(1, m.DynamicRangeIndex);

        int? movedTo = null;
        m.MoveMouseRequested += (_, x) => movedTo = x;
        m.MouseDown(WaveformInput.Left, 301, 50);
        Assert.Equal(300, movedTo); // o mouse volta para cima da borda
        Assert.Equal(new SelectedRangeInfo(1, true), selected);
        Assert.Equal(1, m.SelectedRangeIndex);
        Assert.Equal(0, m.CursorMs); // em edição dinâmica o cursor não se move

        // Arrasta o início para antes da primeira legenda: a ordem muda no fim.
        m.MouseMove(WaveformInput.Left, 50, 50);
        Assert.Equal(new TimeRange(500, 4000), m.Ranges[1]);
        Assert.Equal(new RangeTimesChange(1, 500, 4000, 3000, 4000), changing[^1]);

        m.MouseUp();
        Assert.Equal(new RangeTimesChange(1, 500, 4000, 3000, 4000), changed);
        Assert.Equal([new TimeRange(500, 4000), new TimeRange(1000, 2000)], m.Ranges);
        Assert.Equal(0, m.SelectedRangeIndex);
        Assert.Equal(DynamicEditMode.None, m.DynamicEditMode);
    }

    [Fact]
    public void Shift_click_extends_the_nearest_side_of_the_selection()
    {
        var m = Create();
        m.MouseDown(WaveformInput.Left, 100, 50);
        m.MouseMove(WaveformInput.Left, 200, 50);
        m.MouseUp();
        Assert.Equal((1000, 2000), (m.SelectionStartMs, m.SelectionStopMs));

        m.MouseDown(WaveformInput.Left | WaveformInput.Shift, 250, 50);
        m.MouseUp();
        Assert.Equal((1000, 2500), (m.SelectionStartMs, m.SelectionStopMs));

        m.MouseDown(WaveformInput.Left | WaveformInput.Shift, 50, 50);
        m.MouseUp();
        Assert.Equal((500, 2500), (m.SelectionStartMs, m.SelectionStopMs));
    }

    [Fact]
    public void Snapping_uses_minimum_blank_of_neighbours_and_ctrl_disables_it()
    {
        var m = Create(new TimeRange(1000, 2000), new TimeRange(5000, 6000));
        m.MouseMove(WaveformInput.None, 300, 50); // entre as duas: espaço mínimo de ambas
        Assert.Equal(new MinBlankInfo(0, false), m.MinBlank1);
        Assert.Equal(new MinBlankInfo(1, true), m.MinBlank2);

        m.MouseDown(WaveformInput.Left, 215, 50); // 2150 ms, a 30 ms de 2000 + 120
        Assert.Equal(2120, m.CursorMs);
        m.MouseUp();

        m.MouseMove(WaveformInput.None, 300, 50);
        m.MouseDown(WaveformInput.Left | WaveformInput.Ctrl, 215, 50);
        Assert.Equal(2150, m.CursorMs);
        m.MouseUp();
    }

    [Fact]
    public void Snapping_to_scene_changes_skips_filtered_ones()
    {
        var m = Create(new TimeRange(1000, 5000));
        m.MinimumBlankMs = 0;
        m.SceneChangeEnabled = true;
        m.SceneChanges = [3000, 8000];
        Assert.True(m.IsFilteredSceneChange(3000)); // no meio da legenda, longe das bordas
        Assert.False(m.IsFilteredSceneChange(8000));

        // 8000 - 130 = 7870; clique em 7900 encaixa.
        m.MouseDown(WaveformInput.Left, 790, 50);
        Assert.Equal(7870, m.CursorMs);
        m.MouseUp();
    }

    [Fact]
    public void Anti_overlapping_limits_the_drag_to_the_gap_between_subtitles()
    {
        var m = Create(new TimeRange(1000, 2000), new TimeRange(5000, 6000));
        m.SnappingEnabled = false;
        m.AntiOverlapping = true;
        (int, int)? clip = null;
        m.ClipMouseRequested += (_, c) => clip = c;

        m.MouseDown(WaveformInput.Left, 300, 50);
        Assert.Equal((200 - 2, 500 + 3), clip); // 2001 ms e 4999 ms (200 e 500 px), com a folga do original
        m.MouseMove(WaveformInput.Left, 900, 50);
        Assert.Equal(4999, m.SelectionStopMs);
        m.MouseUp();
        Assert.Null(clip);
    }

    [Fact]
    public void Ssa_mode_left_sets_start_and_right_sets_stop()
    {
        var m = Create();
        m.SelectionMode = WaveformSelectionMode.Ssa;
        m.SnappingEnabled = false;
        m.MouseDown(WaveformInput.Left, 100, 50);
        m.MouseUp();
        m.MouseDown(WaveformInput.Right, 300, 50);
        m.MouseUp();
        Assert.Equal((1000, 3000), (m.SelectionStartMs, m.SelectionStopMs));
        m.MouseDown(WaveformInput.Left, 150, 50);
        m.MouseUp();
        Assert.Equal((1500, 3000), (m.SelectionStartMs, m.SelectionStopMs));
    }

    [Fact]
    public void Range_list_search_matches_TRangeList()
    {
        var m = Create(new TimeRange(1000, 3000), new TimeRange(2000, 2500), new TimeRange(4000, 5000));
        Assert.Equal(1, m.GetRangeIdxAt(2200));
        Assert.Equal(0, m.GetRangeIdxAt(2800));
        Assert.Equal(WaveformMouse.NoRange, m.GetRangeIdxAt(3500));
        Assert.Equal(2, m.FindInsertPos(3500, -1));
        Assert.Equal([0, 1], m.FindRangesAt(2200, 0));
        Assert.Equal([2], m.FindRangesAt(3990, 20));
    }

    [Fact]
    public void Karaoke_marker_is_dragged_between_its_neighbours()
    {
        var m = Create(new TimeRange(1000, 3000) { SubTimes = [1500, 2000] });
        KaraokeChange? changing = null, changed = null;
        m.KaraokeTimeChanging += (_, e) => changing = e;
        m.KaraokeTimeChanged += (_, e) => changed = e;

        // Sobre o marcador de 1500 ms, no meio da altura.
        m.MouseMove(WaveformInput.None, 150, 100);
        Assert.Equal(DynamicEditMode.Karaoke, m.DynamicEditMode);
        Assert.Equal(0, m.DynamicEditTime);
        // Fora da faixa do meio (1/10 a 9/10), o marcador não é pego.
        m.MouseMove(WaveformInput.None, 150, 10);
        Assert.Equal(DynamicEditMode.None, m.DynamicEditMode);

        m.MouseMove(WaveformInput.None, 150, 100);
        m.MouseDown(WaveformInput.Left, 150, 100);
        m.MouseMove(WaveformInput.Left, 170, 100);
        Assert.Equal(new KaraokeChange(0, 0, 1700, 1500), changing);
        Assert.Equal(0, m.CursorMs);
        m.MouseUp();
        Assert.Equal(new KaraokeChange(0, 0, 1700, 1500), changed);
        Assert.Equal([1700, 2000], m.Ranges[0].SubTimes);
    }

    [Fact]
    public void Double_click_selects_a_karaoke_part_and_next_previous_walk_them()
    {
        var m = Create(new TimeRange(1000, 3000) { SubTimes = [1500, 2000] }, new TimeRange(4000, 5000) { SubTimes = [4500] });
        int selectedRange = -2;
        m.KaraokeRangeSelected += (_, i) => selectedRange = i;
        m.MouseDown(WaveformInput.Left, 170, 100);
        m.MouseUp();
        m.DoubleClick(100);
        Assert.Equal(0, selectedRange);
        Assert.Equal(1, m.SelectedKaraokeIndex);
        Assert.Equal((1500, 2000), (m.SelectionStartMs, m.SelectionStopMs));
        Assert.Equal(WaveformMouse.NoRange, m.SelectedRangeIndex);

        m.SelectNextKaraoke();
        Assert.Equal((2000, 3000), (m.SelectionStartMs, m.SelectionStopMs));
        m.SelectNextKaraoke();
        Assert.Equal((1, 0), (m.SelectedKaraokeRangeIndex, m.SelectedKaraokeIndex));
        Assert.Equal((4000, 4500), (m.SelectionStartMs, m.SelectionStopMs));
        m.SelectPreviousKaraoke();
        Assert.Equal((0, 2), (m.SelectedKaraokeRangeIndex, m.SelectedKaraokeIndex));

        // Um clique na onda solta o trecho.
        m.MouseDown(WaveformInput.Left, 700, 100);
        Assert.Equal(-1, selectedRange);
        Assert.Equal(-1, m.SelectedKaraokeIndex);
    }
}
