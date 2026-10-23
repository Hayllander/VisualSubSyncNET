using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VisualSubSync.Core.Audio;
using VisualSubSync.Core.Settings;
using VisualSubSync.Core.Waveform;

namespace VisualSubSync.Waveform;

/// <summary>
/// Porta de TWAVDisplayer (WAVDisplayerUnit.pas) com o mesmo desenho, pixel a pixel: onda,
/// linha zero, régua de 20 px, barra de rolagem de 12 px (TMiniScrollBar), legendas como linhas
/// pontilhadas com traços horizontais e texto, seleção invertida e cursor em XOR.
/// O desenho é feito num bitmap em pixels físicos; as medidas do original (em pixels a 96 DPI)
/// são multiplicadas pela escala do monitor.
/// </summary>
public sealed partial class WaveformControl : FrameworkElement, IWaveformView
{
    public bool IsMouseInteracting => _mouse.MouseIsDown || _thumbGrabOffset is not null;
    private const int RulerHeight96 = 20;      // FDisplayRulerHeight
    private const int ScrollBarHeight96 = 12;  // MiniSB.Height
    private const int ThumbMinSize96 = 5;      // FThumbMinSize
    private const int TextMargins96 = 5;       // TEXT_MARGINS em WAVDisplayer1CustomDrawRange
    private const int MinimalTextSpace96 = 25; // MINIMAL_SPACE

    public static readonly DependencyProperty PeaksProperty = Register<PeakData?>(nameof(Peaks), null, Layer.Base);
    public static readonly DependencyProperty LengthMsProperty = Register(nameof(LengthMs), 0, Layer.Base);
    public static readonly DependencyProperty PositionMsProperty = Register(nameof(PositionMs), 0, Layer.Base);
    public static readonly DependencyProperty PageSizeMsProperty = Register(nameof(PageSizeMs), 0, Layer.Base);
    public static readonly DependencyProperty VerticalScalingProperty = Register(nameof(VerticalScaling), 100, Layer.Base);
    public static readonly DependencyProperty CursorMsProperty = Register(nameof(CursorMs), 0, Layer.Overlay);
    public static readonly DependencyProperty PlayCursorMsProperty = Register(nameof(PlayCursorMs), 0, Layer.Overlay);
    public static readonly DependencyProperty IsPlayingProperty = Register(nameof(IsPlaying), false, Layer.Overlay);
    public static readonly DependencyProperty SelectionStartMsProperty = Register(nameof(SelectionStartMs), 0, Layer.Overlay);
    public static readonly DependencyProperty SelectionStopMsProperty = Register(nameof(SelectionStopMs), 0, Layer.Overlay);
    public static readonly DependencyProperty RangesProperty = Register<IReadOnlyList<WaveformRange>?>(nameof(Ranges), null, Layer.Text);
    public static readonly DependencyProperty VoRangesProperty = Register<IReadOnlyList<WaveformRange>?>(nameof(VoRanges), null, Layer.Text);
    public static readonly DependencyProperty ShowVOProperty = Register(nameof(ShowVO), false, Layer.Base);
    public static readonly DependencyProperty ShowTextProperty = Register(nameof(ShowText), true, Layer.Text);
    public static readonly DependencyProperty TextFontFamilyProperty = Register(nameof(TextFontFamily), new FontFamily("Arial"), Layer.Text);
    public static readonly DependencyProperty TextFontSizeProperty = Register(nameof(TextFontSize), 13.333, Layer.Text);
    public static readonly DependencyProperty TextFontWeightProperty = Register(nameof(TextFontWeight), FontWeights.Bold, Layer.Text);
    public static readonly DependencyProperty TextFontStyleProperty = Register(nameof(TextFontStyle), FontStyles.Normal, Layer.Text);
    public static readonly DependencyProperty SelectionModeProperty = Register(nameof(SelectionMode), WaveformSelectionMode.CoolEdit, Layer.Overlay);
    public static readonly DependencyProperty SnappingEnabledProperty = Register(nameof(SnappingEnabled), true, Layer.Overlay);
    public static readonly DependencyProperty AntiOverlappingProperty = Register(nameof(AntiOverlapping), false, Layer.Overlay);
    public static readonly DependencyProperty MinimumBlankMsProperty = Register(nameof(MinimumBlankMs), 120, Layer.Overlay);
    public static readonly DependencyProperty SceneChangesProperty = Register<IReadOnlyList<int>?>(nameof(SceneChanges), null, Layer.Overlay);
    public static readonly DependencyProperty SceneChangeEnabledProperty = Register(nameof(SceneChangeEnabled), false, Layer.Overlay);
    public static readonly DependencyProperty SceneChangeStartOffsetProperty = Register(nameof(SceneChangeStartOffset), 130, Layer.Overlay);
    public static readonly DependencyProperty SceneChangeStopOffsetProperty = Register(nameof(SceneChangeStopOffset), 130, Layer.Overlay);
    public static readonly DependencyProperty SceneChangeFilterOffsetProperty = Register(nameof(SceneChangeFilterOffset), 250, Layer.Overlay);

    private static readonly Typeface RulerTypeface = new("Arial");

    /// <summary>crHSplit (CheckSubtitleForDynamicSelection): duas barras com setas, desenhado como o da VCL.</summary>
    private static readonly Cursor HSplitCursor = LoadHSplitCursor();
    private const double RulerFontSize96 = 8 * 96 / 72.0; // Font.Size := 8

    private WriteableBitmap? _bitmap;
    private int[] _base = [];
    private int[] _frame = [];
    private int[]? _textLayer;
    private int _w, _h;
    private double _scale = 1;
    private bool _baseDirty = true, _textDirty = true;

    private readonly WaveformMouse _mouse = new();
    private bool _syncing;
    private int? _thumbGrabOffset;

    /// <summary>FRepeatTimer da TMiniScrollBar: segurando o botão no trilho, pagina a cada 150 ms.</summary>
    private readonly System.Windows.Threading.DispatcherTimer _scrollRepeatTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private int _lastScrollBarX;

    public WaveformControl()
    {
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        Cursor = Cursors.IBeam;
        _mouse.CursorChanged += (_, _) => PushState();
        _mouse.SelectionChanged += (_, _) => PushState();
        _mouse.RangeSelected += (_, e) =>
        {
            PushState();
            RangeSelected?.Invoke(this, new WaveformRangeEventArgs(e.Index, e.IsDynamic));
        };
        _mouse.SelectedRangeTimesChanging += (_, e) => SelectedRangeChanging?.Invoke(this, new WaveformRangeTimesEventArgs(e));
        _mouse.SelectedRangeTimesChanged += (_, e) => SelectedRangeChanged?.Invoke(this, new WaveformRangeTimesEventArgs(e));
        _mouse.MoveMouseRequested += (_, x) => MoveMouseTo(x);
        _mouse.ClipMouseRequested += (_, clip) => ClipMouse(clip);
        _mouse.ScrollRequested += (_, position) =>
        {
            // Botão do meio: SetAutoScrollInternal(False).
            UserScrolled?.Invoke(this, EventArgs.Empty);
            SetPosition(position);
        };
        _scrollRepeatTimer.Tick += (_, _) => ScrollBarRepeat();
        _mouse.KaraokeTimeChanging += (_, e) => KaraokeChanging?.Invoke(this, e);
        _mouse.KaraokeTimeChanged += (_, e) => KaraokeChanged?.Invoke(this, e);
        _mouse.RangeEdgeDoubleClicked += (_, e) => RangeEdgeDoubleClicked?.Invoke(this, new WaveformRangeEdgeEventArgs(e.Index, e.IsStart));
        _mouse.KaraokeRangeSelected += (_, index) =>
        {
            PushState();
            KaraokeRangeSelected?.Invoke(this, index);
        };
    }

