using System.Text;
using VisualSubSync.Core.Text;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.Core.Subtitles.Formats;

/// <summary>SRT conforme TSRTParser (SRTParserUnit.pas) e TMainForm.SaveSubtitlesAsSRT.</summary>
public static class SrtFormat
{
    /// <summary>
    /// Porta fiel de TSRTParser.Load: tolera lixo antes do primeiro tempo, legendas sem linha em branco
    /// e sem índice, remove o índice da legenda seguinte e corrige fim == início da próxima (fim - 1 ms).
    /// </summary>
    public static SubtitleDocument Parse(string text) => Parse(text, sort: true);

    /// <summary>
    /// <paramref name="sort"/> falso: na ordem do texto, como o TSRTParser (o FullSort é do LoadSRT; o
    /// TryPasteAsSRT não ordena).
    /// </summary>
    internal static SubtitleDocument Parse(string text, bool sort)
    {
        var source = DelphiText.SplitLines(text);
        source.Add(string.Empty); // o legado acrescenta uma linha vazia antes de analisar
        var document = new SubtitleDocument();
        int lineIndex = 0;
        int start = -1, stop = -1, nextStart = -1, nextStop = -1;

        while (lineIndex < source.Count)
        {
            string s = source[lineIndex++];
            if (IsTimeStampsLine(s, ref start, ref stop))
            {
                break;
            }
        }

        while (lineIndex < source.Count)
        {
            var subText = new StringBuilder();
            while (lineIndex < source.Count)
            {
                string s = source[lineIndex++];
                if (IsTimeStampsLine(s, ref nextStart, ref nextStop))
                {
                    break;
                }

                subText.Append(DelphiText.Trim(s)).Append("\r\n");
            }

            string body = DelphiText.TrimRight(subText.ToString());
            if (start != -1 && stop != -1)
            {
                if (stop == nextStart)
                {
                    document.AutoCorrected = true;
                    stop--;
                }

                int i = body.LastIndexOf("\r\n", StringComparison.Ordinal);
                if (i >= 0 && DelphiText.StrToIntDef(body[(i + 2)..], -1) != -1 && lineIndex < source.Count)
                {
                    body = body[..i];
                }

                document.Items.Add(new Subtitle { StartMs = start, StopMs = stop, Text = DelphiText.Trim(body) });
            }

            start = nextStart;
            stop = nextStop;
        }

        if (sort)
        {
            document.Sort();
        }

        return document;
    }

    /// <summary>IsTimeStampsLine: altera início/fim sempre que a linha contém "--&gt;", mesmo se inválida.</summary>
    private static bool IsTimeStampsLine(string line, ref int start, ref int stop)
    {
        int i = line.IndexOf("-->", StringComparison.Ordinal);
        if (i < 0)
        {
            return false;
        }

        start = TimeFormat.ParseVss(DelphiText.Trim(line[..i]));
        stop = TimeFormat.ParseVss(DelphiText.Trim(line[(i + 3)..]));
        return start != -1 && stop != -1;
    }

    public static string Format(SubtitleDocument document, SubtitleWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new SubtitleWriteOptions();
        bool fromSsa = options.ConvertsFrom(".ass", ".ssa");
        var sb = new StringBuilder();
        for (int i = 0; i < document.Items.Count; i++)
        {
            var sub = document.Items[i];
            sb.Append(i + 1).Append("\r\n")
              .Append(TimeFormat.ToSrt(sub.StartMs)).Append(" --> ").Append(TimeFormat.ToSrt(sub.StopMs)).Append("\r\n")
              .Append(fromSsa ? SubtitleTags.SsaToSrt(options.TranslateText(sub.Text)) : options.TranslateText(sub.Text)).Append("\r\n\r\n");
        }

        return sb.ToString();
    }
}
