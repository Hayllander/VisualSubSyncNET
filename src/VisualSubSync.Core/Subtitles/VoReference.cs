using System.Text;
using VisualSubSync.Core.Subtitles.Formats;

namespace VisualSubSync.Core.Subtitles;

/// <summary>O texto do MemoSubtitleVO: a parte de antes e a de depois ficam em cinza.</summary>
public readonly record struct VoMemoText(string Text, int CurrentStart, int CurrentStop);

/// <summary>
/// As legendas de referência (VO) de main.pas: LoadVOSubs, GetVOText (coluna VO e MemoSubtitleVO) e o
/// texto de UpdateMemoVO.
/// </summary>
public static class VoReference
{
    private const int MemoVOMaxUnassigned = 100;
    private const int MemoVOMaxLength = 40;
    private const int MemoVOMaxRows = 4;

    /// <summary>
    /// LoadVOSubs: SRT pelo TSRTParser, SSA/ASS pelo TSSAParser (Start/End válidos, \N vira quebra);
    /// outra extensão ou arquivo inexistente dá uma lista vazia. A lista fica ordenada (FullSort).
    /// </summary>
    public static List<Subtitle> Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path) || !SubtitleFile.CanRead(path))
        {
            return [];
        }

        var items = SubtitleFile.Load(path).Items;
        return [.. items.Select(s => new Subtitle { StartMs = s.StartMs, StopMs = s.StopMs, Text = s.Text })];
    }

    /// <summary>
    /// GetVOText: as legendas do VO entre o início e o fim da legenda <paramref name="currSub"/>, e as que
    /// ficam entre ela e as vizinhas (até 100 caracteres de cada lado). Na coluna (enhanced falso) vira
    /// uma linha com "[&lt;&lt;&lt;]" e "[&gt;&gt;&gt;]"; para o MemoSubtitleVO (enhanced verdadeiro) as três partes
    /// vêm separadas por '|'.
    /// </summary>
    public static string GetVoText(IReadOnlyList<Subtitle> subs, int currSub, IReadOnlyList<Subtitle> vo, bool enhanced)
    {
        ArgumentNullException.ThrowIfNull(subs);
        ArgumentNullException.ThrowIfNull(vo);
        if (currSub < 0 || currSub >= subs.Count || vo.Count == 0)
        {
            return string.Empty;
        }

        var range = subs[currSub];
        int firstVo = SearchStartVo(vo, range.StartMs);
        int lastVo = SearchEndVo(vo, range.StopMs);
        int prevVo = currSub > 0 ? SearchEndVo(vo, subs[currSub - 1].StopMs) + 1 : 0;
        int nextVo = currSub < subs.Count - 1 ? SearchStartVo(vo, subs[currSub + 1].StartMs) - 1 : vo.Count - 1;

        var text = new StringBuilder();
        for (int i = firstVo; i <= lastVo; i++)
        {
            if (i > firstVo)
            {
                text.Append('|');
            }

            text.Append(CrlfToPipe(vo[i].Text.Replace('|', ' ')));
        }

        string prevText = string.Empty;
        for (int i = firstVo - 1; i >= prevVo; i--)
        {
            if (i < firstVo - 1)
            {
                prevText = " " + prevText;
            }

            prevText = vo[i].Text + prevText;
            if (prevText.Length > MemoVOMaxUnassigned)
            {
                break;
            }
        }

        string nextText = string.Empty;
        for (int i = lastVo + 1; i <= nextVo; i++)
        {
            if (i > lastVo + 1)
            {
                nextText += " ";
            }

            nextText += vo[i].Text;
            if (nextText.Length > MemoVOMaxUnassigned)
            {
                break;
            }
        }

        if (enhanced)
        {
            return ShrinkSpace(prevText) + "|" + text + "|" + ShrinkSpace(nextText);
        }

        string result = text.ToString();
        if (prevText.Length > 0)
        {
            result = "[<<<] " + result;
        }

        if (nextText.Length > 0)
        {
            result += " [>>>]";
        }

        return ShrinkSpace(result);
    }

    /// <summary>
    /// UpdateMemoVO: a parte de antes (com "[&lt;&lt;&lt;]") e a de depois (com "[&gt;&gt;&gt;]") encurtadas para 40
    /// caracteres, e a atual quebrada para caber em 4 linhas no total.
    /// </summary>
    public static VoMemoText MemoText(string enhText)
    {
        ArgumentNullException.ThrowIfNull(enhText);
        int textFirst = enhText.IndexOf('|', StringComparison.Ordinal);
        int textLast = enhText.LastIndexOf('|');
        if (textFirst < 0 || textLast <= textFirst)
        {
            return new VoMemoText(enhText, 0, enhText.Length);
        }

        string prevText = enhText[..textFirst];
        if (prevText.Length > 0)
        {
            prevText += " [<<<]";
        }

        string currText = enhText[(textFirst + 1)..textLast];
        string nextText = enhText[(textLast + 1)..];
        if (nextText.Length > 0)
        {
            nextText = "[>>>] " + nextText;
        }

        if (prevText.Length > MemoVOMaxLength)
        {
            prevText = "[...]" + prevText[^(MemoVOMaxLength - 5)..];
        }

        if (nextText.Length > MemoVOMaxLength)
        {
            nextText = nextText[..(MemoVOMaxLength - 5)] + "[...]";
        }

        int countRows = 1 + currText.Count(c => c == '|');
        int rowAvail = MemoVOMaxRows;
        if (prevText.Length > 0)
        {
            rowAvail--;
        }

        if (nextText.Length > 0)
        {
            rowAvail--;
        }

        if (countRows > rowAvail)
        {
            // Junta tudo numa linha e troca espaços por quebras em pontos proporcionais.
            var chars = ShrinkSpace(currText).ToCharArray();
            int startCount = 0; // posição base 1 do último corte, como em PosEx
            while (rowAvail > 1)
            {
                int offset = startCount + 1 + ((chars.Length - startCount - 1) / rowAvail);
                startCount = PosEx(' ', chars, offset);
                if (startCount <= 0)
                {
                    break;
                }

                chars[startCount - 1] = '|';
                rowAvail--;
            }

            currText = new string(chars);
        }

        currText = currText.Replace("|", "\r\n", StringComparison.Ordinal);
        if (prevText.Length > 0 && (currText.Length > 0 || nextText.Length > 0))
        {
            prevText += "\r\n";
        }

        int first = prevText.Length;
        if (currText.Length > 0 && nextText.Length > 0)
        {
            currText += "\r\n";
        }

        int last = first + currText.Length;
        return new VoMemoText(prevText + currText + nextText, first, last);
    }

    /// <summary>StringConvertShrinkSpace: espaços, quebras e '|' viram um espaço só, sem espaço nas pontas.</summary>
    public static string ShrinkSpace(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var result = new StringBuilder(s.Length);
        bool lastSpace = true, addSpace = false;
        foreach (char c in s)
        {
            if (c is ' ' or '\r' or '\n' or '|')
            {
                if (!lastSpace)
                {
                    addSpace = true;
                }

                lastSpace = true;
            }
            else
            {
                if (addSpace)
                {
                    result.Append(' ');
                }

                addSpace = false;
                result.Append(c);
                lastSpace = false;
            }
        }

        return result.ToString();
    }

    /// <summary>StringConvertCRLFToPipe: cada #13 vira '|' e o caractere seguinte é pulado.</summary>
    private static string CrlfToPipe(string s)
    {
        var result = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\r')
            {
                result.Append('|');
                i++;
            }
            else
            {
                result.Append(s[i]);
            }
        }

        return result.ToString();
    }

    /// <summary>PosEx base 1: 0 quando não acha ou quando o início passa do fim.</summary>
    private static int PosEx(char c, char[] text, int offset)
    {
        if (offset < 1 || offset > text.Length)
        {
            return 0;
        }

        int i = Array.IndexOf(text, c, offset - 1);
        return i < 0 ? 0 : i + 1;
    }

    /// <summary>SearchStartVO: o primeiro VO que termina em <paramref name="timing"/> ou depois (ou Count).</summary>
    private static int SearchStartVo(IReadOnlyList<Subtitle> vo, int timing)
    {
        for (int i = 0; i < vo.Count; i++)
        {
            if (vo[i].StopMs >= timing)
            {
                return i;
            }
        }

        return vo.Count;
    }

    /// <summary>SearchEndVO: o último VO que começa em <paramref name="timing"/> ou antes (ou -1).</summary>
    private static int SearchEndVo(IReadOnlyList<Subtitle> vo, int timing)
    {
        for (int i = vo.Count - 1; i >= 0; i--)
        {
            if (vo[i].StartMs <= timing)
            {
                return i;
            }
        }

        return -1;
    }
}
