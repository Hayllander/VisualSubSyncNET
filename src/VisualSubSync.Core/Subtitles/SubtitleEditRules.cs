using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Subtitles;

/// <summary>TMergeType (UndoableSubTaskUnit.pas).</summary>
public enum MergeType
{
    /// <summary>Textos um embaixo do outro.</summary>
    Normal,

    /// <summary>Cada texto numa linha começada por "- ".</summary>
    Dialog,

    /// <summary>Textos numa linha só, separados por um espaço.</summary>
    OneLine,
}

/// <summary>Regras de TMainForm.MergeSubtitles e TMainForm.SplitSubtitle (main.pas).</summary>
public static class SubtitleEditRules
{
    /// <summary>
    /// MergeSubtitles: cópia da primeira legenda com o fim mais tardio entre todas e os textos juntados
    /// conforme <paramref name="type"/>.
    /// </summary>
    public static Subtitle Merge(IReadOnlyList<Subtitle> subtitles, MergeType type)
    {
        ArgumentNullException.ThrowIfNull(subtitles);
        ArgumentOutOfRangeException.ThrowIfZero(subtitles.Count);
        var first = subtitles[0];
        string text = type switch
        {
            MergeType.Dialog => "- " + first.Text,
            MergeType.OneLine => DelphiText.TrimRight(first.Text),
            _ => first.Text,
        };
        int lastStop = first.StopMs;
        foreach (var sub in subtitles.Skip(1))
        {
            text = type switch
            {
                MergeType.Dialog => text + "\r\n- " + sub.Text,
                MergeType.OneLine => DelphiText.TrimRight(text) + " " + DelphiText.TrimLeft(sub.Text),
                _ => text + "\r\n" + sub.Text,
            };
            lastStop = Math.Max(lastStop, sub.StopMs);
        }

        var merged = first.Clone();
        merged.StopMs = lastStop;
        merged.Text = DelphiText.Trim(text);
        return merged;
    }

    /// <summary>
    /// SplitSubtitle: o fim da primeira parte e o início da segunda. Sem espaço para o intervalo, a
    /// primeira parte termina 1 ms antes do corte; as duas nunca se sobrepõem.
    /// </summary>
    public static (int FirstStopMs, int SecondStartMs) SplitTimes(int startMs, int stopMs, int splitMs, int blankMs)
    {
        int stop1 = splitMs - blankMs;
        int start2 = splitMs;
        if (stop1 < startMs || start2 > stopMs)
        {
            stop1 = splitMs - 1;
            start2 = splitMs;
        }

        if (stop1 == start2)
        {
            stop1--;
        }

        return (stop1, start2);
    }
}
