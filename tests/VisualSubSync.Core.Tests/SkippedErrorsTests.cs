using VisualSubSync.Core.Projects;

namespace VisualSubSync.Core.Tests;

/// <summary>"Clear &amp; Forget" do Enhanced: SkippedSubNNN e LastSkippedOnErrorCheck no .vssprj.</summary>
public sealed class SkippedErrorsTests
{
    [Fact]
    public void Reads_the_enhanced_keys_and_appends_new_texts_once()
    {
        string folder = Directory.CreateTempSubdirectory("vss-skip-").FullName;
        try
        {
            string path = Path.Combine(folder, "a.vssprj");
            File.WriteAllText(path, "[VisualSubsync]\r\nVideoSource=a.avi\r\nSkippedSub001=Hello|World\r\nLastSkippedOnErrorCheck=1\r\n");
            Assert.Equal(["Hello|World"], SkippedErrors.Load(path));

            SkippedErrors.Add(path, ["Hello|World", "Other", "Other"]);
            var ini = Text.IniDocument.Load(path);
            Assert.Equal("Other", ini.Get("VisualSubsync", "SkippedSub002"));
            Assert.Equal("2", ini.Get("VisualSubsync", "LastSkippedOnErrorCheck"));
            Assert.Equal("a.avi", ini.Get("VisualSubsync", "VideoSource"));
            Assert.Equal(2, SkippedErrors.Load(path).Count);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Without_a_project_file_nothing_is_skipped()
    {
        SkippedErrors.Add(string.Empty, ["x"]);
        Assert.Empty(SkippedErrors.Load(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid() + ".vssprj")));
    }
}
