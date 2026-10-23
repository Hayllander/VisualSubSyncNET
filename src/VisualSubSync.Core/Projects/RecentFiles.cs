using System.Text;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Projects;

/// <summary>
/// TMRUList (MRUListUnit.pas): até 8 projetos, o mais recente primeiro, gravados em [MRUList]
/// Recent0..Recent7 como UTF-8 dentro do ini ANSI (UTF8Encode/UTF8Decode).
/// </summary>
public sealed class RecentFiles
{
    public const int Limit = 8;
    public const string Section = "MRUList";

    private readonly List<string> _files = [];

    public IReadOnlyList<string> Files => _files;

    /// <summary>GetMRUFilename.</summary>
    public string MostRecent => _files.Count > 0 ? _files[0] : string.Empty;

    /// <summary>AddFile: um arquivo já presente (sem distinguir maiúsculas) sobe para o topo.</summary>
    public void Add(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        int idx = _files.FindIndex(f => string.Equals(f, fileName, StringComparison.OrdinalIgnoreCase));
        if (idx == -1)
        {
            _files.Insert(0, fileName);
        }
        else
        {
            string existing = _files[idx];
            _files.RemoveAt(idx);
            _files.Insert(0, existing);
        }

        if (_files.Count > Limit)
        {
            _files.RemoveRange(Limit, _files.Count - Limit);
        }
    }

    /// <summary>OnClearItemClick.</summary>
    public void Clear() => _files.Clear();

    /// <summary>OnClearDeadEntriesItemClick: tira os arquivos que não existem mais.</summary>
    public void ClearDeadEntries(Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        _files.RemoveAll(f => !exists(f));
    }

    /// <summary>LoadIni.</summary>
    public void Load(IniDocument ini)
    {
        ArgumentNullException.ThrowIfNull(ini);
        _files.Clear();
        for (int i = 0; i < Limit; i++)
        {
            string s = DelphiText.Trim(Utf8Decode(ini.GetString(Section, "Recent" + i, string.Empty)));
            if (s.Length > 0)
            {
                _files.Add(s);
            }
        }
    }

    /// <summary>SaveIni: as 8 chaves sempre, vazias quando não há arquivo.</summary>
    public void Save(IniDocument ini)
    {
        ArgumentNullException.ThrowIfNull(ini);
        for (int i = 0; i < Limit; i++)
        {
            ini.Set(Section, "Recent" + i, i < _files.Count ? Utf8Encode(_files[i]) : string.Empty);
        }
    }

    /// <summary>UTF8Encode: os bytes UTF-8 do texto, lidos como caracteres da página ANSI.</summary>
    public static string Utf8Encode(string s) => TextFileReader.Ansi.GetString(Encoding.UTF8.GetBytes(s));

    /// <summary>UTF8Decode: o inverso; um texto que não é UTF-8 válido fica vazio, como no Delphi 7.</summary>
    public static string Utf8Decode(string s)
    {
        byte[] bytes = TextFileReader.Ansi.GetBytes(s);
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return string.Empty;
        }
    }
}
