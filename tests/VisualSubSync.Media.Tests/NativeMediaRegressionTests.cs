using System.Text;
using VisualSubSync.Media.FFmpeg;
using VisualSubSync.Media.Mpv;

namespace VisualSubSync.Media.Tests;

/// <summary>Real FFmpeg/libmpv on generated audio only; no user media or audible playback.</summary>
public sealed class NativeMediaRegressionTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("vss-native-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string WriteWav(int durationMs)
    {
        string path = Path.Combine(_folder, $"source-{durationMs}.wav");
        int frames = durationMs * 48;
        using var writer = new BinaryWriter(File.Create(path), Encoding.ASCII);
        writer.Write("RIFF"u8); writer.Write(36 + frames * 2); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(48000);
        writer.Write(96000); writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8); writer.Write(frames * 2);
        for (int i = 0; i < frames; i++) writer.Write((short)(Math.Sin(i * .2) * 10000));
        return path;
    }

    private sealed class CancelOnProgress(CancellationTokenSource cancellation) : IProgress<double>
    {
        public void Report(double value) => cancellation.Cancel();
    }

    [Fact]
    public async Task Cancelled_extraction_does_not_replace_an_existing_wav()
    {
        string source = WriteWav(20000);
        string target = Path.Combine(_folder, "existing.wav");
        File.WriteAllText(target, "existing audio must survive");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FFmpegAudioExtractor()
            .ExtractPeaksAsync(source, target, 0, new CancelOnProgress(cancellation), cancellation.Token));
        Assert.Equal("existing audio must survive", File.ReadAllText(target));
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Theory]
    [InlineData(true, 24000)]
    [InlineData(false, 48000)]
    public async Task Successful_extraction_commits_a_complete_wav(bool fast, uint expectedRate)
    {
        string source = WriteWav(2000);
        string target = Path.Combine(_folder, "output.wav");
        var peaks = await new FFmpegAudioExtractor(fast).ExtractPeaksAsync(source, target, 0, null, TestContext.Current.CancellationToken);
        Assert.Equal(expectedRate, peaks.SamplesPerSecond);
        Assert.Equal((ushort)1, peaks.Channels);
        Assert.InRange(peaks.LengthMs, 1998, 2002);
        using var reader = new BinaryReader(File.OpenRead(target));
        Assert.Equal("RIFF", Encoding.ASCII.GetString(reader.ReadBytes(4)));
        Assert.Equal(new FileInfo(target).Length - 8, reader.ReadUInt32());
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Fact]
    public async Task Invalid_stream_selection_releases_input_file()
    {
        string source = WriteWav(1000);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new FFmpegAudioExtractor()
            .ExtractPeaksAsync(source, null, 99, null, TestContext.Current.CancellationToken));
        File.Delete(source);
        Assert.False(File.Exists(source));
    }

    [Fact]
    public async Task Closing_real_mpv_unloads_file_and_allows_reopening()
    {
        string source = WriteWav(1000);
        await using var engine = new MpvMediaEngine(headless: true);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        await engine.OpenAsync(source, cancellation.Token);
        await engine.SeekAsync(0, cancellation.Token);
        Assert.True(engine.IsOpen);
        engine.Close();
        Assert.False(engine.IsOpen);
        Assert.False(engine.IsPlaying);
        Assert.Equal(0, engine.DurationMs);
        Assert.Equal(0, engine.PositionMs);
        await engine.OpenAsync(source, cancellation.Token);
        Assert.True(engine.IsOpen);
    }

    [Fact]
    public async Task Loop_at_eof_keeps_playing_after_keep_open_pauses()
    {
        string source = WriteWav(200);
        await using var engine = new MpvMediaEngine(headless: true);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        await engine.OpenAsync(source, cancellation.Token);
        await engine.SeekAsync(0, cancellation.Token);
        long updates = engine.PositionUpdates;
        engine.Play(0, 400, loop: true); // stop is past EOF, exercising eof-reached rather than the range limit.
        await Task.Delay(750, cancellation.Token);
        Assert.True(engine.IsPlaying);
        Assert.True(engine.PositionUpdates - updates > 4);
        engine.Stop();
    }

    [Fact]
    public async Task External_wav_selection_restores_the_original_audio_track()
    {
        string source = WriteWav(1000);
        string external = WriteWav(2000);
        await using var engine = new MpvMediaEngine(headless: true);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        await engine.OpenAsync(source, cancellation.Token);
        await engine.SeekAsync(0, cancellation.Token);
        long original = engine.SelectedAudioTrackId;
        Assert.True(original > 0);
        engine.SetExternalAudio(external);
        Assert.NotEqual(original, engine.SelectedAudioTrackId);
        engine.SetExternalAudio(null);
        Assert.Equal(original, engine.SelectedAudioTrackId);
    }

    [Fact]
    public async Task Resuming_after_pause_does_not_add_the_paused_wall_time_to_the_cursor()
    {
        string source = WriteWav(2000);
        await using var engine = new MpvMediaEngine(headless: true);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        await engine.OpenAsync(source, cancellation.Token);
        await engine.SeekAsync(0, cancellation.Token);
        engine.Play(0, 1500);
        await Task.Delay(100, cancellation.Token);
        engine.Pause();
        await Task.Delay(200, cancellation.Token);
        int before = engine.PositionMs;
        engine.Resume();
        Assert.InRange(engine.InterpolatedPositionMs - before, 0, 100);
        engine.Stop();
    }
}
