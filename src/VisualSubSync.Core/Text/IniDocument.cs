using System.Text;

namespace VisualSubSync.Core.Text;

/// <summary>
/// INI em memória que preserva ordem, seções e chaves desconhecidas, para que um arquivo
/// do legado (.vssprj, VisualSubSync.ini) possa ser regravado sem perder informação.
/// Seções e chaves são comparadas sem diferenciar maiúsculas, como a API do Windows.
/// </summary>
public sealed class IniDocument
{
    private readonly List<Section> _sections = [];

    public static IniDocument Parse(string text)
    {
        var doc = new IniDocument();
        Section? current = null;
        foreach (string raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == ';')
            {
                continue;
            }

            if (line[0] == '[' && line[^1] == ']')
            {
                current = doc.GetOrAddSection(line[1..^1].Trim());
                continue;
            }

            int eq = line.IndexOf('=', StringComparison.Ordinal);
            if (current is not null && eq > 0)
            {
                current.Set(line[..eq].Trim(), line[(eq + 1)..]);
            }
        }

        return doc;
    }

    public static IniDocument Load(string path) => Parse(TextFileReader.ReadAllText(path).Text);

    /// <summary>Leitura como o TIniFile: BOM quando houver, senão ANSI (sem a detecção de UTF-8).</summary>
    public static IniDocument LoadAnsi(string path) => Parse(TextFileReader.ReadAllText(path, detectUtf8: false).Text);

    public string? Get(string section, string key) => FindSection(section)?.Get(key);

    public void Set(string section, string key, string value) => GetOrAddSection(section).Set(key, value);

    public string GetString(string section, string key, string defaultValue) => Get(section, key) ?? defaultValue;

    public int GetInt(string section, string key, int defaultValue) =>
        int.TryParse(Get(section, key), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int v) ? v : defaultValue;

    /// <summary>Como TCustomIniFile.ReadBool: qualquer inteiro diferente de zero é verdadeiro.</summary>
    public bool GetBool(string section, string key, bool defaultValue) => GetInt(section, key, defaultValue ? 1 : 0) != 0;

    public void SetInt(string section, string key, int value) =>
        Set(section, key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public void SetBool(string section, string key, bool value) => Set(section, key, value ? "1" : "0");

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var section in _sections)
        {
            sb.Append('[').Append(section.Name).Append("]\r\n");
            foreach (var (key, value) in section.Entries)
            {
                sb.Append(key).Append('=').Append(value).Append("\r\n");
            }

            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>Grava em UTF-16 LE com BOM, a forma usada por TTntIniFile.</summary>
    public void Save(string path) => Save(path, new UnicodeEncoding(false, true));

    /// <summary>Grava na codificação dada (TIniFile usa a página ANSI do sistema).</summary>
    public void Save(string path, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        byte[] body = encoding.GetBytes(ToString());
        byte[] preamble = encoding.GetPreamble();
        byte[] bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        AtomicFile.WriteAllBytes(path, bytes);
    }

    private Section? FindSection(string name) =>
        _sections.Find(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    private Section GetOrAddSection(string name)
    {
        var section = FindSection(name);
        if (section is null)
        {
            section = new Section(name);
            _sections.Add(section);
        }

        return section;
    }

    private sealed class Section(string name)
    {
        public string Name { get; } = name;

        public List<(string Key, string Value)> Entries { get; } = [];

        public string? Get(string key)
        {
            int i = IndexOf(key);
            return i < 0 ? null : Entries[i].Value;
        }

        public void Set(string key, string value)
        {
            int i = IndexOf(key);
            if (i < 0)
            {
                Entries.Add((key, value));
            }
            else
            {
                Entries[i] = (Entries[i].Key, value);
            }
        }

        private int IndexOf(string key) =>
            Entries.FindIndex(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}
