using System.Globalization;
using System.Text;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Subtitles;

/// <summary>
/// Karaokê (EnableExperimentalKaraoke do 1.0.1): os marcadores SubTime de TRange, KaraSplit e
/// KaraSplit2 (WAVDisplayerUnit.pas) e CreateKaraoke, ClearKaraoke, CalculateNewTextFromSubTime e
/// ActionPlaceKaraokeCursorsAtEnd (main.pas). Os tempos \k são centésimos de segundo.
/// </summary>
public static class Karaoke
{
    /// <summary>CompressedDuration de ActionPlaceKaraokeCursorsAtEnd.</summary>
    public const int CompressedDurationMs = 100;

    /// <summary>KaraSplit: os pedaços de texto sem as tags {\k..} e os tempos de cada tag.</summary>
    public static (List<string> Words, List<int> Times) Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = new List<string>();
        var times = new List<int>();
        string s = text;
        while (s.Length > 0)
        {
            int i = FindTag(s);
            if (i > 0)
            {
                words.Add(s[..(i - 1)]);
                s = s[(i - 1)..];
                int close = s.IndexOf('}', StringComparison.Ordinal) + 1;
                if (close > 0)
                {
                    times.Add(DelphiText.StrToIntDef(Copy(s, 4, close - 4), 0));
                    s = s[close..];
                }
                else
                {
                    words[^1] += s;
                    s = string.Empty;
                }
            }
            else
            {
                words.Add(s);
                s = string.Empty;
            }
        }