    /// <summary>OnRangeStartDblClick / OnRangeStopDblClick: duplo clique na borda de uma legenda.</summary>
    public event EventHandler<WaveformRangeEdgeEventArgs>? RangeEdgeDoubleClicked;

    /// <summary>
    /// A vista foi rolada pelo usuário (mini barra ou botão do meio): OnScrollBarChange/SetAutoScroll(False)
    /// desliga o "Auto-scroll WAV display".
    /// </summary>
    public event EventHandler? UserScrolled;

    /// <summary>Um marcador de karaokê está sendo arrastado.</summary>
    public event EventHandler<KaraokeChange>? KaraokeChanging;

    /// <summary>OnKaraokeChanged: o arraste de um marcador terminou.</summary>
    public event EventHandler<KaraokeChange>? KaraokeChanged;

    /// <summary>OnSelectedKaraokeRange: índice da legenda do trecho selecionado, ou -1.</summary>
    public event EventHandler<int>? KaraokeRangeSelected;

    /// <summary>
    /// OnSelectedRange(Range, IsDynamic): duplo clique numa legenda, ou início da edição pela borda
    /// (dinâmica). A janela foca a legenda na lista.
    /// </summary>
    public event EventHandler<WaveformRangeEventArgs>? RangeSelected;

    /// <summary>OnSelectedRangeChange: os tempos da legenda selecionada mudaram durante o arraste.</summary>
    public event EventHandler<WaveformRangeTimesEventArgs>? SelectedRangeChanging;

    /// <summary>OnSelectedRangeChanged: fim do arraste; a ordem das legendas pode ter mudado.</summary>
    public event EventHandler<WaveformRangeTimesEventArgs>? SelectedRangeChanged;

    public PeakData? Peaks { get => (PeakData?)GetValue(PeaksProperty); set => SetValue(PeaksProperty, value); }

    /// <summary>Duração total (FLengthMs). Zero = controle desabilitado, pintado de cinza.</summary>
    public int LengthMs { get => (int)GetValue(LengthMsProperty); set => SetValue(LengthMsProperty, value); }

    public int PositionMs { get => (int)GetValue(PositionMsProperty); set => SetValue(PositionMsProperty, value); }

    public int PageSizeMs { get => (int)GetValue(PageSizeMsProperty); set => SetValue(PageSizeMsProperty, value); }

    /// <summary>FVerticalScaling, em %, de 0 a 400.</summary>
    public int VerticalScaling { get => (int)GetValue(VerticalScalingProperty); set => SetValue(VerticalScalingProperty, value); }

    public int CursorMs { get => (int)GetValue(CursorMsProperty); set => SetValue(CursorMsProperty, value); }

    public int PlayCursorMs { get => (int)GetValue(PlayCursorMsProperty); set => SetValue(PlayCursorMsProperty, value); }

    public bool IsPlaying { get => (bool)GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }

    public int SelectionStartMs { get => (int)GetValue(SelectionStartMsProperty); set => SetValue(SelectionStartMsProperty, value); }

    /// <summary>Fim da seleção; 0 = sem seleção, como FSelection.StopTime.</summary>
    public int SelectionStopMs { get => (int)GetValue(SelectionStopMsProperty); set => SetValue(SelectionStopMsProperty, value); }

    public IReadOnlyList<WaveformRange>? Ranges
    {
        get => (IReadOnlyList<WaveformRange>?)GetValue(RangesProperty);
        set => SetValue(RangesProperty, value);
    }

    /// <summary>RangeListVO: as legendas de referência, só para mostrar.</summary>
    public IReadOnlyList<WaveformRange>? VoRanges
    {
        get => (IReadOnlyList<WaveformRange>?)GetValue(VoRangesProperty);
        set => SetValue(VoRangesProperty, value);
    }

    /// <summary>ShowVO: a faixa de cima mostra o VO e a de baixo as legendas.</summary>
    public bool ShowVO { get => (bool)GetValue(ShowVOProperty); set => SetValue(ShowVOProperty, value); }

    /// <summary>ShowTextInWAVDisplay das preferências.</summary>
    public bool ShowText { get => (bool)GetValue(ShowTextProperty); set => SetValue(ShowTextProperty, value); }

    /// <summary>Fonte do texto das legendas: a mesma do editor (MemoSubtitleText.Font).</summary>
    public FontFamily TextFontFamily { get => (FontFamily)GetValue(TextFontFamilyProperty); set => SetValue(TextFontFamilyProperty, value); }

    public double TextFontSize { get => (double)GetValue(TextFontSizeProperty); set => SetValue(TextFontSizeProperty, value); }

    public FontWeight TextFontWeight { get => (FontWeight)GetValue(TextFontWeightProperty); set => SetValue(TextFontWeightProperty, value); }

    /// <summary>Itálico da fonte do editor (ACanvas.Font.Assign(MemoSubtitleText.Font)).</summary>
    public FontStyle TextFontStyle { get => (FontStyle)GetValue(TextFontStyleProperty); set => SetValue(TextFontStyleProperty, value); }

