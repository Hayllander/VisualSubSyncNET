using VisualSubSync.Core.Audio;

namespace VisualSubSync.Media.FFmpeg;

/// <summary>
/// Acumula picos como TWAVDisplayer.CreatePeakFile / CWavWriterFilter::PeakProcessing:
/// blocos de SamplesPerPeak quadros, mínimo e máximo sobre todas as amostras intercaladas,
/// último bloco completado com zeros, e normalização igual a NormalizePeakTab(-1).
/// </summary>
internal sealed class PeakBuilder(int channels, int samplesPerPeak)
{
    private readonly List<Peak> _peaks = new(capacity: 360_000);
    private readonly int _blockSamples = samplesPerPeak * channels;
    private int _filled;
    private short _max = short.MinValue, _min = short.MaxValue;

    public long TotalSamples { get; private set; }

    public void Add(ReadOnlySpan<short> interleaved)
    {
        TotalSamples += interleaved.Length;
        foreach (short s in interleaved)
        {
            if (s > _max) _max = s;
            if (s < _min) _min = s;
            if (++_filled == _blockSamples)
            {
                Flush();
            }
        }
    }

    public Peak[] Finish()
    {
        if (_filled > 0)
        {
            // O restante do bloco é zero no legado (ZeroMemory antes da leitura).
            if (_max < 0) _max = 0;
            if (_min > 0) _min = 0;
            Flush();
        }

        var peaks = _peaks.ToArray();
        Normalize(peaks);
        return peaks;
    }

    private void Flush()
    {
        _peaks.Add(new Peak(_max, _min));
        _max = short.MinValue;
        _min = short.MaxValue;
        _filled = 0;
    }

    internal static bool Normalize(Peak[] peaks)
    {
        int maxAbs = 0;
        foreach (var p in peaks)
        {
            maxAbs = Math.Max(maxAbs, Math.Max((int)p.Max, -(int)p.Min));
        }

        if (maxAbs == 0)
        {
            return false;
        }

        double factor = 32768.0 / maxAbs;
        if (factor <= 1.1)
        {
            return false;
        }

        for (int i = 0; i < peaks.Length; i++)
        {
            peaks[i] = new Peak(Clamp(peaks[i].Max * factor), Clamp(peaks[i].Min * factor));
        }

        return true;
    }

    // O Delphi atribui Round() a SmallInt sem saturar; aqui saturamos para não inverter o sinal.
    private static short Clamp(double v) => (short)Math.Clamp(Math.Round(v), short.MinValue, short.MaxValue);
}
