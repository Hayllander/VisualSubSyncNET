namespace VisualSubSync.Core.Subtitles;

/// <summary>TTagType de main.pas: os estilos rápidos do menu (ActionTextItalic e as "Other styles").</summary>
public enum TextTagType
{
    Italic,
    Bold,
    Underline,
    Color,
    Size,
}

/// <summary>Resultado de TagTextMemo: o novo texto e a seleção que o editor recebe depois.</summary>
public readonly record struct TaggedMemoText(string Text, int SelStart, int SelLength);

/// <summary>
/// Os pares de tags de TMainForm.TagText(TagType), conforme a extensão do arquivo de legendas, e a
/// troca de texto de TagTextMemo.
/// </summary>
public static class TextStyleTags
{
    /// <summary>TagText(TagType): SRT usa tags HTML, ASS/SSA usa override; outras extensões não têm tags.</summary>
    public static (string Start, string Stop)? For(TextTagType tagType, string subtitlesFile)
    {
        string ext = Path.GetExtension(subtitlesFile ?? string.Empty).ToLowerInvariant();
        if (ext == ".srt")
        {
            return tagType switch
            {
                TextTagType.Italic => ("<i>", "</i>"),
                TextTagType.Bold => ("<b>", "</b>"),
                TextTagType.Underline => ("<u>", "</u>"),
                TextTagType.Color => ("<font color=\"#FFFFFF\">", "</font>"),
                _ => ("<font size=\"16\">", "</font>"),
            };
        }

        if (ext is ".ass" or ".ssa")
        {
            return tagType switch
            {
                TextTagType.Italic => ("{\\i1}", "{\\i0}"),
                TextTagType.Bold => ("{\\b1}", "{\\b0}"),
                TextTagType.Underline => ("{\\u1}", "{\\u0}"),
                TextTagType.Color => ("{\\1c&HFFFFFF&}", "{\\1c}"),
                _ => ("{\\fs16}", "{\\fs}"),
            };
        }

        return null;
    }

    /// <summary>
    /// TagTextMemo: com seleção, as tags a envolvem e ela é reselecionada com as tags; sem seleção no
    /// começo, envolvem o texto todo e o cursor fica no início; senão entram juntas no cursor e ele fica
    /// depois da tag de abertura.
    /// </summary>
    public static TaggedMemoText TagMemo(string text, int selStart, int selLength, string startTag, string stopTag)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(startTag);
        ArgumentNullException.ThrowIfNull(stopTag);
        selStart = Math.Clamp(selStart, 0, text.Length);
        selLength = Math.Clamp(selLength, 0, text.Length - selStart);
        if (selLength > 0)
        {
            string newText = text[..selStart] + startTag + text.Substring(selStart, selLength) + stopTag + text[(selStart + selLength)..];
            return new TaggedMemoText(newText, selStart, selLength + startTag.Length + stopTag.Length);
        }

        if (selStart == 0)
        {
            return new TaggedMemoText(startTag + text + stopTag, 0, 0);
        }

        return new TaggedMemoText(text[..selStart] + startTag + stopTag + text[selStart..], selStart + startTag.Length, 0);
    }
}
