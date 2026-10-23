using System.Text;
using VisualSubSync.Core.Text;
using WeCantSpell.Hunspell;

namespace VisualSubSync.Core.Spelling;

/// <summary>
/// THunspellChecker (LibHunspellUnit.pas) sobre o WeCantSpell.Hunspell: os mesmos .aff/.dic, a lista
/// de palavras ignoradas da sessão e o dicionário pessoal (perso.dic).
/// </summary>
public sealed class HunspellChecker
{
    private readonly Lock _lock = new();
    private readonly List<string> _dictList = [];
    private readonly List<string> _ignoreList = [];
    private readonly List<string> _addList = [];
    private string _dictListPath = string.Empty;
    private WordList? _wordList;
    private string _currentDictName = string.Empty;

    /// <summary>Os .dic encontrados por <see cref="FindDict"/> (só os que têm o .aff ao lado).</summary>
    public IReadOnlyList<string> Dictionaries => _dictList;

    /// <summary>As palavras acrescentadas pelo usuário (FAddList), na ordem em que entraram.</summary>
    public IReadOnlyList<string> PersonalWords => _addList;

    /// <summary>GetCurrentDictName: o nome do .dic carregado, ou vazio.</summary>
    public string CurrentDictName
    {
        get
        {
            lock (_lock)
            {
                return _currentDictName;
            }
        }
    }

    public bool IsInitialized
    {
        get
        {
            lock (_lock)
            {
                return _wordList is not null;
            }
        }
    }

    /// <summary>Initialize(DictAff, DictDic): carrega o dicionário e acrescenta as palavras pessoais.</summary>
    public bool Initialize(string affPath, string dicPath)
    {
        ArgumentNullException.ThrowIfNull(dicPath);
        Cleanup();
        WordList wordList;
        try
        {
            wordList = WordList.CreateFromFiles(dicPath, affPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or FormatException or ArgumentException)
        {
            return false;
        }

        lock (_lock)
        {
            _wordList = wordList;
            _currentDictName = Path.GetFileName(dicPath);
            LoadAddListInHunspell();
        }

        return true;
    }

    /// <summary>Initialize(DictIndex): o .dic da lista e o .aff de mesmo nome.</summary>
    public bool Initialize(int dictIndex)
    {
        if (dictIndex < 0 || dictIndex >= _dictList.Count)
        {
            return false;
        }

        string dic = Path.Combine(_dictListPath, _dictList[dictIndex]);
        return Initialize(Path.ChangeExtension(dic, ".aff"), dic);
    }

    public void Cleanup()
    {
        lock (_lock)
        {
            _wordList = null;
            _currentDictName = string.Empty;
        }
    }

    /// <summary>
    /// Spell: o apóstrofo tipográfico vira o reto; palavras ignoradas (sem distinguir maiúsculas) são
    /// aceitas. Sem dicionário, toda palavra é recusada.
    /// </summary>
    public bool Spell(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        word = word.Replace('’', '\'');
        if (IndexOf(_ignoreList, word) != -1)
        {
            return true;
        }

        lock (_lock)
        {
            return _wordList is { } list && (word.Length == 0 || list.Check(word));
        }
    }

    /// <summary>Suggest: as sugestões do Hunspell, na ordem dele.</summary>
    public IReadOnlyList<string> Suggest(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        lock (_lock)
        {
            return _wordList is { } list && word.Length > 0 ? [.. list.Suggest(word)] : [];
        }
    }

    /// <summary>Add: entra no dicionário pessoal e no dicionário carregado.</summary>
    public void Add(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        _addList.Add(word);
        lock (_lock)
        {
            if (_wordList is { } list && word.Length > 0)
            {
                list.Add(word);
            }
        }
    }

    /// <summary>FindDict: os *.dic da pasta que têm um .aff de mesmo nome, na ordem do FindFirst do NTFS.</summary>
    public int FindDict(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _dictList.Clear();
        _dictListPath = path;
        if (Directory.Exists(path))
        {
            _dictList.AddRange(Directory.EnumerateFiles(path, "*.dic")
                .Where(f => File.Exists(Path.ChangeExtension(f, ".aff")))
                .Select(Path.GetFileName)
                .OfType<string>()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
        }

        return _dictList.Count;
    }

    /// <summary>GetDictIdx: TStrings.IndexOf, sem distinguir maiúsculas.</summary>
    public int GetDictIdx(string name) => IndexOf(_dictList, name);

    /// <summary>Ignore: vale até o programa fechar.</summary>
    public void Ignore(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        _ignoreList.Add(word);
    }

    /// <summary>LoadPersonalDict: TTntStringList.LoadFromFile (BOM ou ANSI), uma palavra por linha.</summary>
    public void LoadPersonalDict(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        if (!File.Exists(fileName))
        {
            return;
        }

        try
        {
            string text = TextFileReader.ReadAllText(fileName).Text;
            _addList.Clear();
            _addList.AddRange(DelphiText.SplitLines(text));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // O original ignora o erro e fica com a lista que tinha.
        }

        lock (_lock)
        {
            LoadAddListInHunspell();
        }
    }

    /// <summary>SavePersonalDict: TTntStringList.SaveToFile grava UTF-16 com BOM e CRLF.</summary>
    public bool SavePersonalDict(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        try
        {
            var text = new StringBuilder();
            foreach (string word in _addList)
            {
                text.Append(word).Append("\r\n");
            }

            var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
            AtomicFile.WriteAllBytes(fileName, [.. encoding.GetPreamble(), .. encoding.GetBytes(text.ToString())]);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void LoadAddListInHunspell()
    {
        if (_wordList is not { } list)
        {
            return;
        }

        foreach (string word in _addList.Where(w => w.Length > 0))
        {
            list.Add(word);
        }
    }

    private static int IndexOf(List<string> list, string value) =>
        list.FindIndex(s => StringComparer.CurrentCultureIgnoreCase.Equals(s, value));
}
