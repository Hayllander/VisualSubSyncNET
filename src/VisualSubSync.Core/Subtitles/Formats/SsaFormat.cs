using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VisualSubSync.Core.Text;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.Core.Subtitles.Formats;

/// <summary>
/// SSA (v4) e ASS (v4+) conforme TSSAParser (SSAParserUnit.pas), TMainForm.LoadASS e
/// SaveSubtitlesAsSSA/ASS. Mantém o comportamento do legado, inclusive o descarte de linhas
/// "Comment:" e de chaves desconhecidas dentro das seções de estilos e eventos.
/// </summary>
public static partial class SsaFormat
{
    private const string SsaStyleFormat =
        "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, TertiaryColour, BackColour, Bold, Italic, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, AlphaLevel, Encoding";

    // O espaço duplo depois de "Italic," é o do legado.
    private const string AssStyleFormat =
        "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic,  Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding";

    /// <summary>Resultado da leitura: o documento e se o arquivo é ASS (ScriptType v4.00+ ou [v4+ Styles]).</summary>
    public sealed record ParseResult(SubtitleDocument Document, bool IsAss);

    public static ParseResult Parse(string text) => Parse(text, clipboard: false);

    /// <summary>
    /// <paramref name="clipboard"/>: TryPasteAsSSA, que mantém a ordem do texto e lê Marked e Layer cada
    /// um quando existe no Format, sem olhar se é SSA ou ASS.
    /// </summary>
    internal static ParseResult Parse(string text, bool clipboard)
    {
        var scriptInfo = new List<string>();
        var styles = new FormattedSection();
        var events = new FormattedSection();
        var footer = new StringBuilder();
        bool styleSectionIsAss = false;

        Section section = Section.Footer;
        foreach (string line in DelphiText.SplitLines(text))
        {
            string trimmed = DelphiText.Trim(line);
            if (DelphiText.StartsText("[", line) && DelphiText.EndsText("]", trimmed))
            {
                section = SectionFor(trimmed, ref styleSectionIsAss);
            }

            switch (section)
            {
                case Section.ScriptInfo:
                    if (!DelphiText.StartsText(";", line) && line.Contains(':', StringComparison.Ordinal))
                    {
                        scriptInfo.Add(line);
                    }

                    break;
                case Section.Styles:
                    styles.ParseLine(line, "Style:");
                    break;
                case Section.Events:
                    events.ParseLine(line, "Dialogue:");
                    break;
                default:
                    if (footer.Length > 0)
                    {
                        footer.Append("\r\n");
                    }

                    footer.Append(line);
                    break;
            }
        }

        string scriptType = DelphiText.Trim(ScriptInfoValue(scriptInfo, "ScriptType"));
        bool isAss = DelphiText.StartsText("v4.00+", scriptType) || styleSectionIsAss;

        var document = new SubtitleDocument
        {
            Header = string.Concat(scriptInfo.Select(static l => l + "\r\n")),
            Footer = footer.ToString(),
        };

        for (int i = 0; i < styles.Count; i++)
        {
            document.Styles.Add(ReadStyle(styles, i, isAss));
        }

        for (int i = 0; i < events.Count; i++)
        {
            int start = TimeFormat.ParseSsa(events.Get(i, "Start"));
            int stop = TimeFormat.ParseSsa(events.Get(i, "End"));
            if (start == -1 || stop == -1)
            {
                continue;
            }

            var sub = new Subtitle
            {
                StartMs = start,
                StopMs = stop,
                Text = events.Get(i, "Text").Replace(@"\N", "\r\n", StringComparison.Ordinal),
                Effect = events.Get(i, "Effect"),
                MarginR = events.Get(i, "MarginR"),
                MarginL = events.Get(i, "MarginL"),
                MarginV = events.Get(i, "MarginV"),
                Style = events.Get(i, "Style"),
                Actor = events.Get(i, "Name"),
            };
            if (clipboard)
            {
                sub.Marked = events.Get(i, "Marked");
                sub.Layer = events.Get(i, "Layer");
            }
            else if (isAss)
            {
                sub.Layer = events.Get(i, "Layer");
            }
            else
            {
                sub.Marked = events.Get(i, "Marked");
            }

            document.Items.Add(sub);
        }

        if (!clipboard)
        {
            document.Sort();
        }

        return new ParseResult(document, isAss);
    }

