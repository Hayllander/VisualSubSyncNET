using System.Globalization;
using System.Text;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Text;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.Core.Web;

/// <summary>
/// TGlobalContext (GlobalUnit.pas): os campos válidos em toda a página e a lista que os blocos
/// SUBTITLES_LIST e SELECTED_SUB percorrem.
/// </summary>
/// <param name="Version">g_ApplicationVersion.VersionString.</param>
/// <param name="ProjectFileName">CurrentProject.Filename (só o nome vai para a página).</param>
/// <param name="WavAverageBytesPerSecond">Usado no campo wav-size.</param>
public sealed record PageContext(string Version, string ProjectFileName, IReadOnlyList<Subtitle> Subtitles, int WavAverageBytesPerSecond = 0);

/// <summary>
/// TPageProcessor (PageProcessorUnit.pas): troca os campos <c>$|nome|$</c> e repete os blocos
/// <c>&lt;!--$TMPL_...$--&gt;</c>. Trabalha sobre os bytes, como o legado: o modelo é copiado sem
/// conversão, os campos terminados em 8 saem em UTF-8 e os demais na página de código ANSI.
/// </summary>
public static class PageProcessor
{
    private const string StartFieldMark = "$|";
    private const string EndFieldMark = "|$";
    private const string BlockStart = "<!--$TMPL_";
    private const string BlockStartEnd = "$-->";

