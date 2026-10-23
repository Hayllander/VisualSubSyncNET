using VisualSubSync.Core.Scenes;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Plugins;

#pragma warning disable CA1822 // Os membros fazem parte da API vista pelos scripts.

/// <summary>
/// TSubtitleRangeJSWrapper: uma legenda vista pelo script (Index, Start, Stop, Text, StrippedText).
/// Mudar um valor avisa o contexto antes, para o desfazer, como os eventos OnChange* do original.
/// </summary>
public sealed class SubtitleJs
{
    private readonly PluginContext _context;
    private string? _strippedText;

    internal SubtitleJs(PluginContext context, Subtitle subtitle)
    {
        _context = context;
        Subtitle = subtitle;
    }

    internal Subtitle Subtitle { get; }

    /// <summary>Node.Index + 1, ou -1 quando a legenda não está na lista.</summary>
    public int Index
    {
        get
        {
            var list = _context.Engine?.Subtitles;
            int i = list is null ? -1 : IndexOf(list, Subtitle);
            return i >= 0 ? i + 1 : -1;
        }
    }

    public double Start
    {
        get => Subtitle.StartMs;
        set
        {
            int v = JsConvert.Truncate(value);
            if (v != Subtitle.StartMs)
            {
                _context.OnChangingStart(Subtitle, v);
                Subtitle.StartMs = v;
            }
        }
    }

    public double Stop
    {
        get => Subtitle.StopMs;
        set
        {
            int v = JsConvert.Truncate(value);
            if (v != Subtitle.StopMs)
            {
                _context.OnChangingStop(Subtitle, v);
                Subtitle.StopMs = v;
            }
        }
    }

    public string Text
    {
        get => Subtitle.Text;
        set
        {
            value ??= string.Empty;
            if (value != Subtitle.Text)
            {
                _context.OnChangingText(Subtitle, value);
                Subtitle.Text = value;
                _strippedText = null;
            }
        }
    }

    /// <summary>StripTags(Text), calculado uma vez; escrever nele troca o Text (como o original).</summary>
    public string StrippedText
    {
        get => _strippedText ??= SubtitleTextStats.StripTags(Subtitle.Text);
        set => Text = value;
    }

    internal static int IndexOf(IReadOnlyList<Subtitle> list, Subtitle sub)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], sub))
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>TSceneChangeWrapper: o objeto global SceneChange.</summary>
public sealed class SceneChangeJs
{
    private readonly PluginContext _context;

    internal SceneChangeJs(PluginContext context) => _context = context;

    public int StartOffset => _context.SceneChangeStartOffset;

    public int StopOffset => _context.SceneChangeStopOffset;

    public int FilterOffset => _context.SceneChangeFilterOffset;

    public bool Visible => _context.SceneChangeVisible;

    /// <summary>Contains(Start, Stop): há mudança de cena em [Start, Stop].</summary>
    public bool Contains(double start, double stop) =>
        SceneChangeList.Contains(_context.SceneChanges, JsConvert.ToInt32(start), JsConvert.ToInt32(stop));

    public int GetCount() => _context.SceneChanges.Count;

    /// <summary>GetAt(Index): -1 fora da lista.</summary>
    public int GetAt(double index)
    {
        int i = JsConvert.ToInt32(index);
        return i >= 0 && i < _context.SceneChanges.Count ? _context.SceneChanges[i] : -1;
    }

    /// <summary>GetNext(TimeMs): a primeira mudança de cena &gt;= TimeMs.</summary>
    public int GetNext(double timeMs) => GetAt(_context.SceneIndex(JsConvert.ToInt32(timeMs), backward: false));

    /// <summary>GetPrevious(TimeMs): a última mudança de cena &lt;= TimeMs.</summary>
    public int GetPrevious(double timeMs) => GetAt(_context.SceneIndex(JsConvert.ToInt32(timeMs), backward: true));
}

/// <summary>TVSSCoreWrapper: o objeto global VSSCore.</summary>
public sealed class VssCoreJs
{
    private readonly PluginContext _context;

    internal VssCoreJs(PluginContext context) => _context = context;

#pragma warning disable CA1707 // Nomes do original.
    public int INDEX_COL_IDX => _context.IndexColumn;