    /// <summary>SelMode: CoolEdit, ou SSA no modo Timing com "EnableSSATimingMode".</summary>
    public WaveformSelectionMode SelectionMode { get => (WaveformSelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>SnappingEnabled = EnableMouseSnapping.</summary>
    public bool SnappingEnabled { get => (bool)GetValue(SnappingEnabledProperty); set => SetValue(SnappingEnabledProperty, value); }

    /// <summary>EnableMouseAntiOverlapping.</summary>
    public bool AntiOverlapping { get => (bool)GetValue(AntiOverlappingProperty); set => SetValue(AntiOverlappingProperty, value); }

    /// <summary>MinimumBlank = SpaceKeyBlankBetweenSubtitles.</summary>
    public int MinimumBlankMs { get => (int)GetValue(MinimumBlankMsProperty); set => SetValue(MinimumBlankMsProperty, value); }

    /// <summary>FSceneChangeList (ordem crescente).</summary>
    public IReadOnlyList<int>? SceneChanges { get => (IReadOnlyList<int>?)GetValue(SceneChangesProperty); set => SetValue(SceneChangesProperty, value); }

    /// <summary>SceneChangeEnabled = ShowSceneChange.</summary>
    public bool SceneChangeEnabled { get => (bool)GetValue(SceneChangeEnabledProperty); set => SetValue(SceneChangeEnabledProperty, value); }

    public int SceneChangeStartOffset { get => (int)GetValue(SceneChangeStartOffsetProperty); set => SetValue(SceneChangeStartOffsetProperty, value); }

    public int SceneChangeStopOffset { get => (int)GetValue(SceneChangeStopOffsetProperty); set => SetValue(SceneChangeStopOffsetProperty, value); }

    public int SceneChangeFilterOffset { get => (int)GetValue(SceneChangeFilterOffsetProperty); set => SetValue(SceneChangeFilterOffsetProperty, value); }

    /// <summary>FSelectedRange: índice em <see cref="Ranges"/> da legenda presa à seleção, ou -1.</summary>
    public int SelectedRangeIndex => _mouse.SelectedRangeIndex;

    public int KaraokeSelectedIndex => _mouse.SelectedKaraokeIndex;

    public int KaraokeSelectedRangeIndex => _mouse.SelectedKaraokeRangeIndex;

    public void SelectNextKaraoke() => _mouse.SelectNextKaraoke();

    public void SelectPreviousKaraoke() => _mouse.SelectPreviousKaraoke();

    private int RulerHeight => Px(RulerHeight96);

    private int ScrollBarHeight => Px(ScrollBarHeight96);

    /// <summary>GetWavCanvasHeight: altura sem régua e barra de rolagem.</summary>
    private int WavHeight => Math.Max(0, _h - ScrollBarHeight - RulerHeight);

    private int Px(int pixels96) => Math.Max(1, (int)Math.Round(pixels96 * _scale));

    private int LineWidth => Math.Max(1, (int)Math.Round(_scale));

    private int TimeToPixel(int time) =>
        PageSizeMs == 0 ? 0 : (int)Math.Round(time / (PageSizeMs / (double)_w));

    private int PixelToTime(int pixel) =>
        PageSizeMs == 0 ? 0 : (int)Math.Round(pixel * (PageSizeMs / (double)_w));

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        var dpi = VisualTreeHelper.GetDpi(this);
        int w = (int)Math.Round(ActualWidth * dpi.DpiScaleX), h = (int)Math.Round(ActualHeight * dpi.DpiScaleY);
        if (w <= 0 || h <= 0)
        {
            return;
        }

        if (_bitmap is null || w != _w || h != _h || Math.Abs(dpi.DpiScaleX - _scale) > 0.001)
        {
            _w = w;
            _h = h;
            _scale = dpi.DpiScaleX;
            _bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
            _base = new int[w * h];
            _frame = new int[w * h];
            _baseDirty = _textDirty = true;
        }

        if (_baseDirty)
        {
            PaintBase();
            _baseDirty = false;
        }

        Compose();
        _bitmap.WritePixels(new Int32Rect(0, 0, w, h), _frame, w * 4, 0);
        drawingContext.DrawImage(_bitmap, new Rect(0, 0, ActualWidth, ActualHeight));
    }

    // ---------------------------------------------------------------- base: onda e régua

    private void PaintBase()
    {
        int wavH = WavHeight;
        if (LengthMs == 0)
        {
            Array.Fill(_base, WaveformPalette.DisabledBack);
            return;
        }

        FillRect(_base, 0, 0, _w, wavH, WaveformPalette.Background);
        int middle = wavH / 2;
        // PaintWavOnCanvas: sem picos carregados, só o fundo preto (sem linha zero nem cor alternada).
        if (Peaks is { Peaks.Length: > 0 } data)
        {
            if (PageSizeMs > 0)
            {
                PaintWave(data, wavH, middle);
            }

            HLine(_base, 0, _w, middle, WaveformPalette.ZeroLine, 1);
            PaintAlternateWavColor(wavH);
        }

        PaintRuler(wavH);
    }

    /// <summary>Faixas de FDisplayRangeLists: 1, ou 2 com o VO.</summary>
    private int BandCount => ShowVO ? 2 : 1;

    /// <summary>A faixa i (de cima para baixo): Round(SubCanvasHeight * i) até Round(SubCanvasHeight * (i + 1)).</summary>
    private (int Top, int Bottom) Band(int i, int wavH)
    {
        double sub = wavH / (double)BandCount;
        return ((int)Math.Round(sub * i), (int)Math.Round(sub * (i + 1)));
    }

    /// <summary>A lista da faixa i: FDisplayRangeLists[Count - i - 1], então o VO fica em cima.</summary>
    private bool BandIsVo(int i) => BandCount == 2 && i == 0;

    /// <summary>PaintAlternateWavColor: nas faixas ímpares a onda (só os pixels da cor dela) fica em $00587F27.</summary>
    private void PaintAlternateWavColor(int wavH)
    {
        const int newColor = 0x277F58;
        for (int i = 1; i < BandCount; i += 2)
        {
            var (top, bottom) = Band(i, wavH);
            for (int y = top; y < Math.Min(bottom, wavH); y++)
            {
                int row = y * _w;
                for (int x = 0; x < _w; x++)
                {
                    if (_base[row + x] == WaveformPalette.Wave)
                    {
                        _base[row + x] = newColor;
                    }
                }
            }
        }
    }

    /// <summary>PaintWavOnCanvas: uma linha vertical por coluna, do máximo ao mínimo do trecho.</summary>
    private void PaintWave(PeakData data, int rectHeight, int middle)
    {
        var tab = data.Peaks;
        double peaksPerPixel = PageSizeMs / data.PeakDurationMs / _w;
        double start = PositionMs / data.PeakDurationMs;
        double vs = VerticalScaling / 100.0;
        for (int x = 0; x < _w; x++)
        {
            int xs = (int)Math.Min(Math.Round((x * peaksPerPixel) + start), tab.Length - 1);
            int max = tab[xs].Max, min = tab[xs].Min;
            int end = (int)Math.Min(Math.Round(((x + 1) * peaksPerPixel) + start), tab.Length);
            for (int i = xs + 1; i < end; i++)
            {
                if (tab[i].Max > max)
                {
                    max = tab[i].Max;
                }

                if (tab[i].Min < min)
                {
                    min = tab[i].Min;
                }
            }

            int y1 = (int)Math.Round(max * vs * rectHeight / 65536);
            int y2 = (int)Math.Round(min * vs * rectHeight / 65536);
            VLine(_base, x, middle - y1, middle - y2, WaveformPalette.Wave, dotted: false, 0, rectHeight);
        }
    }

    /// <summary>PaintRulerOnCanvas: divisões "redondas", texto com sombra e subdivisões.</summary>
    private void PaintRuler(int top)
    {
        int bottom = top + RulerHeight;
        FillRect(_base, 0, top, _w, bottom, WaveformPalette.RulerBack);
        HLine(_base, 0, _w, top, WaveformPalette.RulerTopBottomLine, 1);
        HLine(_base, 0, _w, bottom - 1, WaveformPalette.RulerTopBottomLine, 1);
        if (PageSizeMs <= 0)
        {
            return;
        }

        double fontSize = RulerFontSize96 * _scale;
        int maxPosStep = (int)Math.Round(_w / (MeasureWidth("000:00.0", RulerTypeface, fontSize) * 2));
        int stepMs = maxPosStep == 0 ? PageSizeMs : (int)Math.Round(PageSizeMs / (double)maxPosStep);
        stepMs = Math.Max(1, stepMs);
        int stepLog = (int)Math.Pow(10, Math.Truncate(Math.Log10(stepMs)));
        stepMs = stepMs / stepLog * stepLog;

        var texts = new List<TextItem>();
        int dy = Px(4), shadow = Px(2);
        for (int p = PositionMs / stepMs * stepMs; p < PositionMs + PageSizeMs; p += stepMs)
        {
            int x = TimeToPixel(p - PositionMs);
            VLine(_base, x, top + Px(1), top + Px(5), WaveformPalette.RulerText, dotted: false, top, bottom);
            string label = TimeMsToShortString(p, stepLog);
            double x1 = x - Math.Floor(MeasureWidth(label, RulerTypeface, fontSize) / 2);
            texts.Add(new TextItem(label, x1 + shadow, top + dy + shadow, WaveformPalette.RulerTextShadow, RulerTypeface, fontSize, null));
            texts.Add(new TextItem(label, x1, top + dy, WaveformPalette.RulerText, RulerTypeface, fontSize, null));
            int x2 = x + TimeToPixel(stepMs / 2);
            VLine(_base, x2, top + Px(1), top + Px(3), WaveformPalette.RulerText, dotted: false, top, bottom);
        }

        BlendInto(_base, RenderText(texts, new Int32Rect(0, top, _w, RulerHeight)));
    }

    /// <summary>TimeMsToShortString do original, com as mesmas peculiaridades (casas sem zeros à esquerda).</summary>
    internal static string TimeMsToShortString(int timeMs, int precision)
    {
        int totalSec = timeMs / 1000;
        int min = totalSec / 60, sec = totalSec % 60;
        int ms = (timeMs - (min * 60 * 1000) - (sec * 1000)) / precision;
        return min > 0
            ? ms > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{min}:{sec:00}.{ms}")
                : string.Create(CultureInfo.InvariantCulture, $"{min}:{sec:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{sec}.{ms}");
    }

    // ---------------------------------------------------------------- composição

    private void Compose()
    {
        Array.Copy(_base, _frame, _base.Length);
        if (LengthMs > 0)
        {
            int wavH = WavHeight;
            PaintSceneChanges(wavH);
            PaintMinimumBlank(wavH);
            PaintRanges(wavH);
            PaintSelection(wavH);
            if (CursorMs >= PositionMs && CursorMs <= PositionMs + PageSizeMs)
            {
                XorVLine(TimeToPixel(CursorMs - PositionMs), wavH, WaveformPalette.Cursor, dotted: true);
            }

            if (IsPlaying && PlayCursorMs >= PositionMs && PlayCursorMs <= PositionMs + PageSizeMs)
            {
                XorVLine(TimeToPixel(PlayCursorMs - PositionMs), wavH, 0xFFFFFF, dotted: false);
            }
        }

        PaintScrollBar();
    }

    /// <summary>PaintSceneChange: linha de cada mudança de cena e, em volta, a faixa dos deslocamentos (por faixa).</summary>
    private void PaintSceneChanges(int wavH)
    {
        var scenes = SceneChanges;
        if (!SceneChangeEnabled || scenes is null || PageSizeMs <= 0)
        {
            return;
        }

        int startOffset = SceneChangeStartOffset, stopOffset = SceneChangeStopOffset;
        for (int band = 0; band < BandCount; band++)
        {
            var (top, bottom) = Band(band, wavH);
            bool vo = BandIsVo(band);
            foreach (int scene in scenes)
            {
                if (scene < PositionMs - stopOffset || scene > PositionMs + PageSizeMs + startOffset)
                {
                    continue;
                }

                if (stopOffset + startOffset > 0 && !_mouse.IsFilteredSceneChange(scene, vo))
                {
                    AlphaRect(scene - startOffset, scene + stopOffset, top, bottom, WaveformPalette.SceneChange);
                }

                VLine(_frame, TimeToPixel(scene - PositionMs), top, bottom, WaveformPalette.SceneChange, dotted: false, 0, wavH);
            }
        }
    }

    /// <summary>PaintMinimumBlank: o espaço mínimo das legendas vizinhas, em branco translúcido.</summary>
    private void PaintMinimumBlank(int wavH)
    {
        var ranges = _mouse.Ranges;
        int blank = MinimumBlankMs;
        foreach (var info in (ReadOnlySpan<MinBlankInfo>)[_mouse.MinBlank1, _mouse.MinBlank2])
        {
            if (info.Exists && info.RangeIndex < ranges.Count)
            {
                var r = ranges[info.RangeIndex];
                AlphaRect(info.GetStart(r, blank), info.GetStop(r, blank), 0, wavH, 0xFFFFFF);
            }
        }
    }

    /// <summary>DrawAlphaRect: AlphaBlend com alfa constante 80 e a cor da caneta.</summary>
    private void AlphaRect(int t1, int t2, int top, int bottom, int color)
    {
        int x1 = Math.Clamp(TimeToPixel(t1 - PositionMs), 0, _w);
        int x2 = Math.Clamp(TimeToPixel(t2 - PositionMs), 0, _w);
        const int alpha = 80;
        int cr = (color >> 16) & 0xFF, cg = (color >> 8) & 0xFF, cb = color & 0xFF;
        for (int y = top; y < bottom; y++)
        {
            int row = y * _w;
            for (int x = x1; x < x2; x++)
            {
                int d = _frame[row + x];
                int r = ((((d >> 16) & 0xFF) * (255 - alpha)) + (cr * alpha)) / 255;
                int g = ((((d >> 8) & 0xFF) * (255 - alpha)) + (cg * alpha)) / 255;
                int b = (((d & 0xFF) * (255 - alpha)) + (cb * alpha)) / 255;
                _frame[row + x] = (r << 16) | (g << 8) | b;
            }
        }
    }

    /// <summary>PaintOnCanvas: PaintRange em cada faixa, o VO em cima; WAVDisplayer1CustomDrawRange para o texto.</summary>
    private void PaintRanges(int wavH)
    {
        if (PageSizeMs <= 0)
        {
            return;
        }

        List<TextItem>? texts = _textDirty ? [] : null;
        int count = BandCount;
        for (int band = 0; band < count; band++)
        {
            bool vo = BandIsVo(band);
            var ranges = vo ? VoRanges : Ranges;
            if (ranges is not null)
            {
                var (top, bottom) = Band(band, wavH);
                PaintRange(ranges, editable: !vo, top, bottom, wavH, drawTopHLine: band == 0, drawBottomHLine: band == count - 1, texts);
            }
        }

        if (texts is not null)
        {
            _textLayer = texts.Count > 0 ? RenderText(texts, new Int32Rect(0, 0, _w, wavH)) : null;
            _textDirty = false;
        }

        if (_textLayer is not null)
        {
            BlendInto(_frame, _textLayer);
        }
    }

    /// <summary>
    /// PaintRange: as bordas vão de cima a baixo da faixa; os traços horizontais ficam a 1/10 da altura
    /// dela, o de cima só na primeira faixa e o de baixo só na última. A lista não editável (VO) usa
    /// RANGE_COLOR_NOT_EDITABLE.
    /// </summary>
    private void PaintRange(IReadOnlyList<WaveformRange> ranges, bool editable, int rangeTop, int rangeBottom, int wavH,
        bool drawTopHLine, bool drawBottomHLine, List<TextItem>? texts)
    {
        int div10 = (rangeBottom - rangeTop) / 10, y1 = rangeTop + div10, y2 = rangeBottom - div10;
        int viewStop = PositionMs + PageSizeMs;
        for (int i = 0; i < ranges.Count; i++)
        {
            var r = ranges[i];
            int x1 = r.StartMs >= PositionMs && r.StartMs <= viewStop ? TimeToPixel(r.StartMs - PositionMs) : -1;
            int x2 = r.StopMs >= PositionMs && r.StopMs <= viewStop ? TimeToPixel(r.StopMs - PositionMs) : -1;
            bool showStart = x1 != -1, showStop = x2 != -1 && x2 != x1;
            bool fullHLines = r.StartMs < PositionMs && r.StopMs > viewStop;
            int color = !editable ? WaveformPalette.RangeNotEditable : i % 2 == 0 ? WaveformPalette.Range1 : WaveformPalette.Range2;

            // A borda sob o mouse (seleção dinâmica) é desenhada com linha cheia.
            bool dynamic = editable && _mouse.DynamicRangeIndex == i;
            if (showStart)
            {
                bool solid = dynamic && _mouse.DynamicEditMode == DynamicEditMode.Start;
                VLine(_frame, x1, rangeTop, rangeBottom, color, dotted: !solid, 0, wavH);
            }

            if (showStop)
            {
                bool solid = dynamic && _mouse.DynamicEditMode == DynamicEditMode.Stop;
                VLine(_frame, x2, rangeTop, rangeBottom, color, dotted: !solid, 0, wavH);
            }

            if (fullHLines)
            {
                x1 = 0;
                x2 = _w - 1;
            }

            if (x1 == -1 && x2 == -1)
            {
                continue;
            }

            if (x1 == -1)
            {
                x1 = 0;
            }
            else if (x2 == -1)
            {
                x2 = _w - 1;
            }

            int pen = 2 * LineWidth;
            if (drawTopHLine)
            {
                HLine(_frame, x1, x2, y1 - (pen / 2), color, pen);
            }

            if (drawBottomHLine)
            {
                HLine(_frame, x1, x2, y2 - (pen / 2), color, pen);
            }

            if (texts is not null && ShowText && x2 - x1 > Px(10))
            {
                int m = Px(TextMargins96);
                var rect = new Int32Rect(x1 + m, y1 + m, x2 - x1 - (2 * m), y2 - y1 - (2 * m));
                // WAVDisplayer1CustomDrawRange: legenda com karaokê não tem o texto desenhado ("TODO : karaoke support").
                if (rect.Width > Px(MinimalTextSpace96) && rect.Height > 0 && r.Text.Length > 0 && r.SubTimes is not { Length: > 0 })
                {
                    // AlignBottom := (Rect.Top > WAVZoneHeight div 2), com WAVZoneHeight = Height - RulerHeight.
                    bool alignBottom = rect.Y > (_h - RulerHeight) / 2;
                    var typeface = new Typeface(TextFontFamily, TextFontStyle, TextFontWeight, FontStretches.Normal);
                    texts.Add(new TextItem(r.Text, rect.X, rect.Y, color, typeface, TextFontSize * _scale, rect, alignBottom));
                }
            }

            // Karaokê: os marcadores, pontilhados; o que está sob o mouse, cheio.
            if (r.SubTimes is { Length: > 0 } subTimes)
            {
                for (int j = 0; j < subTimes.Length; j++)
                {
                    int t = subTimes[j];
                    if (t >= PositionMs && t <= viewStop && t >= r.StartMs && t <= r.StopMs)
                    {
                        bool solid = dynamic && _mouse.DynamicEditMode == DynamicEditMode.Karaoke && _mouse.DynamicEditTime == j;
                        VLine(_frame, TimeToPixel(t - PositionMs), y1, y2, color, dotted: !solid, 0, wavH);
                    }
                }
            }
        }
    }

    /// <summary>PaintSelection: InvertRect na área da onda; seleção vazia vira uma linha em XOR.</summary>
    private void PaintSelection(int wavH)
    {
        int start = SelectionStartMs, stop = SelectionStopMs;
        if (stop <= 0)
        {
            return;
        }

        int x1 = TimeToPixel(start - PositionMs), x2 = TimeToPixel(stop - PositionMs);
        if (x1 == x2)
        {
            if (start >= PositionMs && start <= PositionMs + PageSizeMs)
            {
                XorVLine(x1, wavH, 0xFFFFFF, dotted: false);
            }

            return;
        }

        x1 = Math.Clamp(x1, 0, _w);
        x2 = Math.Clamp(x2, 0, _w);
        if (x1 == x2)
        {
            return;
        }

        int right = Math.Min(_w, x2 + 1);
        for (int y = 0; y < wavH; y++)
        {
            int row = y * _w;
            for (int x = x1; x < right; x++)
            {
                _frame[row + x] ^= 0xFFFFFF;
            }
        }
    }

    /// <summary>TMiniScrollBar: fundo cinza e polegar azul proporcional à página visível.</summary>
    private void PaintScrollBar()
    {
        int top = _h - ScrollBarHeight;
        FillRect(_frame, 0, top, _w, _h, WaveformPalette.ScrollBarBack);
        if (ThumbBounds() is var (left, width))
        {
            FillRect(_frame, left, top, left + width, _h,
                _thumbGrabOffset is null ? WaveformPalette.ScrollThumb : WaveformPalette.ScrollThumbDown);
        }
    }

    /// <summary>UpdateThumb, com Min = 0, Max = LengthMs, PageSize = PageSizeMs e Position = PositionMs.</summary>
    private (int Left, int Width)? ThumbBounds()
    {
        int length = LengthMs;
        if (length == 0)
        {
            return null; // FThumb.Visible := (FMax <> FMin)
        }

        int maxMin = length + 1;
        int page = Math.Clamp(PageSizeMs, 0, maxMin);
        int thumbMin = Px(ThumbMinSize96);
        int width = page == 0 ? thumbMin : Math.Max(thumbMin, MulDiv(page, _w, maxMin));
        int left = MulDiv(PositionMs, _w - width, maxMin - Math.Max(1, page));
        return (Math.Clamp(left, 0, Math.Max(0, _w - width)), width);
    }

    private static int MulDiv(int a, int b, int c) => c == 0 ? -1 : (int)Math.Round((long)a * b / (double)c, MidpointRounding.AwayFromZero);

    // ---------------------------------------------------------------- primitivas de pixel

    private void FillRect(int[] buffer, int x1, int y1, int x2, int y2, int color)
    {
        x1 = Math.Clamp(x1, 0, _w);
        x2 = Math.Clamp(x2, 0, _w);
        y1 = Math.Clamp(y1, 0, _h);
        y2 = Math.Clamp(y2, 0, _h);
        for (int y = y1; y < y2; y++)
        {
            Array.Fill(buffer, color, (y * _w) + x1, x2 - x1);
        }
    }

    /// <summary>LineTo horizontal: de x1 até x2 - 1, com a espessura dada a partir de y.</summary>
    private void HLine(int[] buffer, int x1, int x2, int y, int color, int thickness) =>
        FillRect(buffer, Math.Min(x1, x2), y, Math.Max(x1, x2), y + thickness, color);

    /// <summary>
    /// MoveTo/LineTo vertical (o último pixel não é pintado). Pontilhado como o psDot do GDI:
    /// 3 pixels acesos e 3 apagados, escalados pelo DPI.
    /// </summary>
    private void VLine(int[] buffer, int x, int y1, int y2, int color, bool dotted, int clipTop, int clipBottom)
    {
        int lw = LineWidth;
        int from = Math.Max(Math.Min(y1, y2), clipTop), to = Math.Min(Math.Max(y1, y2), clipBottom);
        int dot = 3 * lw;
        for (int px = x; px < x + lw; px++)
        {
            if (px < 0 || px >= _w)
            {
                continue;
            }

            for (int y = from; y < to; y++)
            {
                if (!dotted || ((y - y1) / dot) % 2 == 0)
                {
                    buffer[(y * _w) + px] = color;
                }
            }
        }
    }

    private void XorVLine(int x, int wavH, int color, bool dotted)
    {
        int lw = LineWidth, dot = 3 * lw;
        for (int px = x; px < x + lw; px++)
        {
            if (px < 0 || px >= _w)
            {
                continue;
            }

            for (int y = 0; y < wavH; y++)
            {
                if (!dotted || (y / dot) % 2 == 0)
                {
                    _frame[(y * _w) + px] ^= color;
                }
            }
        }
    }

    // ---------------------------------------------------------------- texto

    private sealed record TextItem(string Text, double X, double Y, int Color, Typeface Typeface, double Size, Int32Rect? Clip, bool AlignBottom = false);

    private static double MeasureWidth(string text, Typeface typeface, double size) =>
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, Brushes.White, 1.0).WidthIncludingTrailingWhitespace;

