using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Tests;

public class SubtitlePreviewTests
{
    [Fact]
    public void Copy_goes_to_the_visualsubsync_temporary_folder()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "x");
        Assert.Equal(Path.Combine(tmp, "VisualSubSync", "film.srt"),
            SubtitlePreview.TemporaryFileName(Path.Combine("dir", "film.srt"), tmp));
    }

    [Fact]
    public void Without_temporary_folder_the_copy_stays_beside_the_file()
    {
        Assert.Equal(Path.Combine("dir", "~film.srt"), SubtitlePreview.TemporaryFileName(Path.Combine("dir", "film.srt"), string.Empty));
    }
}
