using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Projects;

/// <summary>
/// "Clear &amp; Forget" do Enhanced (TVSSProject.SaveSkippedSubOnErrorCheck e SearchSkippedSubOnErrorCheck):
/// textos de legenda (com as quebras como "|") cujos erros não aparecem mais no Error report. Ficam no
/// próprio .vssprj, seção [VisualSubsync], em SkippedSub001, SkippedSub002... e LastSkippedOnErrorCheck.
/// </summary>
public static class SkippedErrors
{
    private const string S = "VisualSubsync";
    private const string LastKey = "LastSkippedOnErrorCheck";

    private static string Key(int index) => "SkippedSub" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>SearchSkippedSubOnErrorCheck, lido uma vez por verificação.</summary>
    public static HashSet<string> Load(string projectPath)
    {
        var skipped = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath))
        {
            return skipped;
        }

        var ini = IniDocument.Load(projectPath);
        int last = ini.GetInt(S, LastKey, 0);
        for (int i = 0; i <= last; i++)
        {
            if (ini.Get(S, Key(i)) is { } id)
            {
                skipped.Add(id);
            }
        }

        return skipped;
    }

    /// <summary>SaveSkippedSubOnErrorCheck: acrescenta os textos que ainda não estão na lista.</summary>
    public static void Add(string projectPath, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath))
        {
            return;
        }

        var existing = Load(projectPath);
        var ini = IniDocument.Load(projectPath);
        int last = ini.GetInt(S, LastKey, 0);
        bool changed = false;
        foreach (string id in ids)
        {
            if (existing.Add(id))
            {
                last++;
                ini.Set(S, Key(last), id);
                ini.SetInt(S, LastKey, last);
                changed = true;
            }
        }

        if (changed)
        {
            ini.Save(projectPath);
        }
    }
}