    public static string Format(SubtitleDocument document, bool ass, SubtitleWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new SubtitleWriteOptions();
        bool fromSrt = options.ConvertsFrom(".srt");
        var sb = new StringBuilder();
        void Line(string s) => sb.Append(s).Append("\r\n");

        Line("[Script Info]");
        Line("; Written by VisualSubSync " + options.Version);
        if (document.Header.Length == 0)
        {
            Line("Title: <untitled>");
            Line("Original Script: <unknown>");
            Line(ass ? "ScriptType: v4.00+" : "ScriptType: v4.00");
            if (options.VideoWidth > 0 && options.VideoHeight > 0)
            {
                Line(string.Create(CultureInfo.InvariantCulture, $"PlayResX: {options.VideoWidth}"));
                Line(string.Create(CultureInfo.InvariantCulture, $"PlayResY: {options.VideoHeight}"));
            }

            Line("PlayDepth: 0");
            Line("Timer: 100.0");
            if (ass)
            {
                Line("WrapStyle: 0");
            }
        }
        else
        {
            string header = ScriptTypeRegex().Replace(document.Header, ass ? "ScriptType: v4.00+" : "ScriptType: v4.00");
            Line(DelphiText.Trim(header));
        }

        Line(string.Empty);
        Line(ass ? "[v4+ Styles]" : "[v4 Styles]");
        Line(ass ? AssStyleFormat : SsaStyleFormat);
        IEnumerable<SsaStyle> styleList = document.Styles.Count > 0 ? document.Styles : [new SsaStyle { Name = "Default" }];
        foreach (var style in styleList)
        {
            Line(ass ? style.ToAssLine() : style.ToSsaLine());
        }

        Line(string.Empty);
        Line("[Events]");
        Line(ass
            ? "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text"
            : "Format: Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");
        foreach (var sub in document.Items)
        {
            string text = options.TranslateText(sub.Text);
            text = fromSrt ? SubtitleTags.SrtToSsa(text) : text;
            Line(string.Create(CultureInfo.InvariantCulture,
                $"Dialogue: {(ass ? sub.Layer : sub.Marked)},{TimeFormat.ToSsa(sub.StartMs)},{TimeFormat.ToSsa(sub.StopMs)},{sub.Style},{sub.Actor},{sub.MarginL},{sub.MarginR},{sub.MarginV},{sub.Effect},")
                + text.Replace("\r\n", @"\N", StringComparison.Ordinal));
        }

        if (document.Footer.Length > 0)
        {
            Line(string.Empty);
            Line(DelphiText.Trim(document.Footer));
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"ScriptType: v4\.00\+?", RegexOptions.CultureInvariant)]
    private static partial Regex ScriptTypeRegex();

    private enum Section
    {
        Footer,
        ScriptInfo,
        Styles,
        Events,
    }

    private static Section SectionFor(string trimmed, ref bool styleSectionIsAss)
    {
        if (DelphiText.StartsText("[Script Info]", trimmed))
        {
            return Section.ScriptInfo;
        }

        if (DelphiText.StartsText("[v4", trimmed) && DelphiText.EndsText("Styles]", trimmed))
        {
            styleSectionIsAss = DelphiText.StartsText("[v4+", trimmed);
            return Section.Styles;
        }

        return DelphiText.StartsText("[Events]", trimmed) ? Section.Events : Section.Footer;
    }

    /// <summary>TStringList.Values com ':' como separador: primeira linha cujo nome bate (sem diferenciar maiúsculas).</summary>
    private static string ScriptInfoValue(List<string> lines, string key)
    {
        foreach (string line in lines)
        {
            int p = line.IndexOf(':', StringComparison.Ordinal);
            if (p >= 0 && string.Equals(line[..p], key, StringComparison.OrdinalIgnoreCase))
            {
                return line[(p + 1)..];
            }
        }

        return string.Empty;
    }

