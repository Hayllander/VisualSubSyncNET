using System.Globalization;
using System.Text;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.Core.Subtitles.Formats;

/// <summary>Formatos só de exportação: CUE, TXT e CSV (SaveSubtitlesAsCUE/TXT/CSV).</summary>
public static class ExportFormats
{
    /// <summary>Folha CUE com uma faixa por início e outra por fim de cada legenda. Sempre gravada em ANSI.</summary>
    public static string FormatCue(SubtitleDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sb = new StringBuilder();
        void Line(string s) => sb.Append(s).Append("\r\n");

        Line("FILE \"YourFileHere.wav\" WAVE");
        Line("  PERFORMER \"Performer Here\"");
        Line("  TRACK 01 AUDIO");
        Line("    INDEX 01 00:00:00");
        for (int i = 0; i < document.Items.Count; i++)
        {
            var sub = document.Items[i];
            Line(string.Create(CultureInfo.InvariantCulture, $"  TRACK {(i * 2) + 2:00} AUDIO"));
            Line("    INDEX 01 " + TimeFormat.ToCue(sub.StartMs));
            Line("    TITLE \"" + SubtitleTags.JoinLines(sub.Text) + "\"");
            Line(string.Create(CultureInfo.InvariantCulture, $"  TRACK {(i * 2) + 3:00} AUDIO"));
            Line("    INDEX 01 " + TimeFormat.ToCue(sub.StopMs));
        }

        return sb.ToString();
    }

    /// <summary>Uma linha de texto por legenda.</summary>
    public static string FormatTxt(SubtitleDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sb = new StringBuilder();
        foreach (var sub in document.Items)
        {
            sb.Append(SubtitleTags.JoinLines(sub.Text)).Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>Index,Start,Stop,Text com tempos hh:mm:ss.mmm e aspas duplicadas no texto.</summary>
    public static string FormatCsv(SubtitleDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sb = new StringBuilder("Index,Start,Stop,Text\r\n");
        for (int i = 0; i < document.Items.Count; i++)
        {
            var sub = document.Items[i];
            string text = SubtitleTags.JoinLines(sub.Text).Replace("\"", "\"\"", StringComparison.Ordinal);
            sb.Append(string.Create(CultureInfo.InvariantCulture,
                $"{i + 1},{TimeFormat.ToVss(sub.StartMs)},{TimeFormat.ToVss(sub.StopMs)},\"{text}\"\r\n"));
        }

        return sb.ToString();
    }
}