    /// <summary>
    /// Renderiza os textos numa camada transparente (Pbgra32) do tamanho do controle. Textos com
    /// retângulo imitam DrawTextW com DT_END_ELLIPSIS e DT_EDITCONTROL: uma linha por quebra, cortada
    /// com reticências e limitada ao retângulo.
    /// </summary>
    private int[] RenderText(List<TextItem> items, Int32Rect area)
    {
        var visual = new DrawingVisual();
        TextOptions.SetTextFormattingMode(visual, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);
        using (var dc = visual.RenderOpen())
        {
            dc.PushClip(new RectangleGeometry(new Rect(area.X, area.Y, area.Width, area.Height)));
            foreach (var item in items)
            {
                var brush = new SolidColorBrush(Color.FromRgb((byte)(item.Color >> 16), (byte)(item.Color >> 8), (byte)item.Color));
                if (item.Clip is not { } clip)
                {
                    dc.DrawText(Format(item.Text, item, brush), new Point(item.X, item.Y));
                    continue;
                }

                dc.PushClip(new RectangleGeometry(new Rect(clip.X, clip.Y, clip.Width, clip.Height)));
                double y = clip.Y;
                string[] lines = item.Text.Split("\r\n");
                if (item.AlignBottom)
                {
                    // WideCanvasDrawText: DT_CALCRECT mede o texto e, se sobra espaço, ele desce até o fundo.
                    double total = lines.Sum(l => Format(l, item, brush).Height);
                    y += Math.Max(0, clip.Height - total);
                }

                foreach (string line in lines)
                {
                    var ft = Format(line, item, brush);
                    ft.MaxTextWidth = clip.Width;
                    ft.MaxLineCount = 1;
                    ft.Trimming = TextTrimming.CharacterEllipsis;
                    dc.DrawText(ft, new Point(clip.X, y));
                    y += ft.Height;
                    if (y >= clip.Y + clip.Height)
                    {
                        break;
                    }
                }

                dc.Pop();
            }

            dc.Pop();
        }

        var rtb = new RenderTargetBitmap(_w, _h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        int[] pixels = new int[_w * _h];
        rtb.CopyPixels(pixels, _w * 4, 0);
        return pixels;

        static FormattedText Format(string text, TextItem item, Brush brush) =>
            new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, item.Typeface, item.Size, brush, 1.0);
    }

