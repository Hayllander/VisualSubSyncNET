using System.Text;
using VisualSubSync.Core.Text;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.Core.Scenes;

/// <summary>
/// Arquivo .scenechange (SceneChangeUnit.pas), gravado ao lado do vídeo:
/// primeira linha "SceneChangeFormatVersion=1", depois um tempo hh:mm:ss.mmm por linha.
/// </summary>
public static class SceneChangeFile
{
    public const string Extension = ".scenechange";
    private const string Header = "SceneChangeFormatVersion";

    public static int[] Load(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        return Parse(TextFileReader.ReadAllText(path).Text);
    }

    public static int[] Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != Header + "=1")
        {
            return []; // versão desconhecida: ignorada, como no legado
        }

        var result = new List<int>(lines.Length);
        foreach (string line in lines.Skip(1))
        {
            int ms = TimeFormat.ParseVss(line.Trim());
            if (ms != -1)
            {
                result.Add(ms);
            }
        }

        return [.. result];
    }

    public static void Save(string path, IEnumerable<int> timesMs)
    {
        ArgumentNullException.ThrowIfNull(timesMs);
        var sb = new StringBuilder(Header).Append("=1\r\n");
        foreach (int t in timesMs)
        {
            sb.Append(TimeFormat.ToVss(t)).Append("\r\n");
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
}
