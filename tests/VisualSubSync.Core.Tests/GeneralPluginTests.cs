using VisualSubSync.Core.Plugins;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Tests;

public sealed class GeneralPluginTests
{
    private static readonly string GeneralFile = Path.Combine(AppContext.BaseDirectory, "jsplugin", "general", "general_plugin.js");

    private sealed class FakeEngine(List<Subtitle> subs) : IVssCoreEngine
    {
        public IReadOnlyList<Subtitle> Subtitles => subs;

        public IReadOnlyList<Subtitle> SelectedSubtitles { get; set; } = [];

        public List<string> Actions { get; } = [];

        public int SelectionStart { get; set; }

        public void RegisterJavascriptAction(string name, string caption, string defaultShortcut) => Actions.Add($"{name}|{caption}|{defaultShortcut}");

        public int GetTextSelectionStart() => SelectionStart;

        public int GetTextSelectionLength() => 0;

        public int GetAudioCursorPosition() => 0;

        public int MeasureStringWidth(string fontName, int fontSize, bool bold, string text) => text.Length * 10;
    }

    private static (GeneralPlugin Plugin, FakeEngine Engine, PluginContext Context, List<Subtitle> Subs, List<string> Log) Load()
    {
        var subs = new List<Subtitle>
        {
            new() { StartMs = 1000, StopMs = 3500, Text = "Hello world" },
            new() { StartMs = 2100, StopMs = 4000, Text = "Next" },
        };
        var engine = new FakeEngine(subs);
        var context = new PluginContext { Engine = engine };
        var log = new List<string>();
        var plugin = new GeneralPlugin(context);
        plugin.PluginError += log.Add;
        Assert.True(plugin.LoadScript(GeneralFile), string.Join(" / ", log));
        return (plugin, engine, context, subs, log);
    }

    [Fact]
    public void Original_general_plugin_declares_the_rs_column_and_actions()
    {
        var (plugin, engine, _, subs, log) = Load();
        Assert.Empty(log);
        Assert.Equal(1, plugin.ExtraColumnsCount);
        Assert.Equal("RS", plugin.GetColumnTitle(6));
        Assert.Equal(30, plugin.GetColumnSize(6));
        Assert.Equal(50, plugin.GetColumnSize(7));
        Assert.True(plugin.IsColumnBgColorized(6));
        Assert.True(plugin.HasColumnCustomText(6));
        Assert.False(plugin.HasColumnCustomText(4));
        Assert.Equal(["JSAction_QuickStats|Quick stats|Ctrl+M", "JSAction_SplitAtCursor|Split at cursor|"], engine.Actions);

        // RS = 11 * 1000 / (2500 - 500) = 5,5: "Slow, acceptable.", 0x99CCFF.
        Assert.Equal("5.5", plugin.GetColumnText(6, subs[0], null, subs[1]));
        Assert.Equal(0xFFCC99, plugin.GetColumnBgColor(6, subs[0], null, subs[1]));
    }

    [Fact]
    public void Selection_sets_the_status_bar_text()
    {
        var (plugin, _, _, subs, _) = Load();
        var texts = new List<string>();
        plugin.StatusBarText += texts.Add;
        plugin.NotifySelectionModification(subs[0], null, subs[1]);
        string text = Assert.Single(texts);
        Assert.StartsWith("DS: 4.4 ", text, StringComparison.Ordinal);
        Assert.Contains("  |  RS: 5.5 ", text, StringComparison.Ordinal);
        Assert.EndsWith("  |  Duration: 2.5 (ideal: 1.1)  |  Slow, acceptable.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Range_double_clicks_adjust_times_through_the_change_events()
    {
        var (plugin, _, context, subs, _) = Load();
        var changes = new List<string>();
        context.ChangingStart += (s, v) => changes.Add($"{s.Text}.Start {s.StartMs}->{v}");
        context.ChangingStop += (s, v) => changes.Add($"{s.Text}.Stop {s.StopMs}->{v}");

        // Fim de Lavie: 11 * 50 + 500 = 1050 ms, limitado a 2100 - 120.
        plugin.NotifyRangeStopDblClick(subs[0], null, subs[1]);
        Assert.Equal(1980, subs[0].StopMs);

        // Início: não sobrepõe a anterior mais o espaço mínimo.
        plugin.NotifyRangeStartDblClick(subs[1], subs[0], null);
        Assert.Equal(2100, subs[1].StartMs);
        subs[1].StartMs = 2000;
        plugin.NotifyRangeStartDblClick(subs[1], subs[0], null);
        Assert.Equal(2100, subs[1].StartMs);
        Assert.Equal(["Hello world.Stop 3500->1980", "Next.Start 2000->2100"], changes);
    }

    [Fact]
    public void Split_at_cursor_action_runs_on_the_selected_subtitle()
    {
        var (plugin, engine, _, subs, log) = Load();
        subs[0].Text = "Hello\r\nbig world";
        engine.SelectedSubtitles = [subs[0]];
        engine.SelectionStart = 11;
        plugin.ExecuteAction("JSAction_SplitAtCursor");
        Assert.Empty(log);
        Assert.Equal("Hello big \r\nworld", subs[0].Text);
    }

    [Fact]
    public void Quick_stats_action_writes_to_the_log()
    {
        var (plugin, _, _, _, log) = Load();
        plugin.ExecuteAction("JSAction_QuickStats");
        Assert.Equal("--- Quick stats ----------", log[0]);
        Assert.Equal("Subtitle count : 2", log[1]);
        Assert.Equal("---", log[^1]);
    }

    [Fact]
    public void Javascript_actions_join_the_hotkey_table_with_ini_override()
    {
        var table = Hotkeys.HotkeyTable.CreateDefault();
        var ini = Text.IniDocument.Parse("[Hotkeys]\r\nJSAction_SplitAtCursor[Normal]=Ctrl+K\r\n");
        var quick = table.AddAction("JSAction_QuickStats", "[js] Quick stats", Hotkeys.DelphiShortcut.FromText("Ctrl+M"), ini);
        var split = table.AddAction("JSAction_SplitAtCursor", "[js] Split at cursor", 0, ini);
        Assert.Equal((Hotkeys.DelphiShortcut.Ctrl | 'M', 0), (quick.NormalShortcut, quick.TimingShortcut));
        Assert.Equal(Hotkeys.DelphiShortcut.Ctrl | 'K', split.NormalShortcut);
        Assert.Same(quick, table.Find(Hotkeys.DelphiShortcut.Ctrl | 'M', timingMode: false));
        Assert.Null(table.Find(Hotkeys.DelphiShortcut.Ctrl | 'M', timingMode: true));
    }

    [Fact]
    public void Comma_text_matches_tstringlist()
    {
        Assert.Equal(["RS", "My col", "a\"b", ""], Text.DelphiText.SplitCommaText("RS,\"My col\",\"a\"\"b\","));
        Assert.Equal(["a", "b"], Text.DelphiText.SplitCommaText("a b"));
        Assert.Empty(Text.DelphiText.SplitCommaText(string.Empty));
        Assert.Equal("RS,\"My col\",\"a\"\"b\"", Text.DelphiText.ToCommaText(["RS", "My col", "a\"b"]));
        Assert.Equal(string.Empty, Text.DelphiText.ToCommaText([]));
    }
}
