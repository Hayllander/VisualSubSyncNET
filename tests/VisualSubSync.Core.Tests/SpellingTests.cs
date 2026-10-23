using System.Text;
using VisualSubSync.Core.Spelling;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Tests;

public sealed class SpellingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"vss-dict-{Guid.NewGuid():N}");

    public SpellingTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "en_test.aff"), "SET UTF-8\nTRY esianrtolcdugmphbyfvkwz'\nSFX S Y 1\nSFX S 0 s .\n");
        File.WriteAllText(Path.Combine(_dir, "en_test.dic"), "5\nhello\nworld/S\nthe\ncat/S\ndon't\n");
        File.WriteAllText(Path.Combine(_dir, "orphan.dic"), "1\nx\n");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private HunspellChecker Loaded()
    {
        var checker = new HunspellChecker();
        Assert.Equal(1, checker.FindDict(_dir));
        Assert.True(checker.Initialize(0));
        return checker;
    }

    [Fact]
    public void FindDict_lists_dic_files_with_aff_and_loads_them()
    {
        var checker = Loaded();
        Assert.Equal(["en_test.dic"], checker.Dictionaries);
        Assert.Equal(0, checker.GetDictIdx("EN_TEST.DIC"));
        Assert.Equal("en_test.dic", checker.CurrentDictName);
        Assert.True(checker.Spell("worlds"));
        Assert.True(checker.Spell("Hello"));
        Assert.True(checker.Spell("don’t"));
        Assert.False(checker.Spell("helo"));
        Assert.Contains("hello", checker.Suggest("helo"));
        checker.Cleanup();
        Assert.False(checker.IsInitialized);
        Assert.Equal(string.Empty, checker.CurrentDictName);
    }

    [Fact]
    public void Ignore_is_case_insensitive_and_add_goes_to_personal_dict()
    {
        var checker = Loaded();
        checker.Ignore("Foo");
        Assert.True(checker.Spell("foo"));
        checker.Add("bar");
        Assert.True(checker.Spell("bar"));

        string perso = Path.Combine(_dir, "perso.dic");
        checker.SavePersonalDict(perso);
        byte[] bytes = File.ReadAllBytes(perso);
        Assert.Equal([0xFF, 0xFE], bytes[..2]);
        Assert.Equal("bar\r\n", Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));

        var other = new HunspellChecker();
        other.LoadPersonalDict(perso);
        Assert.Equal(["bar"], other.PersonalWords);
        other.FindDict(_dir);
        other.Initialize(0);
        Assert.True(other.Spell("bar"));
        Assert.False(other.Spell("foo"));
    }

    [Fact]
    public void Words_follow_the_original_delimiters()
    {
        string text = "Hello, wrld!\r\nThe cat’s 2nd ABC-xyz";
        var words = new List<string>();
        int index = 0;
        while (index < text.Length)
        {
            var w = SpellWords.GetNextWordPos(ref index, text);
            if (w.Length > 0)
            {
                words.Add(text.Substring(w.Position, w.Length));
            }

            index++;
        }

        Assert.Equal(["Hello", "wrld", "The", "cat’s", "2nd", "ABC", "xyz"], words);
        Assert.Equal(new WordInfo(7, 4), SpellWords.GetWordAt(9, text));
        Assert.Equal(new WordInfo(7, 4), SpellWords.GetWordAt(7, text));
        Assert.Equal(new WordInfo(0, 5), SpellWords.GetWordAt(5, text));
        Assert.True(SpellWords.IsUpperCase(string.Empty));
        Assert.False(SpellWords.IsUpperCase("L'ABC"));
        Assert.True(SpellWords.IsWordDelimiter('¿'));
        Assert.False(SpellWords.IsWordDelimiter('’'));
    }

    [Fact]
    public void Misspelled_skips_digits_and_upper_case()
    {
        var checker = Loaded();
        var bad = SpellWords.Misspelled("the wrld 2wrld WRLD helo", checker.Spell).ToList();
        Assert.Equal([new WordInfo(4, 4), new WordInfo(20, 4)], bad);
    }

    [Fact]
    public void Session_walks_errors_outside_tags_and_records_changes()
    {
        var checker = Loaded();
        var subs = new List<Subtitle>
        {
            new() { StartMs = 1000, StopMs = 2000, Text = "<i>helo</i> world" },
            new() { StartMs = 3000, StopMs = 4000, Text = "the {\\k20}wrld helo" },
            new() { StartMs = 5000, StopMs = 6000, Text = "helo catz" },
        };
        var session = new SpellCheckSession(subs, [], checker);
        session.NextError();
        Assert.Equal(0, session.SubtitleIndex);
        Assert.Equal("helo", session.CurrentWord);
        Assert.Equal(new WordInfo(3, 4), session.Error);
        Assert.Equal("hello", session.ReplaceBy);
        Assert.True(session.CanReplace);
        Assert.Equal("Subtitle 1/3 : 00:00:01.000 -> 00:00:02.000", session.Caption);

        session.ReplaceAll("hello");
        Assert.Equal("<i>hello</i> world", subs[0].Text);
        Assert.Equal(1, session.SubtitleIndex);
        Assert.Equal("wrld", session.CurrentWord);
        Assert.Equal(new WordInfo(10, 4), session.Error);

        session.Replace("world");
        // "helo" na mesma legenda é trocada sozinha pelo Replace all.
        Assert.Equal("the {\\k20}world hello", subs[1].Text);
        Assert.Equal(2, session.SubtitleIndex);
        Assert.Equal("catz", session.CurrentWord);
        Assert.Equal("hello catz", subs[2].Text);

        session.IgnoreOnce();
        Assert.True(session.EndOfCheck);
        Assert.Equal("End of check.", session.Caption);
        Assert.False(session.CanReplace);
        Assert.Equal(4, session.Changes.Count);
        Assert.Equal(new SpellChange(1, "the {\\k20}wrld helo", "the {\\k20}world helo"), session.Changes[1]);
    }

    [Fact]
    public void Session_with_several_selected_checks_only_those()
    {
        var checker = Loaded();
        var subs = new List<Subtitle>
        {
            new() { Text = "helo" },
            new() { Text = "wrld" },
            new() { Text = "catz" },
        };
        var session = new SpellCheckSession(subs, [2, 0], checker);
        session.NextError();
        Assert.Equal(0, session.SubtitleIndex);
        session.Add();
        Assert.Equal(2, session.SubtitleIndex);
        session.IgnoreAll();
        Assert.True(session.EndOfCheck);
        Assert.True(checker.Spell("CATZ".ToLowerInvariant()));
        Assert.True(checker.Spell("helo"));
    }

    [Fact]
    public void TagHighlight_marks_tags_karaoke_tag_and_misspelled_words()
    {
        var checker = Loaded();
        const string text = "{\\k10}the {\\k20}wrld\r\n<i>helo</i> TV";
        var spans = TagHighlighter.Compute(text, 1, checker.Spell);
        Assert.Equal(
            [
                new HighlightSpan(0, 6, HighlightKind.Tag),
                new HighlightSpan(10, 6, HighlightKind.SelectedKaraokeTag),
                new HighlightSpan(16, 4, HighlightKind.Misspelled),
                new HighlightSpan(22, 3, HighlightKind.Tag),
                new HighlightSpan(25, 4, HighlightKind.Misspelled),
                new HighlightSpan(29, 4, HighlightKind.Tag),
            ],
            spans);

        // Sem verificação ao vivo, só as tags.
        Assert.All(TagHighlighter.Compute(text, -1, null), s => Assert.Equal(HighlightKind.Tag, s.Kind));
    }
}
