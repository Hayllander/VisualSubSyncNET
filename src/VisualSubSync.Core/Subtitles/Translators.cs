using System.Text;

namespace VisualSubSync.Core.Subtitles;

/// <summary>TTranslateTextType: o texto das legendas novas na tradução.</summary>
public enum TranslateTextType
{
    /// <summary>tttEmpty.</summary>
    Empty,

    /// <summary>tttCopyOriginal.</summary>
    CopyOriginal,

    /// <summary>tttCopyTagsOnly.</summary>
    CopyTagsOnly,

    /// <summary>tttCustomText.</summary>
    CustomText,
}

/// <summary>Os TTranslator de TranslateFormUnit.pas.</summary>
public static class Translators
{
    /// <summary>TTranslateForm.GetTranslator.</summary>
    public static Func<string, string> Create(TranslateTextType type, string customText) => type switch
    {
        TranslateTextType.CopyOriginal => static text => text,
        TranslateTextType.CopyTagsOnly => StripText,
        TranslateTextType.CustomText => _ => customText,
        _ => static _ => string.Empty,
    };

    /// <summary>StripText (MiscToolsUnit): só as tags do texto, na ordem.</summary>
    public static string StripText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = SubtitleTextStats.TagSplit(text);
        var result = new StringBuilder();
        for (int i = 1; i < parts.Count; i += 2)
        {
            result.Append(parts[i]);
        }

        return result.ToString();
    }

    /// <summary>ActionTranslationTemplateExecute: "nome.translated.ext" ao lado das legendas.</summary>
    public static string TranslatedFileName(string subtitlesFile)
    {
        ArgumentNullException.ThrowIfNull(subtitlesFile);
        string ext = Path.GetExtension(subtitlesFile);
        return subtitlesFile[..^ext.Length] + ".translated" + ext;
    }

    /// <summary>
    /// tstMissingOnly: uma legenda nova para cada VO cujo meio não cai em nenhuma legenda, com os
    /// tempos do VO e o texto do tradutor.
    /// </summary>
    public static List<Subtitle> MissingFromVo(IReadOnlyList<Subtitle> vo, Func<int, bool> hasSubtitleAt, Func<string, string> translate)
    {
        ArgumentNullException.ThrowIfNull(vo);
        ArgumentNullException.ThrowIfNull(hasSubtitleAt);
        ArgumentNullException.ThrowIfNull(translate);
        var result = new List<Subtitle>();
        foreach (var s in vo)
        {
            // TRange.GetMiddle.
            int middle = s.StartMs + ((s.StopMs - s.StartMs) / 2);
            if (!hasSubtitleAt(middle))
            {
                result.Add(new Subtitle { StartMs = s.StartMs, StopMs = s.StopMs, Text = translate(s.Text) });
            }
        }

        return result;
    }
}
