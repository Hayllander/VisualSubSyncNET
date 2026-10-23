using System.Diagnostics;
using System.Globalization;
using System.Text;
using VisualSubSync.Core.Audio;
using VisualSubSync.Media.FFmpeg;
using VisualSubSync.Media.Mpv;

// Medições da prova de conceito (seção "Prova de conceito" do plano).
// Uso: vss-medir-midia <vídeo> [--buscas N] [--trechos N] [--legenda arquivo.ass] [--saida relatorio.md]
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
if (args.Length == 0)
{
    Console.WriteLine("Uso: vss-medir-midia <vídeo> [--buscas 200] [--trechos 20] [--legenda arquivo.ass] [--saida relatorio.md]");
    return 1;
}

string media = Path.GetFullPath(args[0]);
int seeks = IntArg("--buscas", 200), ranges = IntArg("--trechos", 20);
string? subtitle = StrArg("--legenda");
string output = StrArg("--saida") ?? Path.ChangeExtension(media, ".medicao.md");
var rng = new Random(12345);
var report = new StringBuilder();
report.AppendLine($"# Medição de mídia: {Path.GetFileName(media)}").AppendLine();
report.AppendLine($"- Data: {DateTime.Now:yyyy-MM-dd HH:mm}");
report.AppendLine($"- Sistema: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");

// ---- FFmpeg: picos e quadros-chave ----
var sw = Stopwatch.StartNew();
PeakData peaks = await new FFmpegAudioExtractor().ExtractPeaksAsync(media, null, null, CancellationToken.None);
long peakMs = sw.ElapsedMilliseconds;
string peakPath = Path.Combine(Path.GetTempPath(), "vss-medicao.peak");
PeakFile.Save(peaks, peakPath);
var reloaded = PeakFile.Load(peakPath);
File.Delete(peakPath);

sw.Restart();
int[] keyframes = await new FFmpegKeyframeReader().ReadKeyframesMsAsync(media, CancellationToken.None);
long kfMs = sw.ElapsedMilliseconds;

// ---- mpv ----
await using var engine = new MpvMediaEngine(headless: true);
sw.Restart();
await engine.OpenAsync(media);
long openMs = sw.ElapsedMilliseconds;
await Task.Delay(300); // espera duração, fps e tamanho chegarem por eventos
double fps = engine.FramesPerSecond;
double frameMs = fps > 0 ? 1000 / fps : 40;
int duration = engine.DurationMs;

report.AppendLine($"- Duração: {duration} ms, {fps:0.###} fps, {engine.VideoSize.Width}×{engine.VideoSize.Height}");
report.AppendLine($"- Abertura no mpv: {openMs} ms").AppendLine();

report.AppendLine("## Áudio e quadros-chave (FFmpeg)").AppendLine();
report.AppendLine($"- Picos: {peaks.Peaks.Length} em {peakMs} ms ({peaks.SamplesPerSecond} Hz, {peaks.Channels} canal(is), {peaks.SamplesPerPeak} amostras por pico)");
report.AppendLine($"- Duração pelos picos: {peaks.LengthMs} ms (diferença para o mpv: {peaks.LengthMs - duration} ms)");
report.AppendLine($"- `.peak` gravado e relido igual: {(reloaded.Peaks.AsSpan().SequenceEqual(peaks.Peaks) ? "sim" : "NÃO")}");
report.AppendLine($"- Quadros-chave: {keyframes.Length} em {kfMs} ms; primeiros: {string.Join(", ", keyframes.Take(6))}").AppendLine();

// ---- Busca exata ----
var seekErr = new List<double>();
var seekLat = new List<double>();
int withinFrame = 0;
for (int i = 0; i < seeks; i++)
{
    int target = rng.Next(0, Math.Max(1, duration - 1000));
    sw.Restart();
    await engine.SeekAsync(target).WaitAsync(TimeSpan.FromSeconds(10));
    seekLat.Add(sw.Elapsed.TotalMilliseconds);
    double pos = engine.QueryTimePosSeconds() * 1000;
    double err = pos - target;
    seekErr.Add(Math.Abs(err));
    if (Math.Abs(err) < frameMs)
    {
        withinFrame++;
    }
}

