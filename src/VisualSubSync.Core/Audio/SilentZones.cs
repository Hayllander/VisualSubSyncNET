using System.Globalization;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.Core.Audio;

/// <summary>TSilentRangeInfo: um trecho silencioso e a soma das médias RMS das janelas que o formam.</summary>
public sealed record SilentZone(int StartMs, int StopMs, double RmsSum, int RmsCount)
{
    /// <summary>Linha do lbSilentZones (TSilentZoneForm.FillZone): "início -> fim (RMS médio)".</summary>
    public string Format(CultureInfo culture) => string.Format(culture, "{0} -> {1} ({2:F1})",
        TimeFormat.ToVss(StartMs), TimeFormat.ToVss(StopMs), RmsSum / RmsCount);
}

/// <summary>TWAVDisplayer.DetectSilentZone: janelas seguidas com RMS abaixo do limiar formam uma zona.</summary>
public static class SilentZones
{
    /// <summary>Tamanho da janela usado por TSilentZoneForm.UpdateData.</summary>
    public const int WindowSizeMs = 100;

    /// <remarks>
    /// Como no original, a soma dos quadrados de cada janela é dividida pelo número de picos menos um.
    /// </remarks>
    public static List<SilentZone> Detect(PeakData peaks, int threshold, int windowSizeMs = WindowSizeMs)
    {
        ArgumentNullException.ThrowIfNull(peaks);
        var zones = new List<SilentZone>();
        double sampleLen = 1.0 / peaks.SamplesPerSecond;
        double peakLen = sampleLen * peaks.SamplesPerPeak;
        int winPeaks = (int)Math.Ceiling(windowSizeMs / 1000.0 / peakLen);
        int j = 0;
        double sum = 0;
        SilentZone? current = null;
        for (int i = 0; i < peaks.Peaks.Length; i++)
        {
            int max = peaks.Peaks[i].Max;
            sum += max * max;
            if (j == winPeaks - 1)
            {
                double rms = Math.Sqrt(sum / j);
                if (rms < threshold)
                {
                    int start = (int)Math.Round((i - j) * peakLen * 1000.0, MidpointRounding.ToEven);
                    int stop = (int)Math.Round(i * peakLen * 1000.0, MidpointRounding.ToEven);
                    if (current is not null)
                    {
                        current = current with { StopMs = stop, RmsSum = current.RmsSum + rms, RmsCount = current.RmsCount + 1 };
                        zones[^1] = current;
                    }
                    else
                    {
                        current = new SilentZone(start, stop, rms, 1);
                        zones.Add(current);
                    }
                }
                else
                {
                    current = null;
                }

                j = 0;
                sum = 0;
            }

            j++;
        }

        return zones;
    }
}
