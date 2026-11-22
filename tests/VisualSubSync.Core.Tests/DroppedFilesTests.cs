using VisualSubSync.Core.Projects;

namespace VisualSubSync.Core.Tests;

public sealed class DroppedFilesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("vss-drop").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Project_file_wins_over_everything_else()
    {
        var dropped = DroppedFiles.Classify([@"C:\a\film.mkv", @"C:\a\film.vssprj", @"C:\a\other.vssprj"]);
        Assert.Equal(@"C:\a\film.vssprj", dropped.ProjectFile);
        Assert.False(dropped.IsEmpty);
    }

    [Fact]
    public void Media_files_are_sorted_by_kind()
    {
        var dropped = DroppedFiles.Classify(
            [@"C:\a\film.PT.srt", @"C:\a\Film.MP4", @"C:\a\film.en.ass", @"C:\a\film.wav", @"C:\a\film.peak", @"C:\a\notes.txt"]);

        Assert.Null(dropped.ProjectFile);
        Assert.Equal(@"C:\a\Film.MP4", dropped.VideoFile);
        Assert.Equal(@"C:\a\film.PT.srt", dropped.SubtitleFile);
        Assert.Equal(@"C:\a\film.en.ass", dropped.SubtitleVoFile);
        Assert.Equal(@"C:\a\film.wav", dropped.WavFile);
        Assert.Equal(@"C:\a\film.peak", dropped.PeakFile);
        Assert.True(dropped.HasNewProjectFiles);
    }

    [Fact]
    public void Unknown_files_and_folders_are_ignored()
    {
        var dropped = DroppedFiles.Classify([_dir, Path.Combine(_dir, "readme.txt"), ""]);
        Assert.True(dropped.IsEmpty);
        Assert.False(DroppedFiles.IsSupported("a.txt"));
        Assert.True(DroppedFiles.IsSupported("a.webm"));
        Assert.True(DroppedFiles.IsSupported("a.SSA"));
    }

    [Fact]
    public void Subtitle_alone_is_enough_for_a_new_project()
    {
        var dropped = DroppedFiles.Classify([@"C:\a\film.srt"]);
        Assert.True(dropped.HasNewProjectFiles);
        Assert.Null(dropped.VideoFile);
        Assert.Null(dropped.SubtitleVoFile);
    }
}
