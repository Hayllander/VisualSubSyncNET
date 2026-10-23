using VisualSubSync.Core.Plugins;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Tests;

public sealed class ErrorPluginTests
{
    private static readonly string PluginPath = Path.Combine(AppContext.BaseDirectory, "jsplugin");

    private sealed class FakeEngine(List<Subtitle> subs) : IVssCoreEngine
    {
        public IReadOnlyList<Subtitle> Subtitles => subs;

        public IReadOnlyList<Subtitle> SelectedSubtitles { get; set; } = [];

        public List<string> Actions { get; } = [];

        public void RegisterJavascriptAction(string name, string caption, string defaultShortcut) => Actions.Add($"{name}|{caption}|{defaultShortcut}");

        public int GetTextSelectionStart() => 0;

        public int GetTextSelectionLength() => 0;

        public int GetAudioCursorPosition() => 0;

        public int MeasureStringWidth(string fontName, int fontSize, bool bold, string text) => text.Length * 10;
    }

    private static (PluginContext Context, List<Subtitle> Subs) Setup(params (int Start, int Stop, string Text)[] items)
    {
        var subs = items.Select(i => new Subtitle { StartMs = i.Start, StopMs = i.Stop, Text = i.Text }).ToList();
        return (new PluginContext { Engine = new FakeEngine(subs) }, subs);
    }

    private static ErrorPlugin Load(string name, PluginContext context, List<string>? log = null) =>
        PluginEnumerator.Load(Path.Combine(PluginPath, name), context, log is null ? null : log.Add)
        ?? throw new InvalidOperationException("not loaded: " + string.Join(" / ", log ?? []));

    [Fact]
    public void All_original_error_plugins_load_in_name_order()
    {
        var log = new List<string>();
        var (context, _) = Setup();
        var names = PluginEnumerator.Enumerate(PluginPath, context, log.Add).Select(p => p.Name).ToList();
        Assert.Empty(log);
        Assert.Equal(
            ["French typography", "Overlapping & minimum blank", "Scene change", "Too long display time", "Too long line", "Too many lines", "Too short display time"],
            names);
    }

    [Fact]
    public void Overlapping_reports_and_fixes_with_the_default_mode()
    {
        var (context, subs) = Setup((1000, 2050, "A"), (2000, 3000, "B"), (3100, 4000, "C"));
        var plugin = Load("overlapping.js", context);
        Assert.Equal("Subtitle overlap on next subtitle :", plugin.Message);
        Assert.Equal(0x3737FF, ErrorPlugin.ToTColor(plugin.Color));
        Assert.True(plugin.CanFixError);
        Assert.Equal("50ms overlap", plugin.HasError(subs[0], null, subs[1]));
        Assert.Equal("blank is only 100ms", plugin.HasError(subs[1], subs[0], subs[2]));
        Assert.Equal(string.Empty, plugin.HasError(subs[2], subs[1], null));

        var changes = new List<string>();
        context.ChangingStop += (s, v) => changes.Add($"{s.Text}.Stop {s.StopMs}->{v}");
        plugin.FixError(subs[0], null, subs[1]);
        // Modo 2: Stop -= MinimumBlank - overlap (120 + 50).
        Assert.Equal(1880, subs[0].StopMs);
        Assert.Equal(["A.Stop 2050->1880"], changes);
    }

    [Fact]
    public void Params_are_listed_and_applied()
    {
        var (context, subs) = Setup((1000, 2050, "A"), (2000, 3000, "B"));
        var plugin = Load("overlapping.js", context);
        var ps = plugin.GetParams();
        Assert.Equal(["ParamFixableOverlap", "ParamMode"], ps.Select(p => p.Name));
        Assert.Equal((JsPluginParamType.Integer, 100, "ms"), (ps[0].ParamType, ps[0].IntegerValue, ps[0].Unit));
        Assert.StartsWith("Mode 1: Split", ps[1].Description, StringComparison.Ordinal);

        plugin.SetParamValue("ParamMode", "3");
        plugin.FixError(subs[0], null, subs[1]);
        Assert.Equal((2050, 2170), (subs[0].StopMs, subs[1].StartMs));

        // Unit ausente: o jsbridge devolve "undefined".
        var lines = Load("too_many_lines.js", context).GetParams();
        Assert.Equal("Lines", Assert.Single(lines).Unit);
    }

