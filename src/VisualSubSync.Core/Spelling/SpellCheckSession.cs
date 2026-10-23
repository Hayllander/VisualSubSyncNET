using System.Globalization;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.Core.Spelling;

/// <summary>Uma troca feita pela verificação (TChangeSubData com texto antigo e novo).</summary>
public sealed record SpellChange(int SubtitleIndex, string OldText, string NewText);

/// <summary>
/// TSpellCheckForm sem a interface: percorre as legendas (ou só as selecionadas, quando há mais de uma),
/// parando em cada palavra recusada; Replace, Replace all, Ignore, Ignore all e Add seguem para a
/// próxima. As trocas vão direto para as legendas e ficam em <see cref="Changes"/> para o desfazer.
/// </summary>
public sealed class SpellCheckSession
{
    private readonly IReadOnlyList<Subtitle> _subtitles;
    private readonly int[] _order;
    private readonly HunspellChecker _checker;
    private readonly List<(string Word, string ReplaceBy)> _replaceAllWords = [];
    private readonly List<SpellChange> _changes = [];

    private int _orderPos = -1;
    private int _textToSpellOffset;
    private int _textToSpellWithTagOffset;
    private int _wordIdx;
    private WordInfo _wordInfo;

    public SpellCheckSession(IReadOnlyList<Subtitle> subtitles, IReadOnlyList<int> selectedIndexes, HunspellChecker checker)
    {
        ArgumentNullException.ThrowIfNull(subtitles);
        ArgumentNullException.ThrowIfNull(selectedIndexes);
        ArgumentNullException.ThrowIfNull(checker);
        _subtitles = subtitles;
        _checker = checker;
        _order = selectedIndexes.Count <= 1 ? [.. Enumerable.Range(0, subtitles.Count)] : [.. selectedIndexes.Order()];
    }

    /// <summary>A legenda com o erro mostrado (para FocusNode), ou -1.</summary>
    public int SubtitleIndex => _orderPos >= 0 && _orderPos < _order.Length ? _order[_orderPos] : -1;

    /// <summary>pbSubs.Position (pbSubs.Max é a quantidade de legendas).</summary>
    public int Progress { get; private set; }

    /// <summary>pbSubs.Max: a quantidade de legendas.</summary>
    public int Count => _subtitles.Count;

    /// <summary>lblSub.</summary>
    public string Caption { get; private set; } = "Subtitle 0/0 : 00:00:00.000 -> 00:00:00.000";

    /// <summary>reSubtitleText: o texto da legenda, com a palavra errada em vermelho.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>A palavra em vermelho dentro de <see cref="Text"/>.</summary>
    public WordInfo Error { get; private set; }

    /// <summary>CurrentWord.</summary>
    public string CurrentWord { get; private set; } = string.Empty;

    /// <summary>lbSuggestions.</summary>
    public IReadOnlyList<string> Suggestions { get; private set; } = [];

    /// <summary>edReplaceBy: a primeira sugestão, ou vazio.</summary>
    public string ReplaceBy { get; private set; } = string.Empty;

    /// <summary>EndOfCheck.</summary>
    public bool EndOfCheck { get; private set; }

    /// <summary>
    /// UpdateButton: Replace e Replace all precisam de texto em "Replace by"; o original só recalcula ao
    /// mostrar um erro, não ao digitar.
    /// </summary>
    public bool CanReplace { get; private set; }

    public IReadOnlyList<SpellChange> Changes => _changes;

