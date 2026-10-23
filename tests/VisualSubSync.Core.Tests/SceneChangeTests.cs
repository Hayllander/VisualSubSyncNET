using VisualSubSync.Core.Scenes;

namespace VisualSubSync.Core.Tests;

public class SceneChangeTests
{
    [Fact]
    public void SceneChange_roundtrip_and_ignores_unknown_version()
    {
        string path = Path.Combine(Path.GetTempPath(), $"vss-{Guid.NewGuid():N}.scenechange");
        try
        {
            SceneChangeFile.Save(path, [41, 3_006_969]);
            Assert.StartsWith("SceneChangeFormatVersion=1\r\n00:00:00.041", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Equal([41, 3_006_969], SceneChangeFile.Load(path));
            Assert.Empty(SceneChangeFile.Parse("SceneChangeFormatVersion=2\r\n00:00:01.000"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
