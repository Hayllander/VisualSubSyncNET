using VisualSubSync.Core.Hotkeys;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Text;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.Core.Tests;

public class TimingModeTests
{
    [Theory]
    [InlineData(0x70, "F1")]
    [InlineData(0x1B, "Esc")]
    [InlineData(0x20, "Space")]
    [InlineData(DelphiShortcut.Ctrl | 0x53, "Ctrl+S")]
    [InlineData(DelphiShortcut.Shift | DelphiShortcut.Ctrl | 0x46, "Shift+Ctrl+F")]
    [InlineData(DelphiShortcut.Alt | 0x25, "Alt+Left")]
    [InlineData(0x2E, "Del")]
    public void ShortCutToText_matches_Delphi(int shortcut, string text)
    {
        Assert.Equal(text, DelphiShortcut.ToText(shortcut));
        Assert.Equal(shortcut, DelphiShortcut.FromText(text));
    }

    [Fact]
    public void TextToShortCut_accepts_any_modifier_order_and_case()
    {
        Assert.Equal(DelphiShortcut.Shift | DelphiShortcut.Ctrl | 0x46, DelphiShortcut.FromText("ctrl+shift+f"));
        Assert.Equal(DelphiShortcut.Ctrl | 0x53, DelphiShortcut.FromText("^S"));
        Assert.Equal(0x30, DelphiShortcut.FromText("0")); // o primeiro com o nome: a tecla 0, não o teclado numérico
        Assert.Equal(0, DelphiShortcut.FromText(""));
        Assert.Equal(0, DelphiShortcut.FromText("Ctrl+"));
        Assert.Equal(0, DelphiShortcut.FromText("Nada"));
    }

    [Fact]
    public void Default_hotkeys_follow_the_form_and_DefaultTimingShortcuts()
    {
        var table = HotkeyTable.CreateDefault();
        var play = table.Get("ActionPlay")!;
        Assert.Equal((0x70, 0x70), (play.NormalShortcut, play.TimingShortcut));
        var find = table.Get("ActionFind")!;
        Assert.Equal((DelphiShortcut.Ctrl | 0x46, 0), (find.NormalShortcut, find.TimingShortcut));
        Assert.Equal("ActionLoop", table.Find(0x71, timingMode: false)!.ActionName);
        Assert.Null(table.Find(0x71, timingMode: true));
        Assert.Null(table.Get("ActionNewProject")); // Tag = 0: fora da lista
    }

    [Fact]
    public void Hotkeys_section_overrides_existing_keys_only()
    {
        var table = HotkeyTable.CreateDefault();
        table.Load(IniDocument.Parse(
            "[Hotkeys]\r\nActionSetSubtitleStartTime[Timing]=A\r\nActionPlay[Timing]=\r\nActionStop[Normal]=Shift+Esc\r\n"));
        Assert.Equal(0x41, table.Get("ActionSetSubtitleStartTime")!.TimingShortcut);
        Assert.Equal(0, table.Get("ActionPlay")!.TimingShortcut);
        Assert.Equal(0x70, table.Get("ActionPlay")!.NormalShortcut);
        Assert.Equal(DelphiShortcut.Shift | 0x1B, table.Get("ActionStop")!.NormalShortcut);

        table.Load(IniDocument.Parse("[Hotkeys]\r\nUseIntegerShortcut=1\r\nActionLoop[Normal]=16467\r\n"));
        Assert.Equal(16467, table.Get("ActionLoop")!.NormalShortcut);
    }

    [Fact]
    public void SetStartTime_keeps_the_minimum_blank_after_the_previous_subtitle()
    {
        var previous = new Subtitle { StartMs = 0, StopMs = 1000 };
        var current = new Subtitle { StartMs = 2000, StopMs = 3000 };
        TimingRules.SetStartTime(current, previous, 950, setStopTime: false, 120, 18, 1000);
        Assert.Equal(950 - 60, previous.StopMs);
        Assert.Equal(890 + 120, current.StartMs);

        previous.StopMs = 1000;
        TimingRules.SetStartTime(current, previous, 1050, setStopTime: false, 120, 18, 1000);
        Assert.Equal(1000 - 35, previous.StopMs); // faltavam 70 ms: metade sai da anterior
        Assert.Equal(965 + 120, current.StartMs);
    }

    [Fact]
    public void SetStartTime_fixes_the_stop_and_uses_the_cps_target()
    {
        var current = new Subtitle { StartMs = 1000, StopMs = 2000, Text = "<i>abcdefghijklmnopqrstuvwxyz0123456789</i>" };
        TimingRules.SetStartTime(current, null, 5000, setStopTime: false, 120, 18, 1000);
        Assert.Equal((5000, 5100), (current.StartMs, current.StopMs));

        TimingRules.SetStartTime(current, null, 6000, setStopTime: true, 120, 18, 1000);
        Assert.Equal(6000 + 2000, current.StopMs); // 36 caracteres a 18/s

        current.Text = "ab";
        TimingRules.SetStartTime(current, null, 6000, setStopTime: true, 120, 18, 1000);
        Assert.Equal(7000, current.StopMs); // duração mínima
    }

    [Fact]
    public void SetStopTime_aborts_keeps_or_updates()
    {
        var current = new Subtitle { StartMs = 1000, StopMs = 2000, Text = "abcdefghijklmnopqrstuvwxyz0123456789" };
        Assert.Equal(StopTimeResult.Aborted, TimingRules.SetStopTime(current, 900, false, false, 18, 1000));
        Assert.Equal(StopTimeResult.Updated, TimingRules.SetStopTime(current, 1000, true, false, 18, 1000));
        Assert.Equal(1001, current.StopMs);

        // 36 caracteres em 1,5 s = 24 car/s > 18: mantém o fim que já existia.
        Assert.Equal(StopTimeResult.Kept, TimingRules.SetStopTime(current, 2500, true, true, 18, 1000));
        Assert.Equal(1001, current.StopMs);
        Assert.Equal(StopTimeResult.Updated, TimingRules.SetStopTime(current, 4000, true, true, 18, 1000));
        Assert.Equal(4000, current.StopMs);
    }

    [Fact]
    public void Offsets_never_invert_the_subtitle()
    {
        var current = new Subtitle { StartMs = 1000, StopMs = 1050 };
        Assert.False(TimingRules.CanOffsetStart(current, 50));
        Assert.True(TimingRules.CanOffsetStart(current, 10));
        Assert.False(TimingRules.CanOffsetStop(current, -50));
        Assert.True(TimingRules.CanOffsetStop(current, -10));
    }
}
