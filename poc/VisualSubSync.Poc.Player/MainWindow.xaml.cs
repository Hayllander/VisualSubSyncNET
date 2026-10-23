using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using VisualSubSync.Media.FFmpeg;
using VisualSubSync.Media.Mpv;

namespace VisualSubSync.Poc.Player;

/// <summary>
/// Player mínimo para avaliar à mão o que o medidor não captura: vídeo dentro da janela WPF,
/// cursor da forma de onda sincronizado com o som, legendas ASS sobre o vídeo, mudança de velocidade.
/// Código direto na janela de propósito: é descartável e não segue o MVVM do aplicativo.
/// </summary>
public partial class MainWindow : Window
{
    private MpvMediaEngine? _engine;
    private int _frames;
    private readonly Stopwatch _fpsClock = Stopwatch.StartNew();
    private double _uiFps;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            try
            {
                _engine = new MpvMediaEngine(Video.VideoHandle);
                _engine.PlaybackStopped += (_, _) => Dispatcher.BeginInvoke(UpdateStopStatus);
                CompositionTarget.Rendering += OnRendering;
            }
            catch (Exception ex) when (ex is DllNotFoundException or InvalidOperationException)
            {
                StatusText.Text = ex.Message;
            }
        };
        Closed += async (_, _) =>
        {
            CompositionTarget.Rendering -= OnRendering;
            if (_engine is not null)
            {
                await _engine.DisposeAsync();
            }
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Space)
            {
                OnPlayPause(this, e);
                e.Handled = true;
            }
        };
    }

    private async void OnOpen(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Vídeo|*.mkv;*.mp4;*.avi;*.mov;*.webm;*.ts;*.wmv|Todos|*.*" };
        if (_engine is null || dialog.ShowDialog(this) != true)
        {
            return;
        }

        string path = dialog.FileName;
        var sw = Stopwatch.StartNew();
        await _engine.OpenAsync(path);
        long openMs = sw.ElapsedMilliseconds;
        StatusText.Text = $"Aberto em {openMs} ms. Extraindo picos...";

        sw.Restart();
        var progress = new Progress<double>(p => StatusText.Text = $"Extraindo picos... {p:P0}");
        var peaks = await new FFmpegAudioExtractor().ExtractPeaksAsync(path, null, progress, CancellationToken.None);
        long peakMs = sw.ElapsedMilliseconds;
        int[] keyframes = await new FFmpegKeyframeReader().ReadKeyframesMsAsync(path, CancellationToken.None);

        Wave.Peaks = peaks;
        Wave.PositionMs = 0;
        StatusText.Text = string.Create(CultureInfo.InvariantCulture,
            $"Aberto em {openMs} ms; picos em {peakMs} ms; {keyframes.Length} quadros-chave; {_engine.FramesPerSecond:0.###} fps.");
    }

    private void OnOpenSubtitle(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Legendas|*.ass;*.ssa;*.srt" };
        if (_engine is not null && dialog.ShowDialog(this) == true)
        {
            _engine.SetSubtitleOverlay(dialog.FileName);
        }
    }

    private void OnPlayPause(object sender, RoutedEventArgs e)
    {
        if (_engine is null || !_engine.IsOpen)
        {
            return;
        }

        if (_engine.IsPlaying)
        {
            _engine.Pause();
        }
        else
        {
            _engine.Play(Wave.CursorMs >= 0 ? Wave.CursorMs : _engine.PositionMs);
        }
    }

    private void OnPlaySelection(object sender, RoutedEventArgs e)
    {
        if (_engine is { IsOpen: true } && Wave.SelectionStopMs > Wave.SelectionStartMs && Wave.SelectionStartMs >= 0)
        {
            _engine.Play(Wave.SelectionStartMs, Wave.SelectionStopMs, LoopToggle.IsChecked == true);
        }
    }

    private void OnStop(object sender, RoutedEventArgs e) => _engine?.Stop();

    private async void OnShowStart(object sender, RoutedEventArgs e)
    {
        if (_engine is { IsOpen: true } && Wave.SelectionStartMs >= 0)
        {
            await _engine.SeekAsync(Wave.SelectionStartMs);
        }
    }

    private async void OnShowStop(object sender, RoutedEventArgs e)
    {
        if (_engine is { IsOpen: true } && Wave.SelectionStopMs >= 0)
        {
            await _engine.SeekAsync(Wave.SelectionStopMs);
        }
    }

    private void OnRateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_engine is not null && RateBox.SelectedItem is ComboBoxItem { Tag: string tag })
        {
            _engine.PlaybackRate = double.Parse(tag, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>A cada quadro da interface: cursor amarelo pela posição interpolada, rolagem automática.</summary>
    private void OnRendering(object? sender, EventArgs e)
    {
        if (_engine is not { IsOpen: true })
        {
            return;
        }

        int pos = _engine.InterpolatedPositionMs;
        Wave.PlayCursorMs = pos;
        if (_engine.IsPlaying && (pos < Wave.PositionMs || pos > Wave.PositionMs + Wave.PageSizeMs))
        {
            Wave.PositionMs = Math.Max(0, pos - (Wave.PageSizeMs / 10));
        }

        _frames++;
        if (_fpsClock.ElapsedMilliseconds >= 1000)
        {
            _uiFps = _frames * 1000.0 / _fpsClock.ElapsedMilliseconds;
            _frames = 0;
            _fpsClock.Restart();
            Title = string.Create(CultureInfo.InvariantCulture,
                $"VisualSubSync - prova de conceito  |  {pos} ms  |  interface {_uiFps:0} fps");
        }
    }

    private void UpdateStopStatus()
    {
        if (_engine?.LastRangeStopDetectedAtMs is int detected)
        {
            StatusText.Text = string.Create(CultureInfo.InvariantCulture,
                $"Fim do trecho detectado em {detected} ms (fim pedido: {Wave.SelectionStopMs} ms, diferença {detected - Wave.SelectionStopMs} ms).");
        }
    }
}