    public int START_COL_IDX => _context.StartColumn;

    public int STOP_COL_IDX => _context.StopColumn;

    public int STYLE_COL_IDX => _context.StyleColumn;

    public int TEXT_COL_IDX => _context.TextColumn;

    public int VO_COL_IDX => _context.VoColumn;

    public int LAST_CORE_COL_IDX => _context.LastCoreColumn;
#pragma warning restore CA1707

    public int CpsTarget => _context.CpsTarget;

    public int MinimumDuration => _context.MinimumDuration;

    public int MaximumDuration => _context.MaximumDuration;

    public int MinimumBlank => _context.MinimumBlank;

    public int VideoWidth => _context.VideoWidth;

    public int VideoHeight => _context.VideoHeight;

    private IReadOnlyList<Subtitle> Subs => _context.Engine?.Subtitles ?? [];

    private SubtitleJs? Wrap(Subtitle? sub) => sub is null ? null : new SubtitleJs(_context, sub);

    public void RegisterJavascriptAction(string name, string caption, string defaultShortcut) =>
        _context.Engine?.RegisterJavascriptAction(name, caption, defaultShortcut);

    public int GetSubCount() => Subs.Count;

    public SubtitleJs? GetSubAt(double index)
    {
        int i = JsConvert.ToInt32(index);
        return i >= 0 && i < Subs.Count ? Wrap(Subs[i]) : null;
    }

    public SubtitleJs? GetFirst() => Subs.Count > 0 ? Wrap(Subs[0]) : null;

    public SubtitleJs? GetNext(SubtitleJs? sub) => Neighbour(sub, 1);

    public SubtitleJs? GetPrevious(SubtitleJs? sub) => Neighbour(sub, -1);

    private SubtitleJs? Neighbour(SubtitleJs? sub, int delta)
    {
        if (sub is null)
        {
            return null;
        }

        int i = SubtitleJs.IndexOf(Subs, sub.Subtitle);
        return i >= 0 && i + delta >= 0 && i + delta < Subs.Count ? Wrap(Subs[i + delta]) : null;
    }

    public int GetSelectedCount() => _context.Engine?.SelectedSubtitles.Count ?? 0;

    public SubtitleJs? GetFirstSelected()
    {
        var selected = _context.Engine?.SelectedSubtitles;
        return selected is { Count: > 0 } ? Wrap(selected[0]) : null;
    }

    public SubtitleJs? GetNextSelected(SubtitleJs? sub)
    {
        var selected = _context.Engine?.SelectedSubtitles;
        if (sub is null || selected is null)
        {
            return null;
        }

        int i = SubtitleJs.IndexOf(selected, sub.Subtitle);
        return i >= 0 && i + 1 < selected.Count ? Wrap(selected[i + 1]) : null;
    }

    public int MeasureStringWidth(string fontName, double fontSize, bool bold, string text) =>
        _context.Engine?.MeasureStringWidth(fontName, JsConvert.ToInt32(fontSize), bold, text) ?? 0;

    public int GetTextSelectionStart() => _context.Engine?.GetTextSelectionStart() ?? 0;

    public int GetTextSelectionLength() => _context.Engine?.GetTextSelectionLength() ?? 0;

    public int GetAudioCursorPosition() => _context.Engine?.GetAudioCursorPosition() ?? 0;
}

/// <summary>Conversões de valores do JavaScript como as do SpiderMonkey 1.5 usado pelo original.</summary>
internal static class JsConvert
{
    /// <summary>JS_ValueToInt32: arredonda com floor(d + 0,5); NaN e infinitos viram 0.</summary>
    public static int ToInt32(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d))
        {
            return 0;
        }

        double r = Math.Floor(d + 0.5);
        return r <= int.MinValue ? int.MinValue : r >= int.MaxValue ? int.MaxValue : (int)r;
    }

    /// <summary>
    /// SetPropValue numa propriedade Integer (RangedValue da TypInfo do Delphi 7): trunca o double;
    /// NaN e infinitos viram 0.
    /// </summary>
    public static int Truncate(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d))
        {
            return 0;
        }

        double r = Math.Truncate(d);
        return r <= int.MinValue ? int.MinValue : r >= int.MaxValue ? int.MaxValue : (int)r;
    }
}
