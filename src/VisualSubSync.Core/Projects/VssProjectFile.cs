using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Projects;

/// <summary>
/// Leitura e gravação de .vssprj com as mesmas chaves, padrões e caminhos relativos de
/// TVSSProject.LoadFromINIFile e TMainForm.SaveProject. Chaves e seções desconhecidas são preservadas.
/// </summary>
public static class VssProjectFile
{
    public const string Extension = ".vssprj";

    /// <summary>Texto do painel "text pipe", gravado ao lado do projeto em RTF.</summary>
    public const string TextPipeExtension = ".vssscript";
    private const string S = "VisualSubsync";

    public static VssProject Load(string path) => Read(IniDocument.Load(path), path);

    public static VssProject Read(IniDocument ini, string path)
    {
        ArgumentNullException.ThrowIfNull(ini);
        string Rel(string key) => WindowsPath.Resolve(path, ini.GetString(S, key, string.Empty));

        return new VssProject
        {
            FileName = path,
            VideoSource = Rel("VideoSource"),
            WavFile = Rel("WAVFile"),
            PeakFile = Rel("PeakFile"),
            WavMode = (ProjectWavMode)ini.GetInt(S, "WAVMode", 1),
            SubtitlesFile = Rel("SubtitlesFile"),
            SubtitlesVO = Rel("SubtitlesVO"),
            TextPipeSource = Rel("TextPipeSource"),
            TextPipePosition = ini.GetInt(S, "TextPipePosition", 0),
            VideoPanelWidth = ini.GetInt(S, "VideoPanelWidth", 0),
            VideoPanelHeight = ini.GetInt(S, "VideoPanelHeight", 0),
            ShowVideo = ini.GetBool(S, "ShowVideo", false),
            ShowVO = ini.GetBool(S, "ShowVO", false),
            DetachedVideo = ini.GetBool(S, "DetachedVideo", false),
            VideoWindowNormalLeft = ini.GetInt(S, "VideoWindowNormalLeft", 0),
            VideoWindowNormalTop = ini.GetInt(S, "VideoWindowNormalTop", 0),
            VideoWindowNormalWidth = ini.GetInt(S, "VideoWindowNormalWidth", 0),
            VideoWindowNormalHeight = ini.GetInt(S, "VideoWindowNormalHeight", 0),
            WavDisplayerPositionStartMs = ini.GetInt(S, "WAVDisplayerPositionStartMs", -1),
            WavDisplayerPositionStopMs = ini.GetInt(S, "WAVDisplayerPositionStopMs", -1),
            FocusedTimeMs = ini.GetInt(S, "FocusedTimeMs", -1),
            Dictionary = ini.GetString(S, "Dictionnary", string.Empty),
            Presets = ini.GetString(S, "Presets", string.Empty),
        };
    }

    /// <summary>Grava sobre o arquivo existente (se houver) para preservar o que esta versão não conhece.</summary>
    /// <param name="textPipeHasContent">O legado só grava TextPipe* quando o painel de texto não está vazio
    /// (o conteúdo vai para o arquivo .vssscript ao lado do projeto).</param>
    public static void Save(VssProject project, string path, bool textPipeHasContent = false)
    {
        var ini = File.Exists(path) ? IniDocument.Load(path) : new IniDocument();
        Write(project, ini, path, textPipeHasContent);
        ini.Save(path);
        project.FileName = path;
    }

    public static void Write(VssProject project, IniDocument ini, string path, bool textPipeHasContent = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(ini);
        void Rel(string key, string value) => ini.Set(S, key, WindowsPath.MakeRelative(path, value));

        Rel("VideoSource", project.VideoSource);
        Rel("WAVFile", project.WavFile);
        Rel("PeakFile", project.PeakFile);
        ini.SetInt(S, "WAVMode", (int)project.WavMode);
        Rel("SubtitlesFile", project.SubtitlesFile);
        Rel("SubtitlesVO", project.SubtitlesVO);
        if (textPipeHasContent)
        {
            ini.SetInt(S, "TextPipePosition", project.TextPipePosition);
            Rel("TextPipeSource", project.TextPipeSource);
        }

        ini.SetInt(S, "VideoPanelWidth", project.VideoPanelWidth);
        ini.SetInt(S, "VideoPanelHeight", project.VideoPanelHeight);
        ini.SetBool(S, "ShowVideo", project.ShowVideo);
        ini.SetBool(S, "ShowVO", project.ShowVO);
        ini.SetBool(S, "DetachedVideo", project.DetachedVideo);
        ini.SetInt(S, "VideoWindowNormalLeft", project.VideoWindowNormalLeft);
        ini.SetInt(S, "VideoWindowNormalTop", project.VideoWindowNormalTop);
        ini.SetInt(S, "VideoWindowNormalWidth", project.VideoWindowNormalWidth);
        ini.SetInt(S, "VideoWindowNormalHeight", project.VideoWindowNormalHeight);
        ini.SetInt(S, "WAVDisplayerPositionStartMs", project.WavDisplayerPositionStartMs);
        ini.SetInt(S, "WAVDisplayerPositionStopMs", project.WavDisplayerPositionStopMs);
        ini.SetInt(S, "FocusedTimeMs", project.FocusedTimeMs);
        ini.Set(S, "Dictionnary", project.Dictionary);
        ini.Set(S, "Presets", project.Presets);
    }
}