    /// <summary>Mistura uma camada pré-multiplicada sobre o buffer opaco.</summary>
    private static void BlendInto(int[] target, int[] layer)
    {
        for (int i = 0; i < target.Length; i++)
        {
            uint s = (uint)layer[i];
            uint a = s >> 24;
            if (a == 0)
            {
                continue;
            }

            if (a == 255)
            {
                target[i] = (int)(s & 0xFFFFFF);
                continue;
            }

            uint d = (uint)target[i], inv = 255 - a;
            uint r = ((s >> 16) & 0xFF) + ((((d >> 16) & 0xFF) * inv) / 255);
            uint g = ((s >> 8) & 0xFF) + ((((d >> 8) & 0xFF) * inv) / 255);
            uint b = (s & 0xFF) + (((d & 0xFF) * inv) / 255);
            target[i] = (int)((Math.Min(r, 255u) << 16) | (Math.Min(g, 255u) << 8) | Math.Min(b, 255u));
        }
    }

    // ---------------------------------------------------------------- mouse

    private int DevX(MouseEventArgs e) => (int)Math.Round(e.GetPosition(this).X * _scale);

    private int DevY(MouseEventArgs e) => (int)Math.Round(e.GetPosition(this).Y * _scale);

    /// <summary>Botões e teclas no formato de TShiftState.</summary>
    private static WaveformInput Input(MouseEventArgs e)
    {
        var input = WaveformInput.None;
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            input |= WaveformInput.Left;
        }

