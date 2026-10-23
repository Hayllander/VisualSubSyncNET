using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Timing;

/// <summary>Resultado de <see cref="TimingRules.SetStopTime"/>.</summary>
public enum StopTimeResult
{
    /// <summary>Duração nula ou negativa: nada muda e não avança para a próxima legenda.</summary>
    Aborted,

    /// <summary>O fim atual foi mantido (alvo de caracteres por segundo), mas a ação segue.</summary>
    Kept,

    Updated,
}

/// <summary>
/// Regras de tempo do modo Timing (main.pas): SetSubtitleStartTime, SetFocusedSubtitleStopTime,
/// OffsetCurrentSubtitleStartTime/StopTime e o limite das ações de criação.
/// </summary>
public static class TimingRules
{
    /// <summary>ActionStopSub/ActionStopAndStartSub/TntFormKeyUp: só cria legendas com mais de 400 ms.</summary>
    public const int MinimumCreatedDurationMs = 400;

    /// <summary>
    /// SetSubtitleStartTime: o início vai para <paramref name="newStartMs"/>; com espaço mínimo
    /// configurado, a legenda anterior é encurtada (dividindo a diferença) para manter esse espaço.
    /// Com <paramref name="setStopTime"/>, o fim é calculado pelo alvo de caracteres por segundo.
    /// </summary>
    public static void SetStartTime(Subtitle current, Subtitle? previous, int newStartMs, bool setStopTime,
        int blankBetweenMs, int cpsTarget, int minimalDurationMs)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (blankBetweenMs > 0 && previous is not null &&
            (previous.StopMs >= newStartMs || newStartMs - previous.StopMs < blankBetweenMs))
        {
            if (previous.StopMs >= newStartMs)
            {
                previous.StopMs = newStartMs - (blankBetweenMs / 2);
            }
            else
            {
                int missing = blankBetweenMs - (newStartMs - previous.StopMs);
                previous.StopMs -= missing / 2;
            }

            newStartMs = previous.StopMs + blankBetweenMs;
        }

        current.StartMs = newStartMs;
        if (newStartMs > current.StopMs)
        {
            current.StopMs = newStartMs + 100;
        }

        if (setStopTime)
        {
            current.StopMs = newStartMs + Math.Max(DurationForCps(current.Text, cpsTarget), minimalDurationMs);
        }
    }

    /// <summary>
    /// SetFocusedSubtitleStopTime: o fim vai para <paramref name="newStopMs"/>. Tocando, um fim igual
    /// ao início ganha 1 ms (o cursor de reprodução pode travar). Com
    /// <paramref name="keepIfSuperiorToCpsTarget"/>, o fim atual fica quando o novo passaria do alvo de
    /// caracteres por segundo ou não chegaria à duração mínima.
    /// </summary>
    public static StopTimeResult SetStopTime(Subtitle current, int newStopMs, bool isPlaying,
        bool keepIfSuperiorToCpsTarget, int cpsTarget, int minimalDurationMs)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (isPlaying && newStopMs == current.StartMs)
        {
            newStopMs++;
        }

        int duration = newStopMs - current.StartMs;
        if (duration <= 0)
        {
            return StopTimeResult.Aborted;
        }

        if (keepIfSuperiorToCpsTarget)
        {
            int cps = (int)Math.Round(SubtitleTextStats.StripTags(current.Text).Length / (double)duration * 1000.0);
            if (cps > cpsTarget || duration <= minimalDurationMs)
            {
                return StopTimeResult.Kept;
            }
        }

        current.StopMs = newStopMs;
        return StopTimeResult.Updated;
    }

    /// <summary>OffsetCurrentSubtitleStartTime: só desloca se o início continuar antes do fim.</summary>
    public static bool CanOffsetStart(Subtitle current, int offsetMs)
    {
        ArgumentNullException.ThrowIfNull(current);
        return current.StartMs + offsetMs < current.StopMs;
    }

    /// <summary>OffsetCurrentSubtitleStopTime: só desloca se o fim continuar depois do início.</summary>
    public static bool CanOffsetStop(Subtitle current, int offsetMs)
    {
        ArgumentNullException.ThrowIfNull(current);
        return current.StopMs + offsetMs > current.StartMs;
    }

    /// <summary>Round(Length(StripTags(Text)) / SpaceKeyCPSTarget * 1000).</summary>
    private static int DurationForCps(string text, int cpsTarget) => cpsTarget <= 0
        ? 0
        : (int)Math.Round(SubtitleTextStats.StripTags(text).Length / (double)cpsTarget * 1000);
}
