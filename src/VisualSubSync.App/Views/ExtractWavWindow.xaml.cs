using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using VisualSubSync.App.Services;
using VisualSubSync.Core.Audio;
using VisualSubSync.Media;
using VisualSubSync.Media.FFmpeg;

namespace VisualSubSync.App.Views;

// O CancellationTokenSource é liberado no fim da extração (finally de OnExtract e OnClosed); o
// analisador não enxerga esse caminho e exigiria IDisposable na janela.
#pragma warning disable CA1001
/// <summary>
/// TExtractWAVForm (WAVExtractFormUnit.pas): abre o vídeo, conta os fluxos de áudio e extrai o WAV
/// e/ou o arquivo de picos (com FFmpeg no lugar do filtro DirectShow WavWriter do legado).
/// </summary>
public partial class ExtractWavWindow : Window
{
    private readonly ExtractWavRequest _request;
    private IReadOnlyList<AudioStreamInfo> _audioStreams = [];
    private bool _closed;
    private CancellationTokenSource? _cts;

    public ExtractWavWindow(ExtractWavRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        InitializeComponent();
        _request = request;
        SetExtractionType(request.Type);
        Loaded += OnLoaded;
    }

    /// <summary>Resultado quando a extração termina com sucesso; null enquanto não concluída.</summary>
    public ExtractWavResult? Result { get; private set; }

    /// <summary>SetExtractionType.</summary>
    private void SetExtractionType(WavExtractionType type)
    {
        OnlyPeakRadio.IsChecked = type == WavExtractionType.OnlyPeakFile;
        NoConversionRadio.IsChecked = type == WavExtractionType.NoConversion;
        FastConversionRadio.IsChecked = type == WavExtractionType.FastConversion;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => _ = AnalyseAsync();

    /// <summary>FormActivate: abre o arquivo e enumera os fluxos de áudio.</summary>
    private async Task AnalyseAsync()
    {
        ExtractButton.IsEnabled = false;
        StopButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
        SettingsFrame.IsEnabled = false;

        MemoVideoInfo.Clear();
        AddInfo("Opening and analyzing file :");
        AddInfo(_request.VideoFile);
        AddInfo("In case of problems first check : http://legendasrt.com.br/VisualSubSyncNET/");
        AddInfo("Please wait...");

        // TCursorManager(crHourGlass) do FormActivate.
        var previousCursor = Mouse.OverrideCursor;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            _audioStreams = await new FFmpegAudioExtractor()
                .GetAudioStreamsAsync(_request.VideoFile, CancellationToken.None).ConfigureAwait(true);
            AddInfo(string.Create(CultureInfo.InvariantCulture, $"{_audioStreams.Count} audio stream found."));
            if (_audioStreams.Count > 0)
            {
                StreamIndexCombo.ItemsSource = _audioStreams;
                StreamIndexCombo.DisplayMemberPath = nameof(AudioStreamInfo.DisplayName);
                StreamIndexCombo.SelectedIndex = Math.Clamp(_request.AudioStreamIndex, 0, _audioStreams.Count - 1);
                bool showSelection = _audioStreams.Count > 1;
                StreamToExtractLabel.Visibility = showSelection ? Visibility.Visible : Visibility.Collapsed;
                StreamIndexCombo.Visibility = showSelection ? Visibility.Visible : Visibility.Collapsed;
                AddInfo("Select a stream and press the 'Extract' button.");
            }
        }
        catch (Exception ex) when (IsMediaFailure(ex))
        {
            AddInfo($"Can't open the video file : {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = previousCursor;
            ExtractButton.IsEnabled = _audioStreams.Count > 0;
            CloseButton.IsEnabled = true;
            SettingsFrame.IsEnabled = true;
        }
    }

    /// <summary>bttExtractClick: extrai o WAV (quando pedido) e grava o arquivo de picos.</summary>
    private async void OnExtract(object sender, RoutedEventArgs e)
    {
        int selectedStream = StreamIndexCombo.SelectedItem is AudioStreamInfo stream ? stream.Index : 0;
        AddInfo(string.Create(CultureInfo.InvariantCulture, $"Extracting stream {selectedStream + 1}, please wait..."));
        ExtractButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        SettingsFrame.IsEnabled = false;
        CloseButton.IsEnabled = false;
        _cts = new CancellationTokenSource();

        bool onlyPeak = OnlyPeakRadio.IsChecked == true;
        try
        {
            var report = new Progress<double>(value => Progress.Value = Math.Clamp((int)Math.Round(value * 1000), 0, 1000));
            var peaks = await new FFmpegAudioExtractor(FastConversionRadio.IsChecked == true)
                .ExtractPeaksAsync(_request.VideoFile, onlyPeak ? null : _request.DestinationFile, selectedStream, report, _cts.Token)
                .ConfigureAwait(true);

            PeakFile.Save(peaks, Path.ChangeExtension(_request.DestinationFile, PeakFile.Extension));
            if (!_closed)
            {
                AddInfo("Extraction finished successfully.");
                Result = new ExtractWavResult(onlyPeak, _request.DestinationFile, selectedStream);
                DialogResult = true;
            }
        }
        catch (OperationCanceledException)
        {
            AddInfo("Extraction aborted by user.");
        }
        catch (Exception ex) when (IsMediaFailure(ex))
        {
            AddInfo($"Extraction failed : {ex.Message}");
        }
        finally
        {
            Progress.Value = 0;
            _cts?.Dispose();
            _cts = null;
            ExtractButton.IsEnabled = _audioStreams.Count > 0;
            StopButton.IsEnabled = false;
            SettingsFrame.IsEnabled = true;
            CloseButton.IsEnabled = true;
        }
    }

    /// <summary>bttStopClick: cancela a extração em curso.</summary>
    private void OnStop(object sender, RoutedEventArgs e) => _cts?.Cancel();

    /// <summary>bttDebugClick: o legado lista o grafo de filtros do DirectShow; aqui fica o backend usado.</summary>
    private void OnDebug(object sender, RoutedEventArgs e)
    {
        AddInfo("--- Filters list ---");
        AddInfo(string.Create(CultureInfo.InvariantCulture,
            $"FFmpeg backend, {_audioStreams.Count} audio stream(s) : {_request.VideoFile}"));
    }

    protected override void OnClosed(EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _closed = true;
        _cts?.Cancel();
        base.OnClosed(e);
    }

    /// <summary>Acrescenta uma linha ao memo, como MemoVideoInfo.Lines.Add.</summary>
    private void AddInfo(string line)
    {
        if (MemoVideoInfo.Text.Length > 0)
        {
            MemoVideoInfo.AppendText("\r\n");
        }

        MemoVideoInfo.AppendText(line);
        MemoVideoInfo.ScrollToEnd();
    }

    /// <summary>Falhas possíveis ao ler a mídia com o FFmpeg (o diálogo não pode derrubar o programa).</summary>
    private static bool IsMediaFailure(Exception ex) => ex is IOException or InvalidOperationException
        or NotSupportedException or ArgumentException or DllNotFoundException
        or EntryPointNotFoundException or BadImageFormatException;
}
#pragma warning restore CA1001
