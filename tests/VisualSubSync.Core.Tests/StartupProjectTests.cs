using VisualSubSync.Core.Projects;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Tests;

public sealed class StartupProjectTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("vss-startup").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Subtitle_creates_a_project_with_matching_video_and_peak()
    {
        string srt = Path.Combine(_dir, "film.srt");
        File.WriteAllText(srt, string.Empty);
        File.WriteAllText(Path.Combine(_dir, "film.mkv"), string.Empty);
        File.WriteAllText(Path.Combine(_dir, "film.peak"), string.Empty);

        string? project = StartupProject.Resolve(srt, detachedVideo: false, showVideo: true);

        Assert.Equal(Path.Combine(_dir, "film.vssprj"), project);
        // Os valores como foram gravados (o caminho absoluto recebido, como no original).
        var ini = IniDocument.Load(project!);
        Assert.Equal(srt, ini.Get("VisualSubsync", "SubtitlesFile"));
        Assert.Equal(Path.Combine(_dir, "film.mkv"), ini.Get("VisualSubsync", "VideoSource"));
        Assert.Equal(Path.Combine(_dir, "film.peak"), ini.Get("VisualSubsync", "PeakFile"));
        Assert.Equal("2", ini.Get("VisualSubsync", "WAVMode"));
        Assert.Equal("0", ini.Get("VisualSubsync", "DetachedVideo"));
        Assert.Equal("1", ini.Get("VisualSubsync", "ShowVideo"));
    }

    [Fact]
    public void Existing_project_with_the_same_name_is_used()
    {
        string srt = Path.Combine(_dir, "a.ass");
        string prj = Path.Combine(_dir, "a.vssprj");
        File.WriteAllText(prj, "[VisualSubsync]");
        Assert.Equal(prj, StartupProject.Resolve(srt, false, false));
    }

    [Fact]
    public void Other_files_open_nothing()
    {
        Assert.Null(StartupProject.Resolve(Path.Combine(_dir, "a.txt"), false, false));
        Assert.Equal("x.vssprj", StartupProject.Resolve("x.vssprj", false, false));
    }
}
