using VisualSubSync.Core.Plugins;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Tests;

/// <summary>Funcionalidade nova: o plugin Line balance.</summary>
public sealed class LineBalanceTests
{
    private static readonly string PluginPath = Path.Combine(AppContext.BaseDirectory, "jsplugin-enh");

    private static List<string> CheckAndFix(List<Subtitle> subs)
    {
        var context = new PluginContext();
        var log = new List<string>();
        var settings = PluginSettings.Build(PluginPath, context, log.Add);
        var errors = ErrorChecker.Check(subs, PluginPath, context, settings, log.Add).Where(e => e.PluginName == "Line balance").ToList();
        ErrorChecker.FixErrors(subs, errors, context, settings, log.Add);
        Assert.Empty(log);
        return [.. errors.Select(e => e.ErrorMessage)];
    }

    [Fact]
    public void Joins_lines_that_fit_and_rebalances_unbalanced_ones()
    {
        var subs = new List<Subtitle>
        {
            new() { StartMs = 0, StopMs = 1000, Text = "Ele saiu pela porta da\r\nfrente" },
            new() { StartMs = 2000, StopMs = 3000, Text = "Ele saiu pela porta da frente e foi direto para\r\ncasa" },
            new() { StartMs = 4000, StopMs = 5000, Text = "Ele saiu pela porta da frente\r\ne foi direto para casa." },
            new() { StartMs = 6000, StopMs = 7000, Text = "- Quem é você?\r\n- Ninguém." },
            new() { StartMs = 8000, StopMs = 9000, Text = "<i>Ele saiu pela porta</i>\r\n<i>da frente</i>" },
        };
        var messages = CheckAndFix(subs);
        Assert.Equal(["fits in one line (29 characters)", "unbalanced lines (47/4)", "fits in one line (29 characters)"], messages);
        Assert.Equal("Ele saiu pela porta da frente", subs[0].Text);
        Assert.Equal("Ele saiu pela porta da frente\r\ne foi direto para casa", subs[1].Text);
        Assert.Equal("Ele saiu pela porta da frente\r\ne foi direto para casa.", subs[2].Text);
        Assert.Equal("- Quem é você?\r\n- Ninguém.", subs[3].Text);
        Assert.Equal("<i>Ele saiu pela porta</i> <i>da frente</i>", subs[4].Text);
    }
}