    [Fact]
    public void Mode_1_rounds_half_values_like_spidermonkey()
    {
        var (context, subs) = Setup((1000, 2051, "A"), (2000, 3000, "B"));
        context.MinimumBlank = 121;
        var plugin = Load("overlapping.js", context);
        plugin.SetParamValue("ParamMode", "1");
        plugin.FixError(subs[0], null, subs[1]);
        // Meio = 2051 - 25,5 = 2025,5; menos 60,5 = 1965 (floor(x + 0,5)).
        Assert.Equal((1965, 2086), (subs[0].StopMs, subs[1].StartMs));
    }

    [Fact]
    public void Tools_are_included_relative_to_the_plugin()
    {
        var (context, subs) = Setup((1000, 1500, "Short"), (3000, 6000, "Bem longo demais para tão pouco tempo"));
        var plugin = Load("too_short_display_time.js", context);
        Assert.Equal("500ms", plugin.HasError(subs[0], null, subs[1]));
        Assert.Equal(string.Empty, plugin.HasError(subs[1], subs[0], null));
    }

    [Fact]
    public void Too_long_line_splits_the_text()
    {
        string text = "Uma frase bem comprida que passa do limite de caracteres por linha sem dúvida";
        var (context, subs) = Setup((0, 5000, text));
        var plugin = Load("too_long_line.js", context);
        Assert.Equal(text.Length + " Characters", plugin.HasError(subs[0], null, null));
        plugin.FixError(subs[0], null, null);
        Assert.Contains("\r\n", subs[0].Text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, plugin.HasError(subs[0], null, null));
    }

    [Fact]
    public void Scene_change_plugin_reads_the_context()
    {
        var (context, subs) = Setup((1000, 2000, "A"));
        context.SceneChanges = [950];
        var plugin = Load("scene_change.js", context);
        Assert.Equal("1 - Overlap on start: 80ms", plugin.HasError(subs[0], null, null));
        context.SceneChanges = [];
        Assert.Equal(string.Empty, plugin.HasError(subs[0], null, null));
    }

    [Fact]
    public void Utf8_plugin_with_bom_loads()
    {
        var (context, subs) = Setup((0, 2000, "Bonjour!"));
        var plugin = Load("french_typo.js", context);
        Assert.Equal("French typography", plugin.Name);
        Assert.NotEqual(string.Empty, plugin.HasError(subs[0], null, null));
    }

