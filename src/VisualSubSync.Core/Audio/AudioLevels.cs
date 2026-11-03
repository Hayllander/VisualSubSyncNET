namespace VisualSubSync.Core.Audio;

/// <summary>
/// Funcionalidade nova do VisualSubSyncNET (não existe no 1.0.1 nem no Enhanced): o nível do áudio num
/// trecho, lido da tabela de picos, para os plugins compararem a legenda com a fala. O nível é o RMS dos
/// picos (o maior valor absoluto de cada bloco de 10 ms) em porcentagem do maior pico do arquivo, para
/// que o mesmo limiar sirva para áudios gravados mais altos ou mais baixos.
/// </summary>
public sealed class AudioLevels
{
    /// <summary>Janela usada nas buscas: 30 ms seguidos, para um estalo isolado não contar como fala.</summary>
    public const int SearchWindowMs = 30;

    private readonly PeakData _peaks;
    private readonly double _reference;

    public AudioLevels(PeakData peaks)
    {
        ArgumentNullException.ThrowIfNull(peaks);
        _peaks = peaks;
        int max = 0;
        foreach (var p in peaks.Peaks)
        {
            max = Math.Max(max, Math.Max(Math.Abs((int)p.Max), Math.Abs((int)p.Min)));
        }

        _reference = max;
    }

    private int PeakIndex(int ms) => (int)Math.Floor(ms / _peaks.PeakDurationMs);

    private int PeakTime(int index) => (int)Math.Round(index * _peaks.PeakDurationMs, MidpointRounding.AwayFromZero);

    /// <summary>Nível de [startMs, stopMs) em 0–100; 0 fora do áudio ou com áudio todo em silêncio.</summary>
    public double Level(int startMs, int stopMs) => LevelOfPeaks(PeakIndex(startMs), PeakIndex(Math.Max(stopMs - 1, startMs)));

    private double LevelOfPeaks(int first, int last)
    {
        first = Math.Max(first, 0);
        last = Math.Min(last, _peaks.Peaks.Length - 1);
        if (last < first || _reference <= 0)
        {
            return 0;
        }

        double sum = 0;
        for (int i = first; i <= last; i++)
        {
            var p = _peaks.Peaks[i];
            double v = Math.Max(Math.Abs((int)p.Max), Math.Abs((int)p.Min));
            sum += v * v;
        }

        return Math.Sqrt(sum / (last - first + 1)) * 100.0 / _reference;
    }

    /// <summary>
    /// Percorre de fromMs para toMs (para trás quando toMs &lt; fromMs), em passos de um pico, até achar
    /// uma janela de <see cref="SearchWindowMs"/> acima do nível (<paramref name="above"/>) ou abaixo dele.
    /// Para frente devolve o início dessa janela; para trás, o fim dela (onde o trecho achado acaba). -1
    /// se não houver.
    /// </summary>
    public int Find(int fromMs, int toMs, double level, bool above)
    {
        int window = Math.Max(1, (int)Math.Ceiling(SearchWindowMs / _peaks.PeakDurationMs));
        int from = PeakIndex(fromMs);
        int to = PeakIndex(toMs);
        int step = to >= from ? 1 : -1;
        for (int i = from; step > 0 ? i <= to : i >= to; i += step)
        {
            if (i < 0 || i >= _peaks.Peaks.Length)
            {
                continue;
            }

            // Para frente a janela começa no bloco; para trás ela termina nele. Todos os blocos da janela
            // precisam cumprir a condição, para a borda achada ser a do trecho e não a de um estalo.
            int first = step > 0 ? i : i - window + 1;
            bool all = true;
            for (int k = first; k < first + window && all; k++)
            {
                double value = LevelOfPeaks(k, k);
                all = above ? value >= level : value < level;
            }

            if (all)
            {
                return PeakTime(step > 0 ? i : i + 1);
            }
        }

        return -1;
    }
}
