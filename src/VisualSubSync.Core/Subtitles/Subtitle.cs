namespace VisualSubSync.Core.Subtitles;

/// <summary>
/// Uma legenda. Corresponde a TSubtitleRange (SubStructUnit.pas); os valores iniciais
/// são os de TSubtitleRangeFactory.CreateRange e vão para o arquivo ao salvar em SSA/ASS.
/// </summary>
public sealed class Subtitle
{
    public int StartMs { get; set; }

    public int StopMs { get; set; }

    /// <summary>Texto com quebras de linha CRLF, como no legado.</summary>
    public string Text { get; set; } = string.Empty;

    public string Style { get; set; } = "Default";

    /// <summary>Campo Layer do ASS.</summary>
    public string Layer { get; set; } = "0";

    /// <summary>Campo Marked do SSA (o legado grava "Marked=0" por padrão).</summary>
    public string Marked { get; set; } = "Marked=0";

    /// <summary>Campo Name do SSA/ASS.</summary>
    public string Actor { get; set; } = string.Empty;

    public string Effect { get; set; } = string.Empty;

    public string MarginL { get; set; } = "0000";

    public string MarginR { get; set; } = "0000";

    public string MarginV { get; set; } = "0000";

    /// <summary>TRange.SubTime: marcadores de karaokê em tempo absoluto, em ordem (vêm dos \k do texto).</summary>
    public int[] SubTimes { get; set; } = [];

    public int DurationMs => StopMs - StartMs;

    /// <summary>TSubtitleRange.Assign: cópia de todos os campos (os marcadores também são copiados).</summary>
    public Subtitle Clone()
    {
        var clone = (Subtitle)MemberwiseClone();
        clone.SubTimes = [.. SubTimes];
        return clone;
    }
}
