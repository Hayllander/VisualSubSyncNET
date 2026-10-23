using System.Globalization;
using VisualSubSync.Core.Web;

namespace VisualSubSync.Core.Audio;

/// <summary>
/// ActionExportToWAVExecute: um WAV por legenda (000001.wav, 000002.wav...) e a página index_wav.html
/// gerada a partir de web\index_wav_export.shtml.
/// </summary>
public static class WavExport
{
    public const string TemplateName = "index_wav_export.shtml";

    public const string PageName = "index_wav.html";

    public static string FileName(string folder, int index) =>
        Path.Combine(folder, index.ToString("D6", CultureInfo.InvariantCulture) + ".wav");

    /// <param name="templatePath">ServerRootDir + 'index_wav_export.shtml'; a página não é gerada quando falta.</param>
    public static void Export(WavFile wav, string folder, PageContext context, string templatePath)
    {
        for (int i = 0; i < context.Subtitles.Count; i++)
        {
            var sub = context.Subtitles[i];
            using var output = new FileStream(FileName(folder, i + 1), FileMode.Create, FileAccess.Write);
            wav.ExtractToStream(sub.StartMs, sub.StopMs, output);
        }

        if (File.Exists(templatePath))
        {
            File.WriteAllBytes(Path.Combine(folder, PageName), PageProcessor.ProcessPage(File.ReadAllBytes(templatePath), context));
        }
    }
}
