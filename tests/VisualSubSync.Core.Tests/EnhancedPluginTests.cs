using VisualSubSync.Core.Plugins;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Tests;

/// <summary>Os plugins JavaScript do Enhanced, que o programa distribui em jsplugin, e o Audio sync (novo).</summary>
public sealed class EnhancedPluginTests
{
    private static readonly string PluginPath = Path.Combine(AppContext.BaseDirectory, "jsplugin-enh");

    private sealed class FakeEngine(List<Subtitle> subs) : IVssCoreEngine
    {
        public IReadOnlyList<Subtitle> Subtitles => subs;

        public IReadOnlyList<Subtitle> SelectedSubtitles => subs.Count > 0 ? [subs[0]] : [];

        public List<string> Actions { get; } = [];

        public List<int> Deleted { get; } = [];

        public Dictionary<string, int> Params { get; } = [];

        public void RegisterJavascriptAction(string name, string caption, string defaultShortcut) => Actions.Add(name);

        public int GetTextSelectionStart() => 0;

        public int GetTextSelectionLength() => 0;

        public int GetAudioCursorPosition() => 0;

        public int MeasureStringWidth(string fontName, int fontSize, bool bold, string text) => text.Length * 10;

        public int GetPluginParamValue(string section, string parameter) => Params.GetValueOrDefault(section + "/" + parameter);

        public void DeleteSubtitle(int index)
        {
            Deleted.Add(index);
            subs.RemoveAt(index);
        }
    }

    private static (PluginContext Context, FakeEngine Engine, List<Subtitle> Subs) Setup(params (int Start, int Stop, string Text)[] items)
    {
        var subs = items.Select(i => new Subtitle { StartMs = i.Start, StopMs = i.Stop, Text = i.Text }).ToList();
        var engine = new FakeEngine(subs);
        return (new PluginContext { Engine = engine, CpsTarget = 18, MinimumDuration = 1000, MinimumBlank = 120, MaximumDuration = 6000 }, engine, subs);
    }

    [Fact]
    public void All_enhanced_error_plugins_load_without_errors()
    {
        var log = new List<string>();
        var (context, _, _) = Setup();
        var names = PluginEnumerator.Enumerate(PluginPath, context, log.Add).Select(p => p.Name).ToList();
        Assert.Empty(log);
        Assert.Equal(
            ["Audio sync", "Duration", "Greek typography", "ITASA - Common errors", "ITASA - Row splitter", "Line balance", "Overlapping & minimum blank", "Speed", "Too long line", "Too many lines", "Un-closed tags"],
            names);
    }

    [Fact]
    public void Duration_errors_can_be_fixed_from_the_error_report()
    {
        var log = new List<string>();
        var (context, _, subs) = Setup((1000, 9000, "A long one"), (12000, 12200, "Short"), (20000, 21000, "Ok"));
        var settings = PluginSettings.Build(PluginPath, context, log.Add);
        var errors = ErrorChecker.Check(subs, PluginPath, context, settings, log.Add).Where(e => e.PluginName == "Duration").ToList();
        Assert.Equal(["> 5 s", "< 1 s"], errors.Select(e => e.ErrorMessage));
        Assert.True(ErrorChecker.CanFixError(errors[0].PluginFileName, context, settings, log.Add));

        Assert.Equal(2, ErrorChecker.FixErrors(subs, errors, context, settings, log.Add));
        Assert.Empty(log);
        Assert.Equal(6000, subs[0].StopMs);
        Assert.Equal(13000, subs[1].StopMs);
    }

    [Fact]
    public void Un_closed_tags_are_closed()
    {
        var (context, _, subs) = Setup((1000, 3000, "<i>Hello"));
        var settings = PluginSettings.Build(PluginPath, context, null);
        var errors = ErrorChecker.Check(subs, PluginPath, context, settings, null).Where(e => e.PluginName == "Un-closed tags").ToList();
        ErrorChecker.FixErrors(subs, errors, context, settings, null);
        Assert.Equal("<i>Hello</i>", subs[0].Text);
    }

    [Fact]
    public void Vsscore_delete_subtitle_and_plugin_param_value_reach_the_engine()
    {
        var (context, engine, subs) = Setup((1000, 2000, "A"), (3000, 4000, "B"));
        engine.Params["ITASA - Row splitter/ParamMaxPerRiga"] = 42;
        string folder = Directory.CreateTempSubdirectory("vss-js-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "probe.js"),
                "VSSPlugin = { Name : 'Probe', HasError : function(c) { return '' + VSSCore.GetPluginParamValue('ITASA - Row splitter', 'ParamMaxPerRiga'); },\n" +
                "  FixError : function(c) { if (VSSCore.DeleteSubtitle(0) || VSSCore.DeleteSubtitle(3)) c.Text = 'bad'; VSSCore.DeleteSubtitle(c.index); } };");
            var plugin = PluginEnumerator.Load(Path.Combine(folder, "probe.js"), context, null)!;
            Assert.Equal("42", plugin.HasError(subs[1], subs[0], null));
            plugin.FixError(subs[1], subs[0], null);
            Assert.Equal([1], engine.Deleted);
            Assert.Equal("A", Assert.Single(subs).Text);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Enhanced_general_plugin_loads_its_actions()
    {
        var log = new List<string>();
        var (context, engine, _) = Setup((1000, 3000, "Hello"));
        var plugin = new GeneralPlugin(context);
        plugin.PluginError += log.Add;
        Assert.True(plugin.LoadScript(Path.Combine(PluginPath, "general", "general_plugin.js")), string.Join(" / ", log));
        Assert.Empty(log);
        Assert.Equal(3, plugin.ExtraColumnsCount);
        Assert.Contains("JSAction_QuickStats", engine.Actions);
        Assert.Contains("JSAction_split_line", engine.Actions);
    }
}
