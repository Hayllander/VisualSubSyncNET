using System.Text;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Subtitles.Formats;
using VisualSubSync.Core.Text;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.Core.Tests;

public sealed class ExportAndEncodingTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("vss-").FullName;

    static ExportAndEncodingTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static SubtitleDocument Doc(params (int Start, int Stop, string Text)[] items)
    {
        var doc = new SubtitleDocument();
        doc.Items.AddRange(items.Select(i => new Subtitle { StartMs = i.Start, StopMs = i.Stop, Text = i.Text }));
        return doc;
    }

    [Fact]
    public void Cue_matches_legacy_layout()
    {
        string cue = ExportFormats.FormatCue(Doc((1000, 3_723_500, "A \r\n B\r\nC")));

        Assert.Equal(
            "FILE \"YourFileHere.wav\" WAVE\r\n  PERFORMER \"Performer Here\"\r\n  TRACK 01 AUDIO\r\n    INDEX 01 00:00:00\r\n" +
            "  TRACK 02 AUDIO\r\n    INDEX 01 00:01:00\r\n    TITLE \"A B C\"\r\n" +
            "  TRACK 03 AUDIO\r\n    INDEX 01 02:03:38\r\n",
            cue);
    }

    [Fact]
    public void Txt_and_csv_join_lines_and_escape_quotes()
    {
        var doc = Doc((1000, 2500, "diz \"oi\"\r\nde novo"), (3_600_000, 3_601_001, "x"));

        Assert.Equal("diz \"oi\" de novo\r\nx\r\n", ExportFormats.FormatTxt(doc));
        Assert.Equal(
            "Index,Start,Stop,Text\r\n1,00:00:01.000,00:00:02.500,\"diz \"\"oi\"\" de novo\"\r\n2,01:00:00.000,01:00:01.001,\"x\"\r\n",
            ExportFormats.FormatCsv(doc));
    }

    [Theory]
    [InlineData("0:00:01.50", 1500)]
    [InlineData("01:02:03.04", 3_723_040)]
    [InlineData("0:00:01", -1)]
    [InlineData("0-00:01.00", -1)]
    public void Ssa_time_parsing(string text, int expected) => Assert.Equal(expected, TimeFormat.ParseSsa(text));

    [Fact]
    public void Utf8_documents_get_bom_and_ansi_documents_use_code_page()
    {
        var doc = Doc((0, 1000, "Ação"));
        string utf8Path = Path.Combine(_dir, "a.srt"), ansiPath = Path.Combine(_dir, "b.srt");

        SubtitleFile.Save(doc, utf8Path, new SubtitleWriteOptions { Utf8 = true });
        SubtitleFile.Save(doc, ansiPath, new SubtitleWriteOptions { Utf8 = false });

        Assert.Equal([0xEF, 0xBB, 0xBF], File.ReadAllBytes(utf8Path)[..3]);
        Assert.Equal(TextFileReader.Ansi.GetBytes(SrtFormat.Format(doc)), File.ReadAllBytes(ansiPath));
        Assert.True(SubtitleFile.Load(utf8Path).IsUtf8);
        var ansi = SubtitleFile.Load(ansiPath);
        Assert.False(ansi.IsUtf8);
        Assert.Equal("Ação", ansi.Items[0].Text);
    }

    [Fact]
    public void Ascii_without_bom_stays_ansi_and_cue_is_always_ansi()
    {
        string path = Path.Combine(_dir, "c.srt");
        File.WriteAllText(path, "1\r\n00:00:01,000 --> 00:00:02,000\r\nabc\r\n", new UTF8Encoding(false));
        var doc = SubtitleFile.Load(path);
        Assert.False(doc.IsUtf8);

        doc.IsUtf8 = true;
        string cue = Path.Combine(_dir, "c.cue");
        SubtitleFile.Save(doc, cue);
        Assert.Equal((byte)'F', File.ReadAllBytes(cue)[0]);
    }

    [Fact]
    public void Ansi_document_with_unrepresentable_text_is_promoted_to_utf8()
    {
        var doc = Doc((0, 1000, "Olá"));
        Assert.False(SubtitleFile.PromoteToUtf8IfNeeded(doc));

        doc.Items.Add(new Subtitle { Text = "日本語" });
        Assert.True(SubtitleFile.PromoteToUtf8IfNeeded(doc));
        Assert.True(doc.IsUtf8);
    }

    [Fact]
    public void Ass_load_adds_default_style_when_file_has_none_and_backup_copies_file()
    {
        string path = Path.Combine(_dir, "d.ass");
        File.WriteAllText(path, "[Events]\r\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\n");

        var doc = SubtitleFile.Load(path);
        Assert.Equal("Default", Assert.Single(doc.Styles).Name);

        string? backup = SubtitleFile.Backup(path, Path.Combine(_dir, "Backup"));
        Assert.Equal(Path.Combine(_dir, "Backup", "d.bak"), backup);
        Assert.True(File.Exists(backup));
        Assert.Null(SubtitleFile.Backup(Path.Combine(_dir, "nao-existe.srt"), _dir));
    }
}
