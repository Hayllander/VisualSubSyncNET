using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Projects;

/// <summary>TMainForm.ProcessParams: o projeto a abrir quando o programa recebe um arquivo.</summary>
public static class StartupProject
{
    private static readonly string[] VideoExtensions = [".avi", ".mkv"];

    /// <summary>
    /// Um .vssprj abre direto. Uma legenda (.srt, .ssa, .ass) abre o .vssprj de mesmo nome ou, se ele
    /// não existir, cria um com a legenda, o vídeo .avi/.mkv e o .peak de mesmo nome que existirem.
    /// Qualquer outro arquivo não abre nada.
    /// </summary>
    public static string? Resolve(string argument, bool detachedVideo, bool showVideo)
    {
        ArgumentNullException.ThrowIfNull(argument);
        string ext = Path.GetExtension(argument).ToLowerInvariant();
        if (ext == ".vssprj")
        {
            return argument;
        }

        if (ext is not (".srt" or ".ssa" or ".ass"))
        {
            return null;
        }

        string projectName = Path.ChangeExtension(argument, ".vssprj");
        if (File.Exists(projectName))
        {
            return projectName;
        }

        const string S = "VisualSubsync";
        var ini = new IniDocument();
        ini.Set(S, "SubtitlesFile", argument);
        foreach (string videoExt in VideoExtensions)
        {
            string video = Path.ChangeExtension(argument, videoExt);
            if (File.Exists(video))
            {
                ini.Set(S, "VideoSource", video);
            }
        }

        string peak = Path.ChangeExtension(argument, ".peak");
        if (File.Exists(peak))
        {
            ini.Set(S, "PeakFile", peak);
            ini.SetInt(S, "WAVMode", (int)ProjectWavMode.PeakOnly);
        }
        else
        {
            ini.SetInt(S, "WAVMode", (int)ProjectWavMode.NoWaveform);
        }

        ini.SetBool(S, "DetachedVideo", detachedVideo);
        ini.SetBool(S, "ShowVideo", showVideo);
        ini.Save(projectName);
        return projectName;
    }
}