    [Fact]
    public void Script_errors_are_reported_and_stop_the_plugin()
    {
        string folder = Directory.CreateTempSubdirectory("vss-js-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "a_broken.js"), "VSSPlugin = {\n  Name : 'x',\n  HasError : function( {\n};");
            File.WriteAllText(Path.Combine(folder, "b_throws.js"), "VSSPlugin = { Name : 'Throws', HasError : function(c) { return c.Start > 1500 ? undefinedThing.x : 'e' + c.Index; } };");
            File.WriteAllText(Path.Combine(folder, "c_nohaserror.js"), "VSSPlugin = { Name : 'None' };");
            var log = new List<string>();
            var (context, subs) = Setup((1000, 1100, "A"), (2000, 2100, "B"), (3000, 3100, "C"));
            var settings = new PluginSettings();

            var errors = ErrorChecker.Check(subs, folder, context, settings, log.Add);

            Assert.Equal(["e1"], errors.Select(e => e.ErrorMessage));
            Assert.Equal(2, log.Count);
            Assert.StartsWith("SyntaxError: ", log[0], StringComparison.Ordinal);
            Assert.EndsWith(" in a_broken.js at line 4\r\n-> };", log[0], StringComparison.Ordinal);
            Assert.StartsWith("ReferenceError: undefinedThing is not defined in b_throws.js at line 1", log[1], StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Fix_truncates_fractional_times_and_ignores_member_case()
    {
        string folder = Directory.CreateTempSubdirectory("vss-js-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "half.js"),
                "VSSPlugin = { Name : 'Half', HasError : function(c) { return 'x' + c.index; },\n" +
                "  FixError : function(c, p, n) { c.Stop = (c.Start + n.start + 1) / 2; c.start = -0.5; } };");
            var (context, subs) = Setup((1000, 2050, "A"), (2000, 3000, "B"));
            var settings = PluginSettings.Build(folder, context, null);
            var errors = ErrorChecker.Check(subs, folder, context, settings, null);
            Assert.Equal("x1", errors[0].ErrorMessage);

            ErrorChecker.FixErrors(subs, [errors[0]], context, settings, null);
            Assert.Equal(1500, subs[0].StopMs);
            Assert.Equal(0, subs[0].StartMs);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Disabled_plugins_are_skipped_and_settings_round_trip()
    {
        var (context, subs) = Setup((1000, 2050, "Abcdef"), (2000, 3000, "B"));
        var settings = PluginSettings.Build(PluginPath, context, null);
        Assert.Equal(7, settings.Plugins.Count);
        var ini = IniDocument.Parse("[Overlapping & minimum blank]\r\nEnabled=0\r\n[Too long line]\r\nParamMaxPerLine=5\r\n");
        settings.Load(ini);
        Assert.False(settings.IsEnabled("Overlapping & minimum blank"));
        Assert.Equal(5, settings.GetByName("Too long line")!.GetParam("ParamMaxPerLine")!.IntegerValue);

        var errors = ErrorChecker.Check(subs, PluginPath, context, settings, null);
        Assert.DoesNotContain(errors, e => e.PluginName == "Overlapping & minimum blank");
        Assert.Contains(errors, e => e.PluginName == "Too long line");

        var saved = IniDocument.Parse(string.Empty);
        settings.Save(saved);
        Assert.Equal("0", saved.Get("Overlapping & minimum blank", "Enabled"));
        Assert.Equal("100", saved.Get("Overlapping & minimum blank", "ParamFixableOverlap"));
        Assert.Equal("5", saved.Get("Too long line", "ParamMaxPerLine"));
    }

    [Fact]
    public void Fix_runs_only_where_a_plugin_finds_an_error()
    {
        var (context, subs) = Setup((1000, 2050, "A"), (2000, 3000, "B"), (5000, 6000, "C"));
        var settings = PluginSettings.Build(PluginPath, context, null);
        ErrorChecker.FixSubtitles(subs, [subs[0], subs[2]], PluginPath, context, settings, null);
        Assert.Equal(1880, subs[0].StopMs);
        Assert.Equal((5000, 6000), (subs[2].StartMs, subs[2].StopMs));
    }

    [Fact]
    public void Vss_core_navigation()
    {
        var (context, subs) = Setup((0, 1, "A"), (2, 3, "B"), (4, 5, "C"));
        ((FakeEngine)context.Engine!).SelectedSubtitles = [subs[0], subs[2]];
        string folder = Directory.CreateTempSubdirectory("vss-js-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "nav.js"), """
                VSSPlugin = { Name : 'Nav', HasError : function(c, p, n) {
                  var s = VSSCore.GetFirst(), r = '';
                  while (s != null) { r += s.Index + s.Text; s = VSSCore.GetNext(s); }
                  r += '|' + VSSCore.GetSubCount() + VSSCore.GetSubAt(1).Text + (VSSCore.GetSubAt(9) === null);
                  r += '|' + VSSCore.GetSelectedCount() + VSSCore.GetNextSelected(VSSCore.GetFirstSelected()).Text;
                  r += '|' + VSSCore.GetPrevious(VSSCore.GetFirst()) + (p === null) + n.StrippedText;
                  r += '|' + VSSCore.MinimumBlank + VSSCore.TEXT_COL_IDX + VSSCore.MeasureStringWidth('Arial', 10, false, 'abc');
                  VSSCore.RegisterJavascriptAction('JSAction1', 'Caption', 'Ctrl+K');
                  return r;
                } };
                """);
            var plugin = PluginEnumerator.Load(Path.Combine(folder, "nav.js"), context, null)!;
            Assert.Equal("1A2B3C|3Btrue|2C|nulltrueB|120430", plugin.HasError(subs[0], null, subs[1]));
            Assert.Equal(["JSAction1|Caption|Ctrl+K"], ((FakeEngine)context.Engine).Actions);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Fix_errors_in_list_calls_fix_without_checking_and_counts()
    {
        var (context, subs) = Setup((1000, 2050, "A"), (2000, 3000, "B"), (3100, 4000, "C"));
        var settings = PluginSettings.Build(PluginPath, context, null);
        var errors = ErrorChecker.Check(subs, PluginPath, context, settings, null)
            .Where(e => e.PluginName == "Overlapping & minimum blank").ToList();
        Assert.Equal(2, errors.Count);
        var changes = new List<string>();
        context.ChangingStop += (s, v) => changes.Add($"{s.Text}.Stop {s.StopMs}->{v}");
        context.ChangingStart += (s, v) => changes.Add($"{s.Text}.Start {s.StartMs}->{v}");

        // O erro duplicado também é corrigido de novo (FixError é chamado sem HasError).
        int count = ErrorChecker.FixErrors(subs, [errors[0], errors[0]], context, settings, null);
        Assert.Equal(2, count);
        Assert.Equal(1880, subs[0].StopMs);
        Assert.Equal("A.Stop 2050->1880", changes[0]);
    }

    [Fact]
    public void Fix_and_can_fix_need_the_plugin_listed_and_enabled()
    {
        var (context, subs) = Setup((1000, 2050, "A"), (2000, 3000, "B"));
        var empty = new PluginSettings();
        string overlapping = Path.Combine(PluginPath, "overlapping.js");
        var error = new ErrorItem(subs[0], 0, "m", "c", overlapping, "Overlapping & minimum blank");
        Assert.False(ErrorChecker.CanFixError(overlapping, context, empty, null));
        Assert.False(ErrorChecker.AnyCanFixError(PluginPath, context, empty, null));
        Assert.Equal(0, ErrorChecker.FixErrors(subs, [error], context, empty, null));
        Assert.Equal(2050, subs[0].StopMs);

        var settings = PluginSettings.Build(PluginPath, context, null);
        Assert.True(ErrorChecker.CanFixError(overlapping, context, settings, null));
        Assert.True(ErrorChecker.AnyCanFixError(PluginPath, context, settings, null));
        settings.GetByName("Overlapping & minimum blank")!.Enabled = false;
        Assert.False(ErrorChecker.CanFixError(overlapping, context, settings, null));
    }

    [Fact]
    public void Html_report_matches_the_original_layout()
    {
        var sub = new Subtitle { StartMs = 1000, StopMs = 2500, Text = "Olá\r\nB" };
        var error = new ErrorItem(sub, 0x3737FF, "Subtitle overlap :", "50ms overlap", "x.js", "P");
        using var stream = new MemoryStream();
        ErrorReportHtml.Write(stream, [error], _ => 4, "C:\\p.vssprj", "1.0");
        string html = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.StartsWith("<html>\r\n<head>\r\n<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" >\r\n", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"Project\">Project : C:\\p.vssprj</div>\r\n<table style=\"margin: auto;\">\r\n<tr>\r\n<th class=\"HeaderColor\">Color</th>", html, StringComparison.Ordinal);
        Assert.Contains(
            "<tr><td style=\"border: 1px solid black; background-color: #FF3737;\">&nbsp;&nbsp;</td><td class=\"CellError\">Subtitle overlap :<br><i>50ms overlap</i></td>" +
            "<td class=\"CellIndex\">5</td><td class=\"CellFromTo\">00:00:01.000 - 00:00:02.500</td><td class=\"CellText\">Olá<br>B</td></tr>\r\n</table>\r\n",
            html,
            StringComparison.Ordinal);
        Assert.EndsWith("VisualSubSync</a> 1.0</div>\r\n</div>\r\n</body>\r\n</html>\r\n", html, StringComparison.Ordinal);
    }
}