        if (e.RightButton == MouseButtonState.Pressed)
        {
            input |= WaveformInput.Right;
        }

        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            input |= WaveformInput.Middle;
        }

        var mods = Keyboard.Modifiers;
        if (mods.HasFlag(ModifierKeys.Shift))
        {
            input |= WaveformInput.Shift;
        }

        if (mods.HasFlag(ModifierKeys.Control))
        {
            input |= WaveformInput.Ctrl;
        }

        return input;
    }

    /// <summary>Passa a vista atual ao <see cref="WaveformMouse"/> antes de cada evento.</summary>
    private void PrepareMouse()
    {
        _mouse.PositionMs = PositionMs;
        _mouse.PageSizeMs = PageSizeMs;
        _mouse.LengthMs = LengthMs;
        _mouse.WidthPx = _w;
        _mouse.WavHeightPx = WavHeight;
        _mouse.Scale = _scale;
        _mouse.SelectionMode = SelectionMode;
        _mouse.SnappingEnabled = SnappingEnabled;
        _mouse.AntiOverlapping = AntiOverlapping;
        _mouse.MinimumBlankMs = MinimumBlankMs;
        _mouse.SceneChanges = SceneChanges ?? [];
        _mouse.SceneChangeEnabled = SceneChangeEnabled;
        _mouse.SceneChangeStartOffset = SceneChangeStartOffset;
        _mouse.SceneChangeStopOffset = SceneChangeStopOffset;
        _mouse.SceneChangeFilterOffset = SceneChangeFilterOffset;
    }

    /// <summary>Copia cursor e seleção do <see cref="WaveformMouse"/> para as propriedades ligadas à janela.</summary>
    private void PushState()
    {
        _syncing = true;
        try
        {
            CursorMs = _mouse.CursorMs;
            SelectionStartMs = _mouse.SelectionStartMs;
            SelectionStopMs = _mouse.SelectionStopMs;
        }
        finally
        {
            _syncing = false;
        }

        InvalidateVisual();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseDown(e);
        Focus();
        if (LengthMs == 0)
        {
            return;
        }

        int x = DevX(e), y = DevY(e);
        if (y >= _h - ScrollBarHeight)
        {
            ScrollBarMouseDown(x, e.ChangedButton == MouseButton.Left);
            CaptureMouse();
            e.Handled = true;
            return;
        }

        PrepareMouse();
        if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
        {
            // MouseDown sai com ssDouble; o VCL chama DblClick em seguida.
            _mouse.DoubleClick(y);
        }
        else
        {
            _mouse.MouseDown(Input(e), x, y);
            if (_mouse.IsScrolling)
            {
                Cursor = Cursors.Hand;
            }
        }

        PushState();
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseMove(e);
        if (_thumbGrabOffset is int grab && ThumbBounds() is var (_, width))
        {
            // OnThumbMouseMove: posição proporcional ao deslocamento do polegar.
            int newX = Math.Clamp(DevX(e) - grab, 0, Math.Max(0, _w - width));
            int page = Math.Clamp(PageSizeMs, 0, LengthMs + 1);
            int pos = MulDiv(newX, LengthMs + 1 - Math.Max(1, page), Math.Max(1, _w - width));
            UserScrolled?.Invoke(this, EventArgs.Empty);
            SetPosition(pos);
            return;
        }

        if (_scrollRepeatTimer.IsEnabled)
        {
            _lastScrollBarX = DevX(e);
            return;
        }

        int y = DevY(e);
        bool inScrollBar = !_mouse.MouseIsDown && y >= _h - ScrollBarHeight;
        if (inScrollBar)
        {
            // A barra de rolagem é outro controle no original: o mouse sobre ela não mexe na onda.
            Cursor = Cursors.Arrow;
            return;
        }

        PrepareMouse();
        var oldMode = _mouse.DynamicEditMode;
        int oldRange = _mouse.DynamicRangeIndex;
        var (oldBlank1, oldBlank2) = (_mouse.MinBlank1, _mouse.MinBlank2);
        _mouse.MouseMove(Input(e), DevX(e), y);
        if (!_mouse.MouseIsDown)
        {
            Cursor = _mouse.DynamicEditMode == DynamicEditMode.None ? Cursors.IBeam : HSplitCursor;
        }

        if (oldMode != _mouse.DynamicEditMode || oldRange != _mouse.DynamicRangeIndex
            || oldBlank1 != _mouse.MinBlank1 || oldBlank2 != _mouse.MinBlank2)
        {
            InvalidateVisual();
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseUp(e);
        _scrollRepeatTimer.Stop();
        if (_thumbGrabOffset is not null)
        {
            _thumbGrabOffset = null;
            InvalidateVisual();
        }
        else if (_mouse.MouseIsDown || e.ClickCount == 2)
        {
            _mouse.MouseUp();
            Cursor = Cursors.IBeam;
            PushState();
        }

        if (Mouse.LeftButton != MouseButtonState.Pressed && Mouse.RightButton != MouseButtonState.Pressed
            && Mouse.MiddleButton != MouseButtonState.Pressed)
        {
            ReleaseMouseCapture();
        }
    }

    /// <summary>DoContextPopup: no modo SSA o botão direito marca o fim da seleção; o menu só abre com Shift.</summary>
    protected override void OnContextMenuOpening(ContextMenuEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnContextMenuOpening(e);
        if (SelectionMode == WaveformSelectionMode.Ssa && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
        }
    }

    /// <summary>Windows.SetCursorPos sobre a borda que vai ser arrastada.</summary>
    private void MoveMouseTo(int x)
    {
        var screen = PointToScreen(new Point(x / _scale, Mouse.GetPosition(this).Y));
        SetCursorPos((int)Math.Round(screen.X), (int)Math.Round(screen.Y));
    }

    /// <summary>ClipCursor: prende o mouse entre os X dados (pixels do controle) ou o libera.</summary>
    private void ClipMouse((int Left, int Right)? clip)
    {
        if (clip is not { } c)
        {
            ClipCursor(0);
            return;
        }

        var topLeft = PointToScreen(new Point(c.Left / _scale, 0));
        var bottomRight = PointToScreen(new Point(c.Right / _scale, ActualHeight));
        var rect = new NativeRect
        {
            Left = (int)Math.Round(topLeft.X),
            Top = (int)Math.Round(topLeft.Y),
            Right = (int)Math.Round(bottomRight.X),
            Bottom = (int)Math.Round(bottomRight.Y),
        };
        ClipCursor(ref rect);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClipCursor(ref NativeRect rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClipCursor(nint rect);

    /// <summary>
    /// TMiniScrollBar.MouseDown: fora do polegar pagina (com qualquer botão) e liga a repetição; sobre ele,
    /// o botão esquerdo começa o arraste.
    /// </summary>
    private void ScrollBarMouseDown(int x, bool leftButton)
    {
        if (ThumbBounds() is not var (left, width))
        {
            return;
        }

        if (x >= left && x <= left + width)
        {
            if (leftButton)
            {
                _thumbGrabOffset = x - left;
                InvalidateVisual();
            }
        }
        else
        {
            PageScrollBar(x > left);
            _lastScrollBarX = x;
            _scrollRepeatTimer.Start();
        }
    }

    /// <summary>OnRepeatTimer: para quando o mouse chega ao polegar.</summary>
    private void ScrollBarRepeat()
    {
        if (ThumbBounds() is not var (left, width) || (_lastScrollBarX >= left && _lastScrollBarX <= left + width))
        {
            _scrollRepeatTimer.Stop();
            return;
        }

        PageScrollBar(_lastScrollBarX > left);
    }

    private void PageScrollBar(bool forward)
    {
        UserScrolled?.Invoke(this, EventArgs.Empty);
        SetPosition(PositionMs + (forward ? PageSizeMs : -PageSizeMs));
    }

    private static Cursor LoadHSplitCursor()
    {
        using var stream = typeof(WaveformControl).Assembly.GetManifestResourceStream("VisualSubSync.Waveform.hsplit.cur");
        return stream is null ? Cursors.SizeWE : new Cursor(stream, scaleWithDpi: true);
    }

    /// <summary>SetPositionMs: limitado a [0, LengthMs - PageSizeMs].</summary>
    private void SetPosition(int position) => PositionMs = Math.Clamp(position, 0, Math.Max(0, LengthMs - PageSizeMs));

    /// <summary>ZoomRange: página mínima de 100 ms, centrada quando menor que isso.</summary>
    public void ZoomRange(int startMs, int stopMs)
    {
        if (startMs >= stopMs)
        {
            return;
        }

        int page = stopMs - startMs, position;
        if (page < 100)
        {
            page = 100;
            position = startMs + ((stopMs - startMs - page) / 2);
        }
        else
        {
            page = Math.Clamp(page, 0, LengthMs);
            position = startMs;
        }

        PageSizeMs = page;
        PositionMs = Math.Clamp(position, 0, Math.Max(0, LengthMs - page));
    }

    /// <summary>ZoomAndSelectRange: mostra ao menos 10 s, com 20% de folga, e seleciona a legenda.</summary>
    public void ZoomAndSelectRange(int startMs, int stopMs)
    {
        int len = stopMs - startMs;
        int display = (int)Math.Round(Math.Max(len, 10_000) * 1.2, MidpointRounding.ToEven);
        SelectRange(startMs, stopMs);
        ZoomRange(startMs + (len / 2) - (display / 2), startMs + (len / 2) + (display / 2));
    }

    /// <summary>
    /// SelectedRange := Range (FocusNode com WAVDisplaySelect): prende a seleção à legenda com esses
    /// tempos. Sem legenda correspondente, só seleciona o trecho.
    /// </summary>
    public void SelectRange(int startMs, int stopMs)
    {
        int index = _mouse.FindInsertPos(startMs, stopMs);
        if (index < _mouse.Ranges.Count && _mouse.Ranges[index].StartMs == startMs && _mouse.Ranges[index].StopMs == stopMs)
        {
            _mouse.SetSelectedRange(index);
        }
        else
        {
            _mouse.SetSelectedRange(WaveformMouse.NoRange);
            _mouse.SetSelection(startMs, stopMs);
        }

        PushState();
    }

    /// <summary>ClearSelection + Selection.StartTime/StopTime (TMainForm.SetSelection): um trecho solto.</summary>
    public void SetSelection(int startMs, int stopMs)
    {
        _mouse.SetSelectedRange(WaveformMouse.NoRange);
        _mouse.SetSelection(startMs, stopMs);
        PushState();
    }

    /// <summary>ClearSelection: solta a legenda e zera a seleção.</summary>
    public void ClearSelection()
    {
        _mouse.ClearSelection();
        PushState();
    }

    /// <summary>Scroll(ViewPercent): desloca a vista em uma porcentagem da página.</summary>
    public void Scroll(int viewPercent) => SetPosition(PositionMs + (int)Math.Round(PageSizeMs / 100.0 * viewPercent));

    /// <summary>ZoomIn: o terço central da página.</summary>
    public void ZoomIn() => ZoomRange(
        PositionMs + (int)Math.Round(PageSizeMs / 3.0, MidpointRounding.ToEven),
        PositionMs + (int)Math.Round(PageSizeMs / 3.0 * 2, MidpointRounding.ToEven));

    /// <summary>ZoomOut: três páginas centradas na atual.</summary>
    public void ZoomOut() => ZoomRange(PositionMs - PageSizeMs, PositionMs + (PageSizeMs * 2));

    /// <summary>ActionZoomSelectionExecute: ZoomRange(Selection).</summary>
    public void ZoomSelection() => ZoomRange(SelectionStartMs, SelectionStopMs);

    /// <summary>ZoomAll: do início ao fim.</summary>
    public void ZoomAll() => ZoomRange(0, LengthMs);

    /// <summary>WheelTimeScroll: a tecla que, com a roda, rola 1/4 da página.</summary>
    public WheelModifier WheelTimeScroll { get; set; } = WheelModifier.None;

    /// <summary>WheelVZoom: zoom vertical de 5 em 5.</summary>
    public WheelModifier WheelVZoom { get; set; } = WheelModifier.Shift;

    /// <summary>WheelHZoom: zoom horizontal (80%/125%) centrado no mouse.</summary>
    public WheelModifier WheelHZoom { get; set; } = WheelModifier.Ctrl;

    /// <summary>DoMouseWheelUp / DoMouseWheelDown, com as teclas das preferências (aba Mouse).</summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseWheel(e);
        e.Handled = true;
        if (LengthMs == 0 || e.Delta == 0)
        {
            return;
        }

        bool up = e.Delta > 0;
        var mods = Keyboard.Modifiers;
        var action = WheelActions.Resolve(WheelHZoom, WheelVZoom, WheelTimeScroll,
            mods.HasFlag(ModifierKeys.Shift), mods.HasFlag(ModifierKeys.Alt), mods.HasFlag(ModifierKeys.Control));
        if (action == WheelAction.HorizontalZoom)
        {
            int x = DevX(e);
            int newPage = (int)Math.Round(PageSizeMs * (up ? 80 : 125) / 100.0, MidpointRounding.ToEven);
            int mouseTime = PixelToTime(x) + PositionMs;
            int newMouse = (int)Math.Round(x * (newPage / (double)_w), MidpointRounding.ToEven);
            ZoomRange(mouseTime - newMouse, mouseTime + (newPage - newMouse));
        }
        else if (action == WheelAction.VerticalZoom)
        {
            int v = VerticalScaling;
            if (up && v is >= 0 and <= 395)
            {
                VerticalScaling = v + 5;
            }
            else if (!up && v is >= 5 and <= 400)
            {
                VerticalScaling = v - 5;
            }
        }
        else if (action == WheelAction.TimeScroll)
        {
            int amount = Math.Max(1, (int)Math.Round(PageSizeMs / 4.0, MidpointRounding.ToEven));
            SetPosition(PositionMs + (up ? -amount : amount));
        }
    }

    // ---------------------------------------------------------------- propriedades

    /// <summary>Mantém o <see cref="WaveformMouse"/> igual ao que a janela definiu (legendas, cursor, seleção).</summary>
    private void OnStateChanged(DependencyProperty property)
    {
        if (_syncing)
        {
            return;
        }

        if (property == RangesProperty)
        {
            _mouse.SetRanges(Ranges?.Select(r => new TimeRange(r.StartMs, r.StopMs) { SubTimes = r.SubTimes is null ? [] : [.. r.SubTimes] }) ?? []);
        }
        else if (property == VoRangesProperty)
        {
            _mouse.SetVoRanges(VoRanges?.Select(r => new TimeRange(r.StartMs, r.StopMs)) ?? []);
        }
        else if (property == ShowVOProperty)
        {
            _mouse.ShowVO = ShowVO;
        }
        else if (property == CursorMsProperty)
        {
            _mouse.LengthMs = LengthMs;
            _mouse.SetCursor(CursorMs);
        }
        else if (property == SelectionStartMsProperty || property == SelectionStopMsProperty)
        {
            _mouse.SetSelection(SelectionStartMs, SelectionStopMs);
        }
        else if (property == LengthMsProperty && LengthMs == 0)
        {
            _mouse.SetSelectedRange(WaveformMouse.NoRange);
        }
    }

    private enum Layer
    {
        Base,
        Text,
        Overlay,
    }

    private static DependencyProperty Register<T>(string name, T defaultValue, Layer layer) =>
        DependencyProperty.Register(name, typeof(T), typeof(WaveformControl),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, e) =>
                {
                    var control = (WaveformControl)d;
                    control.OnStateChanged(e.Property);
                    if (layer == Layer.Base)
                    {
                        control._baseDirty = true;
                        control._textDirty = true;
                    }
                    else if (layer == Layer.Text)
                    {
                        control._textDirty = true;
                    }

                    control.InvalidateVisual();
                }));
}