report.AppendLine($"## Busca exata ({seeks} posições aleatórias)").AppendLine();
report.AppendLine("| Medida | p50 | p95 | máx |").AppendLine("| --- | --- | --- | --- |");
report.AppendLine(Row("Erro do quadro exibido (ms)", seekErr));
report.AppendLine(Row("Latência da busca (ms)", seekLat));
report.AppendLine().AppendLine($"- Dentro de 1 quadro ({frameMs:0.#} ms): {withinFrame * 100.0 / seeks:0.#}%").AppendLine();

// ---- Tocar trecho e parar no fim ----
var overshoot = new List<double>();
var finalErr = new List<double>();
var updateRate = new List<double>();
for (int i = 0; i < ranges; i++)
{
    int start = rng.Next(0, Math.Max(1, duration - 5000));
    int stop = start + rng.Next(1000, 3000);
    var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    void OnStopped(object? s, EventArgs e) => stopped.TrySetResult();
    engine.PlaybackStopped += OnStopped;
    long before = engine.PositionUpdates;
    sw.Restart();
    engine.Play(start, stop);
    await stopped.Task.WaitAsync(TimeSpan.FromSeconds(15));
    double secs = sw.Elapsed.TotalSeconds;
    engine.PlaybackStopped -= OnStopped;
    updateRate.Add((engine.PositionUpdates - before) / secs);
    if (engine.LastRangeStopDetectedAtMs is int detected)
    {
        overshoot.Add(detected - stop);
    }

    await Task.Delay(150);
    finalErr.Add((engine.QueryTimePosSeconds() * 1000) - stop);
}

report.AppendLine($"## Tocar trecho ({ranges} trechos de 1 a 3 s)").AppendLine();
report.AppendLine("| Medida | p50 | p95 | máx |").AppendLine("| --- | --- | --- | --- |");
report.AppendLine(Row("Posição ao detectar o fim − fim (ms)", overshoot));
report.AppendLine(Row("Posição final após pausar − fim (ms)", finalErr));
report.AppendLine(Row("Atualizações de posição por segundo", updateRate));
report.AppendLine();

// ---- Velocidade ----
report.AppendLine("## Velocidade").AppendLine();
foreach (double rate in new[] { 0.6, 0.8, 1.0 })
{
    engine.PlaybackRate = rate;
    var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    void OnStopped(object? s, EventArgs e) => stopped.TrySetResult();
    engine.PlaybackStopped += OnStopped;
    int start = Math.Max(0, (duration / 2) - 1500);
    sw.Restart();
    engine.Play(start, start + 2000);
    await stopped.Task.WaitAsync(TimeSpan.FromSeconds(15));
    engine.PlaybackStopped -= OnStopped;
    report.AppendLine($"- {rate:P0}: 2000 ms de mídia em {sw.ElapsedMilliseconds} ms (esperado ≈ {2000 / rate:0})");
}

engine.PlaybackRate = 1.0;
report.AppendLine();

// ---- Legenda ----
if (subtitle is not null)
{
    try
    {
        engine.SetSubtitleOverlay(Path.GetFullPath(subtitle));
        engine.ReloadSubtitles();
        report.AppendLine("## Legenda").AppendLine().AppendLine($"- `{Path.GetFileName(subtitle)}` carregada e recarregada sem erro.").AppendLine();
    }
    catch (InvalidOperationException ex)
    {
        report.AppendLine("## Legenda").AppendLine().AppendLine($"- Falhou: {ex.Message}").AppendLine();
    }
}

await File.WriteAllTextAsync(output, report.ToString());
Console.WriteLine(report);
Console.WriteLine($"Relatório gravado em {output}");
return 0;

int IntArg(string name, int def) => StrArg(name) is { } v && int.TryParse(v, out int n) ? n : def;

string? StrArg(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string Row(string label, List<double> values)
{
    if (values.Count == 0)
    {
        return $"| {label} | – | – | – |";
    }

    var sorted = values.Order().ToArray();
    double P(double q) => sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(q * sorted.Length) - 1)];
    return $"| {label} | {P(0.5):0.#} | {P(0.95):0.#} | {sorted[^1]:0.#} |";
}
