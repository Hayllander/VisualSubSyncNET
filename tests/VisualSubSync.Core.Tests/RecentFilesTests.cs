using VisualSubSync.Core.Projects;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Tests;

public class RecentFilesTests
{
    [Fact]
    public void Added_file_goes_to_the_top_and_the_list_keeps_eight()
    {
        var mru = new RecentFiles();
        for (int i = 0; i < 10; i++)
        {
            mru.Add($@"C:\p{i}.vssprj");
        }

        Assert.Equal(8, mru.Files.Count);
        Assert.Equal(@"C:\p9.vssprj", mru.MostRecent);
        mru.Add(@"c:\P5.VSSPRJ");
        Assert.Equal(@"C:\p5.vssprj", mru.MostRecent);
        Assert.Equal(8, mru.Files.Count);
    }

    [Fact]
    public void Ini_round_trip_writes_all_eight_keys()
    {
        var mru = new RecentFiles();
        mru.Add(@"C:\a.vssprj");
        var ini = new IniDocument();
        mru.Save(ini);
        Assert.Equal(@"C:\a.vssprj", ini.Get("MRUList", "Recent0"));
        Assert.Equal(string.Empty, ini.Get("MRUList", "Recent7"));
        var loaded = new RecentFiles();
        loaded.Load(ini);
        Assert.Equal([@"C:\a.vssprj"], loaded.Files);
    }

    [Fact]
    public void Utf8_inside_ansi_round_trips()
    {
        string path = @"C:\Vídeos\ação.vssprj";
        Assert.Equal(path, RecentFiles.Utf8Decode(RecentFiles.Utf8Encode(path)));
    }

    [Fact]
    public void Dead_entries_are_removed()
    {
        var mru = new RecentFiles();
        mru.Add("a");
        mru.Add("b");
        mru.ClearDeadEntries(f => f == "a");
        Assert.Equal(["a"], mru.Files);
    }

    [Fact]
    public void Accented_paths_survive_writing_and_reading_the_ini_file()
    {
        string path = Path.Combine(Path.GetTempPath(), $"vss-mru-{Guid.NewGuid():N}.ini");
        try
        {
            var mru = new RecentFiles();
            mru.Add(@"D:\Legendas\Coração.vssprj");
            var ini = new IniDocument();
            mru.Save(ini);
            ini.Save(path, TextFileReader.Ansi);

            var loaded = new RecentFiles();
            loaded.Load(IniDocument.LoadAnsi(path));
            Assert.Equal([@"D:\Legendas\Coração.vssprj"], loaded.Files);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