    public static byte[] ProcessPage(byte[] template, PageContext context, IReadOnlyDictionary<string, string>? envVars = null)
    {
        envVars ??= new Dictionary<string, string>();
        string input = Encoding.Latin1.GetString(template);
        var output = new StringBuilder(input.Length);
        var env = new Dictionary<string, string>(envVars, StringComparer.OrdinalIgnoreCase);
        var global = new Fields(context, env, null, 0);

        int pos2 = 0;
        while (true)
        {
            int pos1 = input.IndexOf(BlockStart, pos2, StringComparison.Ordinal);
            if (pos1 == -1)
            {
                // O término normal é aqui.
                CopyAndReplaceFields(input, pos2, input.Length, output, global);
                break;
            }

            CopyAndReplaceFields(input, pos2, pos1, output, global);
            pos1 += BlockStart.Length;
            pos2 = input.IndexOf(BlockStartEnd, pos1, StringComparison.Ordinal);
            if (pos2 == -1)
            {
                output.Append(CultureInfo.InvariantCulture, $"ERROR : End of \"{BlockStart}\" not found at pos {pos1}");
                break;
            }

            string blockName = input[pos1..pos2];
            pos2 += BlockStartEnd.Length;
            string blockEnd = BlockStart + blockName + "_END" + BlockStartEnd;
            pos1 = input.IndexOf(blockEnd, pos2, StringComparison.Ordinal);
            if (pos1 == -1)
            {
                output.Append(CultureInfo.InvariantCulture, $"ERROR : \"{blockEnd}\" not found for block \"{blockName}\" at pos {pos2}");
                break;
            }

            if (blockName == "SUBTITLES_LIST")
            {
                for (int i = 0; i < context.Subtitles.Count; i++)
                {
                    CopyAndReplaceFields(input, pos2, pos1, output, new Fields(context, env, context.Subtitles[i], i + 1));
                }
            }
            else if (blockName == "SELECTED_SUB")
            {
                int i = (env.TryGetValue("idx", out string? idx) && int.TryParse(idx, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0) - 1;
                var fields = i >= 0 && i < context.Subtitles.Count ? new Fields(context, env, context.Subtitles[i], i + 1) : global;
                CopyAndReplaceFields(input, pos2, pos1, output, fields);
            }
            else
            {
                output.Append("ERROR : Unkown block ").Append(blockName);
                break;
            }

            pos2 = pos1 + blockEnd.Length;
        }

        return Encoding.Latin1.GetBytes(output.ToString());
    }

    private static void CopyAndReplaceFields(string input, int start, int stop, StringBuilder output, Fields fields)
    {
        int pos2 = start;
        while (pos2 < stop)
        {
            int pos1 = IndexWithin(input, StartFieldMark, pos2, stop);
            if (pos1 == -1)
            {
                output.Append(input, pos2, stop - pos2);
                break;
            }

            output.Append(input, pos2, pos1 - pos2);
            pos1 += StartFieldMark.Length;
            pos2 = IndexWithin(input, EndFieldMark, pos1, stop);
            if (pos2 == -1)
            {
                output.Append(StartFieldMark);
                pos2 = pos1;
            }
            else
            {
                output.Append(fields.Resolve(input[pos1..pos2]));
                pos2 += EndFieldMark.Length;
            }
        }
    }

    /// <summary>PosStreamEx: a ocorrência tem de caber inteira antes de <paramref name="stop"/>.</summary>
    private static int IndexWithin(string input, string value, int start, int stop) =>
        stop - start < value.Length ? -1 : input.IndexOf(value, start, stop - start, StringComparison.Ordinal);

    /// <summary>TSubtitleRangeWrapper com Parent = g_GlobalContext, seguidos das variáveis de ambiente.</summary>
    private sealed class Fields(PageContext context, Dictionary<string, string> env, Subtitle? subtitle, int index)
    {
        public string Resolve(string name)
        {
            string? value = (subtitle is null ? null : SubtitleField(name, subtitle, index, context.WavAverageBytesPerSecond)) ?? GlobalField(name);
            if (value is not null)
            {
                return value;
            }

            // As variáveis de ambiente já vêm em bytes (Latin-1), como as strings do legado.
            return env.TryGetValue(name, out string? envValue) && envValue.Length > 0
                ? envValue
                : StartFieldMark + name + EndFieldMark;
        }

        private string? GlobalField(string name) => name switch
        {
            "version" => Ansi(context.Version),
            "project-filename" => Ansi(Path.GetFileName(context.ProjectFileName)),
            _ => null,
        };
    }

    /// <summary>TSubtitleRangeWrapper.GetFieldValue; null quando o campo não é de legenda.</summary>
    private static string? SubtitleField(string name, Subtitle subtitle, int index, int wavAverageBytesPerSecond) => name switch
    {
        "index" => index.ToString(CultureInfo.InvariantCulture),
        "time-index" => string.Create(CultureInfo.InvariantCulture, $"{subtitle.StartMs:X}-{subtitle.StopMs:X}"),
        "start" => TimeFormat.ToVss(subtitle.StartMs),
        "stop" => TimeFormat.ToVss(subtitle.StopMs),
        "html-text" => Ansi(ConvertCrLfToBr(subtitle.Text)),
        "raw-text" => Ansi(subtitle.Text),
        "stripped-text" => Ansi(SubtitleTextStats.StripTags(subtitle.Text)),
        "html-text8" => Utf8(ConvertCrLfToBr(subtitle.Text)),
        "raw-text8" => Utf8(subtitle.Text),
        "wav-size" => WavSize((int)Math.Round((subtitle.StopMs - subtitle.StartMs) / 1000.0 * wavAverageBytesPerSecond)),
        "index-padded" => index.ToString("D6", CultureInfo.InvariantCulture),
        _ => null,
    };

    private static string WavSize(int bytes) => bytes switch
    {
        < 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024 / 1024} MB"),
    };

    /// <summary>StringConvertCRLFToBR: cada #13 vira &lt;br&gt; e o caractere seguinte é descartado.</summary>
    public static string ConvertCrLfToBr(string text)
    {
        var sb = new StringBuilder(text.Length + 16);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                sb.Append("<br>");
                i++;
            }
            else
            {
                sb.Append(text[i]);
            }
        }

        return sb.ToString();
    }

    private static string Ansi(string s) => Encoding.Latin1.GetString(TextFileReader.Ansi.GetBytes(s));

    private static string Utf8(string s) => Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(s));
}