/// <summary>Legenda desenhada sobre a onda, com os marcadores de karaokê (SubTime).</summary>
public readonly record struct WaveformRange(int StartMs, int StopMs, string Text, int[]? SubTimes = null);

public sealed class WaveformRangeEventArgs(int index, bool isDynamic) : EventArgs
{
    /// <summary>Posição da legenda em <see cref="WaveformControl.Ranges"/>.</summary>
    public int Index { get; } = index;

    /// <summary>Verdadeiro quando veio da edição pela borda (não mostra o quadro inicial).</summary>
    public bool IsDynamic { get; } = isDynamic;
}

public sealed class WaveformRangeEdgeEventArgs(int index, bool isStart) : EventArgs
{
    /// <summary>Posição da legenda em <see cref="WaveformControl.Ranges"/>.</summary>
    public int Index { get; } = index;

    /// <summary>Verdadeiro na borda inicial, falso na final.</summary>
    public bool IsStart { get; } = isStart;
}

public sealed class WaveformRangeTimesEventArgs(RangeTimesChange change) : EventArgs
{
    public RangeTimesChange Change { get; } = change;
}

/// <summary>Operações da forma de onda que a janela principal aciona pelos comandos de zoom.</summary>
public interface IWaveformView
{
    /// <summary>Playback must not move the viewport during mouse selection or scrollbar dragging.</summary>
    bool IsMouseInteracting { get; }
    void ZoomIn();

    void ZoomOut();

    void ZoomSelection();

    void ZoomAll();

    void ZoomRange(int startMs, int stopMs);

    void ZoomAndSelectRange(int startMs, int stopMs);

    void SelectRange(int startMs, int stopMs);

    void ClearSelection();

    /// <summary>Seleciona um trecho sem prendê-lo a uma legenda.</summary>
    void SetSelection(int startMs, int stopMs);

    void Scroll(int viewPercent);

    /// <summary>Legenda presa à seleção, ou -1.</summary>
    int SelectedRangeIndex { get; }

    /// <summary>KaraokeSelectedIndex: o trecho de karaokê selecionado, ou -1.</summary>
    int KaraokeSelectedIndex { get; }

    /// <summary>KaraokeSelectedRange: a legenda desse trecho, ou -1.</summary>
    int KaraokeSelectedRangeIndex { get; }

    void SelectNextKaraoke();

    void SelectPreviousKaraoke();
}
