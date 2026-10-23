using System.Text;
using VisualSubSync.Core.Projects;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Tests;

public class ProjectFileTests
{
    // Projeto como o VisualSubSync 1.0.1 grava (UTF-16 LE, caminhos relativos).
    private const string Legacy = """
        [VisualSubsync]
        VideoSource=video\filme.avi
        WAVFile=filme.wav
        PeakFile=
        WAVMode=1
        SubtitlesFile=filme.srt
        SubtitlesVO=..\vo\filme.en.srt
        VideoPanelWidth=320
        VideoPanelHeight=240
        ShowVideo=1
        ShowVO=0
        DetachedVideo=0
        WAVDisplayerPositionStartMs=1000
        WAVDisplayerPositionStopMs=16000
        FocusedTimeMs=-1
        Dictionnary=pt_BR
        Presets=
        ChaveFutura=preservar

        """;

    private const string ProjectPath = @"C:\Legendas\Filme\filme.vssprj";

    [Fact]
    public void Read_resolves_relative_paths_and_defaults()
    {
        var p = VssProjectFile.Read(IniDocument.Parse(Legacy), ProjectPath);

        Assert.Equal(@"C:\Legendas\Filme\video\filme.avi", p.VideoSource);
        Assert.Equal(@"C:\Legendas\vo\filme.en.srt", p.SubtitlesVO);
        Assert.Equal(string.Empty, p.PeakFile);
        Assert.Equal(ProjectWavMode.External, p.WavMode);
        Assert.True(p.ShowVideo);
        Assert.Equal("pt_BR", p.Dictionary);
        Assert.Equal(15_000, p.WavDisplayerPositionStopMs - p.WavDisplayerPositionStartMs);
    }

    [Fact]
    public void Write_produces_relative_paths_and_keeps_unknown_keys()
    {
        var ini = IniDocument.Parse(Legacy);
        var p = VssProjectFile.Read(ini, ProjectPath);
        p.SubtitlesFile = @"C:\Legendas\Filme\novo.srt";
        p.WavFile = @"D:\Audio\filme.wav";

        VssProjectFile.Write(p, ini, ProjectPath);

        Assert.Equal("novo.srt", ini.Get("VisualSubsync", "SubtitlesFile"));
        Assert.Equal(@"..\vo\filme.en.srt", ini.Get("VisualSubsync", "SubtitlesVO"));
        Assert.Equal(@"D:\Audio\filme.wav", ini.Get("VisualSubsync", "WAVFile"));
        Assert.Equal("preservar", ini.Get("VisualSubsync", "ChaveFutura"));
        Assert.Null(ini.Get("VisualSubsync", "TextPipeSource"));
    }

    [Fact]
    public void Save_writes_utf16_with_bom_and_reloads()
    {
        string path = Path.Combine(Path.GetTempPath(), $"vss-{Guid.NewGuid():N}.vssprj");
        try
        {
            File.WriteAllText(path, Legacy, new UnicodeEncoding(false, true));
            var p = VssProjectFile.Load(path);
            p.Dictionary = "fr_FR";
            VssProjectFile.Save(p, path);

            byte[] bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
            Assert.Equal("fr_FR", VssProjectFile.Load(path).Dictionary);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