    private static SsaStyle ReadStyle(FormattedSection styles, int i, bool isAss)
    {
        var style = new SsaStyle
        {
            Name = styles.Get(i, "Name"),
            FontName = styles.Get(i, "Fontname"),
            FontSize = styles.GetDouble(i, "Fontsize"),
            PrimaryColor = SsaStyle.ParseColor(styles.Get(i, "PrimaryColour")),
            SecondaryColor = SsaStyle.ParseColor(styles.Get(i, "SecondaryColour")),
            OutlineColor = SsaStyle.ParseColor(styles.Get(i, styles.KeyExists("TertiaryColour") ? "TertiaryColour" : "OutlineColour")),
            BackColor = SsaStyle.ParseColor(styles.Get(i, "BackColour")),
            Bold = styles.GetInt(i, "Bold"),
            Italic = styles.GetInt(i, "Italic"),
            Underline = styles.GetInt(i, "Underline"),
            Strikeout = styles.GetInt(i, "Strikeout"),
            Spacing = styles.GetDouble(i, "Spacing"),
            Angle = styles.GetDouble(i, "Angle"),
            BorderStyle = styles.GetInt(i, "BorderStyle"),
            Outline = styles.GetDouble(i, "Outline"),
            Shadow = styles.GetDouble(i, "Shadow"),
            MarginL = styles.GetInt(i, "MarginL"),
            MarginR = styles.GetInt(i, "MarginR"),
            MarginV = styles.GetInt(i, "MarginV"),
            AlphaLevel = styles.GetInt(i, "AlphaLevel"),
            Encoding = styles.GetInt(i, "Encoding"),
        };
        if (styles.KeyExists("ScaleX"))
        {
            style.ScaleX = styles.GetDouble(i, "ScaleX");
        }

        if (styles.KeyExists("ScaleY"))
        {
            style.ScaleY = styles.GetDouble(i, "ScaleY");
        }

        int alignment = styles.GetInt(i, "Alignment");
        style.Alignment = isAss ? alignment : SsaStyle.SsaToAssAlignment(alignment);
        return style;
    }

    /// <summary>TFormatedParser: linha "Format:" define as chaves; o último campo leva o resto da linha.</summary>
    private sealed class FormattedSection
    {
        private readonly List<string> _keywords = [];
        private readonly List<List<string>> _lines = [];

        public int Count => _lines.Count;

        public void ParseLine(string line, string valuePrefix)
        {
            if (DelphiText.StartsText("Format:", line))
            {
                foreach (string key in line["Format:".Length..].Split(','))
                {
                    _keywords.Add(DelphiText.Trim(key));
                }
            }
            else if (DelphiText.StartsText(valuePrefix, line))
            {
                var values = new List<string>();
                string rest = line[valuePrefix.Length..];
                while (true)
                {
                    int p = rest.IndexOf(',', StringComparison.Ordinal);
                    if (values.Count == _keywords.Count - 1 || p < 0)
                    {
                        values.Add(DelphiText.Trim(rest));
                        break;
                    }

                    values.Add(DelphiText.Trim(rest[..p]));
                    rest = rest[(p + 1)..];
                }

                _lines.Add(values);
            }
        }

        public bool KeyExists(string key) => IndexOf(key) >= 0;

        public string Get(int line, string key)
        {
            int k = IndexOf(key);
            return k >= 0 && line < _lines.Count && k < _lines[line].Count ? _lines[line][k] : string.Empty;
        }

        public int GetInt(int line, string key) => DelphiText.StrToIntDef(Get(line, key), 0);

        public double GetDouble(int line, string key) => DelphiText.StrToFloatDef(Get(line, key), 0.0);

        private int IndexOf(string key) => _keywords.FindIndex(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
    }
}