        return (words, times);
    }

    /// <summary>KaraSplit2: como <see cref="Split"/>, mas cada pedaço guarda as tags, sem os números.</summary>
    public static (List<string> Words, List<int> Times) SplitKeepingTags(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = new List<string>();
        var times = new List<int>();
        string s = text;
        while (s.Length > 0)
        {
            int i = FindTag(s);
            if (i > 0)
            {
                int cut = i + 3 - 1; // i + Length('{\k') - 1
                words.Add(s[..cut]);
                s = s[cut..];
                int close = s.IndexOf('}', StringComparison.Ordinal) + 1;
                if (close > 0)
                {
                    times.Add(DelphiText.StrToIntDef(s[..(close - 1)], 0));
                    s = s[(close - 1)..];
                }
                else
                {
                    words[^1] += s;
                    s = string.Empty;
                }
            }
            else
            {
                words.Add(s);
                s = string.Empty;
            }
        }

        return (words, times);
    }

    /// <summary>
    /// TRange.UpdateSubTimeFromText: os marcadores passam a ser os tempos acumulados dos \k (o
    /// último é descartado). Verdadeiro quando os marcadores mudaram (ou o texto não tem \k).
    /// </summary>
    public static bool UpdateSubTimesFromText(Subtitle subtitle)
    {
        ArgumentNullException.ThrowIfNull(subtitle);
        var (_, times) = Split(subtitle.Text);
        if (times.Count == 0)
        {
            subtitle.SubTimes = [];
            return true;
        }

        int[] absolute = new int[times.Count - 1];
        int accu = subtitle.StartMs;
        for (int i = 0; i < absolute.Length; i++)
        {
            accu += times[i] * 10;
            absolute[i] = accu;
        }

        if (absolute.AsSpan().SequenceEqual(subtitle.SubTimes))
        {
            return false;
        }

        subtitle.SubTimes = absolute;
        return true;
    }

    /// <summary>
    /// CreateKaraoke: divide o texto nos espaços, quebras de linha e '/' (que separa sílabas e some)
    /// e dá a cada pedaço um \k proporcional ao número de letras. Sem letras, nada muda.
    /// </summary>
    public static void Create(Subtitle subtitle)
    {
        ArgumentNullException.ThrowIfNull(subtitle);
        int letters = KaraLen(subtitle.Text.Replace("/", string.Empty, StringComparison.Ordinal));
        if (letters == 0)
        {
            return;
        }

        double timePerLetter = (subtitle.StopMs - subtitle.StartMs) / (double)letters;
        var words = WhiteSpaceSplit(subtitle.Text);
        var subTimes = new List<int>();
        var sb = new StringBuilder();
        int total = 0;
        for (int i = 0; i < words.Count; i++)
        {
            int j = (int)Math.Round(timePerLetter * KaraLen(words[i]) / 10);
            total += j;
            if (i < words.Count - 1)
            {
                AddSubTime(subTimes, subtitle.StartMs + (total * 10));
            }

            sb.Append(CultureInfo.InvariantCulture, $"{{\\k{j}}}{words[i]}");
        }

        subtitle.SubTimes = [.. subTimes];
        subtitle.Text = sb.ToString();
    }

    /// <summary>ClearKaraoke: tira as tags \k do texto e apaga os marcadores.</summary>
    public static void Clear(Subtitle subtitle)
    {
        ArgumentNullException.ThrowIfNull(subtitle);
        subtitle.Text = string.Concat(Split(subtitle.Text).Words);
        subtitle.SubTimes = [];
    }

    /// <summary>
    /// CalculateNewTextFromSubTime: reescreve os \k a partir dos marcadores (o último vai até o fim
    /// da legenda). Sem marcadores, ou com número diferente de tags, o texto fica como está.
    /// </summary>
    public static string TextFromSubTimes(Subtitle subtitle)
    {
        ArgumentNullException.ThrowIfNull(subtitle);
        int[] subTimes = subtitle.SubTimes;
        if (subTimes.Length == 0)
        {
            return subtitle.Text;
        }

        var (words, times) = SplitKeepingTags(subtitle.Text);
        if (subTimes.Length != times.Count - 1 || words.Count < times.Count + 1)
        {
            return subtitle.Text;
        }

        int accu = subtitle.StartMs;
        for (int i = 0; i < subTimes.Length; i++)
        {
            times[i] = (int)Math.Round((subTimes[i] - accu) / 10.0);
            accu += times[i] * 10;
        }

        times[^1] = (int)Math.Round((subtitle.StopMs - accu) / 10.0);
        var sb = new StringBuilder(words[0]);
        for (int i = 0; i < times.Count; i++)
        {
            sb.Append(times[i].ToString(CultureInfo.InvariantCulture)).Append(words[i + 1]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// ActionPlaceKaraokeCursorsAtEnd: junta os marcadores no fim da legenda, 100 ms cada; não faz nada
    /// se não couberem. Devolve os marcadores e o texto novos, ou null.
    /// </summary>
    public static (int[] SubTimes, string Text)? PlaceCursorsAtEnd(Subtitle subtitle)
    {
        ArgumentNullException.ThrowIfNull(subtitle);
        int compressedStart = subtitle.StopMs - (subtitle.SubTimes.Length * CompressedDurationMs);
        if (compressedStart <= subtitle.StartMs)
        {
            return null;
        }

        var copy = subtitle.Clone();
        copy.SubTimes = [.. Enumerable.Range(0, subtitle.SubTimes.Length).Select(i => compressedStart + (CompressedDurationMs * i))];
        return (copy.SubTimes, TextFromSubTimes(copy));
    }

    /// <summary>TRange.AddSubTime: insere em ordem, depois dos tempos iguais.</summary>
    public static void AddSubTime(List<int> subTimes, int time)
    {
        ArgumentNullException.ThrowIfNull(subTimes);
        int index = 0;
        while (index < subTimes.Count && subTimes[index] <= time)
        {
            index++;
        }

        subTimes.Insert(index, time);
    }

    /// <summary>
    /// TRange.GetSubTimeRange: o trecho de karaokê que contém o tempo (0 = do início ao primeiro
    /// marcador; n = do último ao fim) e os seus limites; -1 fora da legenda ou sem marcadores.
    /// </summary>
    public static int GetSubTimeRange(int startMs, int stopMs, IReadOnlyList<int> subTimes, int posMs, out int kStart, out int kStop)
    {
        ArgumentNullException.ThrowIfNull(subTimes);
        kStart = kStop = -1;
        if (subTimes.Count == 0 || posMs < startMs || posMs > stopMs)
        {
            return -1;
        }

        for (int i = 0; i < subTimes.Count; i++)
        {
            if (posMs < subTimes[i])
            {
                kStart = i > 0 ? subTimes[i - 1] : startMs;
                kStop = subTimes[i];
                return i;
            }
        }

        kStart = subTimes[^1];
        kStop = stopMs;
        return subTimes.Count;
    }

    /// <summary>TRange.GetSubTimeRangeAt: os limites do trecho de karaokê de índice dado.</summary>
    public static bool GetSubTimeRangeAt(int startMs, int stopMs, IReadOnlyList<int> subTimes, int index, out int kStart, out int kStop)
    {
        ArgumentNullException.ThrowIfNull(subTimes);
        kStart = kStop = -1;
        if (subTimes.Count == 0 || index < 0 || index > subTimes.Count)
        {
            return false;
        }

        kStart = index == 0 ? startMs : subTimes[index - 1];
        kStop = index == subTimes.Count ? stopMs : subTimes[index];
        return true;
    }

    /// <summary>Posição (1 = início) da primeira tag "{\k" ou "{\K", ou 0.</summary>
    private static int FindTag(string s)
    {
        int i1 = s.IndexOf("{\\k", StringComparison.Ordinal) + 1;
        int i2 = s.IndexOf("{\\K", StringComparison.Ordinal) + 1;
        return i1 == 0 || i2 == 0 ? i1 + i2 : Math.Min(i1, i2);
    }

    /// <summary>Copy do Delphi (índice 1).</summary>
    private static string Copy(string s, int index, int count)
    {
        int start = Math.Clamp(index - 1, 0, s.Length);
        return s.Substring(start, Math.Clamp(count, 0, s.Length - start));
    }

    /// <summary>WhiteSpaceSplit: o separador fica no fim do pedaço; o '/' é retirado.</summary>
    private static List<string> WhiteSpaceSplit(string text)
    {
        var words = new List<string>();
        int j = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is ' ' or '\n' or '/')
            {
                words.Add(text[j..(i + 1)].Replace("/", string.Empty, StringComparison.Ordinal));
                j = i + 1;
            }
        }

        if (text.Length > 0)
        {
            words.Add(text[j..]);
        }

        return words;
    }

    /// <summary>KaraLen: o número de caracteres fora as quebras de linha.</summary>
    private static int KaraLen(string text) => text.Count(c => c is not '\r' and not '\n');
}
