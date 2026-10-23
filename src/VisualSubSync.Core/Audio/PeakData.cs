using System.Runtime.InteropServices;

namespace VisualSubSync.Core.Audio;

/// <summary>Par mínimo/máximo de um bloco de amostras (TPeak em WAVDisplayerUnit.pas). Ordem binária: Max, Min.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct Peak(short Max, short Min);

/// <summary>Tabela de picos usada para desenhar a forma de onda sem reler o áudio.</summary>
public sealed class PeakData
{
    public required int LengthMs { get; init; }

    public required uint SamplesPerSecond { get; init; }

    public required ushort Channels { get; init; }

    public required ushort BitsPerSample { get; init; }

    /// <summary>Amostras por pico. O legado usa SamplesPerSecond / 100, ou seja, um pico a cada 10 ms.</summary>
    public required uint SamplesPerPeak { get; init; }

    public required Peak[] Peaks { get; init; }

    /// <summary>Duração de um pico em milissegundos.</summary>
    public double PeakDurationMs => SamplesPerPeak * 1000.0 / SamplesPerSecond;
}