    /// <summary>NextError: avança até a próxima palavra recusada ou até o fim.</summary>
    public void NextError()
    {
        if (_orderPos == -1)
        {
            _orderPos = 0;
        }

        while (_orderPos < _order.Length)
        {
            var sub = _subtitles[_order[_orderPos]];
            Progress = _order[_orderPos];
            var wordArray = SubtitleTextStats.TagSplit(sub.Text);
            while (_wordIdx < wordArray.Count)
            {
                if (_wordIdx % 2 == 0)
                {
                    string textToSpell = wordArray[_wordIdx];
                    while (_textToSpellOffset < textToSpell.Length)
                    {
                        _wordInfo = SpellWords.GetNextWordPos(ref _textToSpellOffset, textToSpell);
                        CurrentWord = textToSpell.Substring(_wordInfo.Position, _wordInfo.Length);
                        string replaceBy = ReplaceAllValue(CurrentWord);
                        if (replaceBy.Length > 0)
                        {
                            ReplaceWordBy(sub, _wordInfo, replaceBy);
                            wordArray = SubtitleTextStats.TagSplit(sub.Text);
                            textToSpell = wordArray[_wordIdx];
                        }
                        else if (!(SpellWords.ContainDigit(CurrentWord) || SpellWords.IsUpperCase(CurrentWord) || _checker.Spell(CurrentWord)))
                        {
                            ShowError(sub);
                            return;
                        }

                        _textToSpellOffset++;
                    }

                    _textToSpellOffset = 0;
                }

                _textToSpellWithTagOffset += wordArray[_wordIdx].Length;
                _wordIdx++;
            }

            _wordIdx = 0;
            _textToSpellWithTagOffset = 0;
            _orderPos++;
        }

        // End of check.
        Caption = "End of check.";
        Text = string.Empty;
        ReplaceBy = string.Empty;
        Suggestions = [];
        Error = default;
        EndOfCheck = true;
        CanReplace = false;
    }

    /// <summary>bttIgnoreClick.</summary>
    public void IgnoreOnce() => NextError();

    /// <summary>bttIgnoreAllClick.</summary>
    public void IgnoreAll()
    {
        _checker.Ignore(CurrentWord);
        NextError();
    }

    /// <summary>bttAddClick.</summary>
    public void Add()
    {
        _checker.Add(CurrentWord);
        NextError();
    }

    /// <summary>bttReplaceClick.</summary>
    public void Replace(string newText)
    {
        ArgumentNullException.ThrowIfNull(newText);
        ReplaceWordBy(_subtitles[_order[_orderPos]], _wordInfo, newText);
        NextError();
    }

    /// <summary>bttReplaceAllClick: as próximas ocorrências da palavra são trocadas sem parar.</summary>
    public void ReplaceAll(string newText)
    {
        ArgumentNullException.ThrowIfNull(newText);
        _replaceAllWords.Add((CurrentWord, newText));
        ReplaceWordBy(_subtitles[_order[_orderPos]], _wordInfo, newText);
        NextError();
    }

    private void ShowError(Subtitle sub)
    {
        Text = sub.Text;
        Error = new WordInfo(_textToSpellWithTagOffset + _wordInfo.Position, _wordInfo.Length);
        Suggestions = _checker.Suggest(CurrentWord);
        ReplaceBy = Suggestions.Count > 0 ? Suggestions[0] : string.Empty;
        CanReplace = ReplaceBy.Length > 0;
        int index = _order[_orderPos];
        Caption = string.Format(CultureInfo.InvariantCulture, "Subtitle {0}/{1} : {2} -> {3}",
            index + 1, _subtitles.Count, TimeFormat.ToVss(sub.StartMs), TimeFormat.ToVss(sub.StopMs));
    }

    /// <summary>ReplaceWordBy: troca a palavra no texto com tags e corrige a posição de leitura.</summary>
    private void ReplaceWordBy(Subtitle sub, WordInfo word, string newText)
    {
        string oldText = sub.Text;
        int start = _textToSpellWithTagOffset + word.Position;
        sub.Text = oldText[..start] + newText + oldText[(start + word.Length)..];
        _changes.Add(new SpellChange(_order[_orderPos], oldText, sub.Text));
        _textToSpellOffset -= word.Length - newText.Length;
    }

    /// <summary>ReplaceAllWords.Values[CurrentWord]: o primeiro par com esse nome, sem distinguir maiúsculas.</summary>
    private string ReplaceAllValue(string word)
    {
        foreach (var (w, replaceBy) in _replaceAllWords)
        {
            if (StringComparer.CurrentCultureIgnoreCase.Equals(w, word))
            {
                return replaceBy;
            }
        }

        return string.Empty;
    }
}
