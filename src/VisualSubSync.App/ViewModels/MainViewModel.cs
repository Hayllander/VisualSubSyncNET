using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.App.Services;
using VisualSubSync.Core.Audio;
using VisualSubSync.Core.Projects;
using VisualSubSync.Core.Scenes;
using VisualSubSync.Core.Settings;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Subtitles.Formats;
using VisualSubSync.Core.TextPipe;
using VisualSubSync.Core.Timing;
using VisualSubSync.Core.Waveform;
using VisualSubSync.Media;
using VisualSubSync.Media.FFmpeg;
using VisualSubSync.Media.Mpv;
using VisualSubSync.Waveform;

namespace VisualSubSync.App.ViewModels;

/// <summary>Estado e ações da janela principal (TMainForm).</summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    public const string ApplicationName = "VisualSubSyncNET";

    private const string ProjectFilter = "VSS project (*.vssprj)|*.VSSPRJ|All files (*.*)|*.*";

    // Mesma ordem do TntSaveDialog1 em ActionSaveAsExecute: o índice decide a extensão.
    private const string SaveAsFilter =
        "SRT files (*.srt)|*.srt|SRT files stripped of text (*.srt)|*.srt|SSA files (*.ssa)|*.ssa|" +
        "ASS files (*.ass)|*.ass|CUE files (*.cue)|*.cue|TXT files (*.txt)|*.txt|CSV files (*.csv)|*.csv|All files (*.*)|*.*";

    private static readonly string[] SaveAsExtensions = ["", ".srt", ".srt", ".ssa", ".ass", ".cue", ".txt", ".csv"];

    private const string CharsetWarning =
        "Subtitles contain some characters that may not be saved correctly, the file will be saved in Unicode UTF-8 starting from now.";

    /// <summary>StartEndPlayingDuration: trecho tocado por "Play/Loop selection start/end".</summary>
    private const int StartEndPlayingDuration = 500;

    private readonly IDialogService _dialogs;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer _statusTimer;
    private readonly DispatcherTimer _playTimer;
    private SubtitleDocument _document = new();
    private (int Line, int Column, int FirstVisible) _caret;
    // IMediaEngine é o contrato trocável (princípio do plano); o motor concreto é detalhe de AttachMedia.
#pragma warning disable CA1859
    private IMediaEngine? _engine;
#pragma warning restore CA1859

    /// <summary>TPlayingModeType: o que a reprodução atual toca, para UpdatePlayRange.</summary>
    private enum PlayingModeType
    {
        All,
        Selection,
        SelectionStart,
        SelectionEnd,
    }

    private PlayingModeType _playingMode;

    public MainViewModel(IDialogService dialogs, VssSettings settings)
    {
        _dialogs = dialogs;
        Settings = settings;
        _volume = settings.Volume;
        _showTextInWavDisplay = settings.ShowTextInWavDisplay;
        _autoScrollWavDisplay = settings.AutoScrollWavDisplay;
        _autoScrollSubtitles = settings.AutoScrollSubtitles;
        _showSceneChange = settings.ShowSceneChange;
        _isTextPipeVisible = settings.ShowTextPipe;
        _textPipeAutoOption = Enum.IsDefined((TextPipeAutoOption)settings.TextPipeAutoOption)
            ? (TextPipeAutoOption)settings.TextPipeAutoOption
            : TextPipeAutoOption.Colorize;

        // TimerStatusBarMsg: a mensagem some depois de 4 s (padrão de ShowStatusBarMessage).
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(4000) };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            _statusBarSecondaryText = string.Empty;
            UpdateStatusBar();
        };

        // FUpdateCursorTimer: 40 ms, o cursor de reprodução segue a posição do motor.
        dispatcher = Dispatcher.CurrentDispatcher;
        _playTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _playTimer.Tick += (_, _) => OnPlayTimerTick();

        _undoStack.Changed += (_, _) => OnUndoStackChanged();
        ApplyAutoScrollSubtitles(_autoScrollSubtitles);
        UpdateEditorStatus(0, 0, 0);
        ApplyAutoBackupSettings();
        StartSpellChecker();
    }

    public VssSettings Settings { get; }

    /// <summary>Ligado pela janela: a forma de onda executa os zooms (TWAVDisplayer.Zoom*).</summary>
    public IWaveformView? Waveform { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle), nameof(IsProjectOpen), nameof(IsSubtitleEditionEnabled))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand), nameof(CloseCommand), nameof(ZoomInCommand), nameof(ZoomOutCommand),
        nameof(ZoomSelectionCommand), nameof(ZoomAllCommand), nameof(NextSubCommand), nameof(PreviousSubCommand),
        nameof(ShowHideVideoCommand), nameof(ProjectPropertiesCommand), nameof(ShowHideSceneChangeCommand),
        nameof(ClearSelectionCommand), nameof(SetFocusedSubtitleTimeCommand), nameof(SetSubtitleStartTimeCommand),
        nameof(SetSubtitleStopTimeCommand), nameof(SetSubtitleStopTimeAndGoToNextCommand), nameof(ShiftStartTimeCommand),
        nameof(ShiftStopTimeCommand), nameof(SelectNextSubCommand), nameof(SelectPreviousSubCommand),
        nameof(WavDisplayScrollRightCommand), nameof(WavDisplayScrollLeftCommand), nameof(AddSubtitleCommand),
        nameof(StartSubCommand), nameof(StopSubCommand), nameof(StopAndStartSubCommand))]
    private VssProject? _project;

    private bool _isDirty;

    [ObservableProperty]
    private bool _showStyleColumn;

    [ObservableProperty]
    private SubtitleItemViewModel? _selectedSubtitle;

    [ObservableProperty]
    private PeakData? _peaks;

    [ObservableProperty]
    private IReadOnlyList<WaveformRange>? _ranges;

    [ObservableProperty]
    private int _lengthMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViewBeginText), nameof(ViewEndText))]
    private int _positionMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViewEndText), nameof(ViewLengthText))]
    private int _pageSizeMs;

    [ObservableProperty]
    private int _verticalScaling = 100;

    [ObservableProperty]
    private int _cursorMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelBeginText), nameof(SelLengthText))]
    private int _selectionStartMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelEndText), nameof(SelLengthText))]
    private int _selectionStopMs;

    [ObservableProperty]
    private bool _showTextInWavDisplay;

    /// <summary>FSceneChangeList do WAVDisplayer: quadros-chave do vídeo (arquivo .scenechange).</summary>
    [ObservableProperty]
    private IReadOnlyList<int>? _sceneChanges;

    /// <summary>SceneChangeEnabled = ShowSceneChange (MenuItemShowHidesSeneChange).</summary>
    [ObservableProperty]
    private bool _showSceneChange;

    [ObservableProperty]
    private bool _autoScrollWavDisplay;

    [ObservableProperty]
    private bool _autoScrollSubtitles;

    [ObservableProperty]
    private int _volume;

    /// <summary>MemoLinesCounter.</summary>
    [ObservableProperty]
    private string _linesCounterText = "0";

    /// <summary>StatusBarPanel1: linha, coluna, total e caracteres por segundo.</summary>
    [ObservableProperty]
    private string _editorStatusText = string.Empty;

    /// <summary>StatusBarPanel2: mensagens temporárias (ShowStatusBarMessage).</summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>Motor com um arquivo aberto: equivale a VideoRenderer.IsOpen or AudioOnlyRenderer.IsOpen.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand), nameof(StopCommand), nameof(PauseCommand), nameof(LoopCommand),
        nameof(PlayToEndCommand), nameof(PlaySelectionStartCommand), nameof(PlaySelectionEndCommand),
        nameof(LoopSelectionStartCommand), nameof(LoopSelectionEndCommand), nameof(SetPlaybackRateCommand),
        nameof(ToggleTimingModeCommand), nameof(Play1sBeforeCommand), nameof(Play1sBeforeToEndCommand))]
    private bool _isMediaOpen;

    /// <summary>VideoRenderer.IsOpen: habilita "Show frame at cursor/start/stop".</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShowFrameAtCursorCommand), nameof(ShowStartFrameCommand), nameof(ShowStopFrameCommand))]
    private bool _isVideoOpen;

    /// <summary>ShowingVideo (MenuItemShowHideVideo).</summary>
    [ObservableProperty]
    private bool _showingVideo;

    /// <summary>WAVDisplayer.IsPlaying: mantém o cursor de reprodução visível na onda.</summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>FPlayCursorMs: posição do cursor de reprodução (amarelo), distinto do cursor de edição.</summary>
    [ObservableProperty]
    private int _playCursorMs;

    /// <summary>Largura salva do painel de vídeo (CurrentProject.VideoPanelWidth); 0 = calcular pela proporção.</summary>
    [ObservableProperty]
    private int _videoPanelWidth;

    /// <summary>Proporção do vídeo (largura/altura); 0 enquanto o tamanho não é conhecido.</summary>
    [ObservableProperty]
    private double _videoAspectRatio;

    public ObservableCollection<SubtitleItemViewModel> Subtitles { get; } = [];

    public bool IsProjectOpen => Project is not null;

    /// <summary>CurrentProjectOnDirtyChange: "VisualSubSync - projeto.vssprj" e "*" quando há alterações.</summary>
    public string WindowTitle => Project is null
        ? ApplicationName
        : $"{ApplicationName} - {Path.GetFileName(Project.FileName)}{(IsDirty ? "*" : string.Empty)}";

    public string SelBeginText => IsProjectOpen ? TimeFormat.ToVss(SelectionStartMs) : string.Empty;

    public string SelEndText => IsProjectOpen ? TimeFormat.ToVss(SelectionStopMs) : string.Empty;

    public string SelLengthText => IsProjectOpen ? TimeFormat.ToVss(SelectionStopMs - SelectionStartMs) : string.Empty;

    public string ViewBeginText => IsProjectOpen ? TimeFormat.ToVss(PositionMs) : string.Empty;

    public string ViewEndText => IsProjectOpen ? TimeFormat.ToVss(PositionMs + PageSizeMs) : string.Empty;

    public string ViewLengthText => IsProjectOpen ? TimeFormat.ToVss(PageSizeMs) : string.Empty;

    /// <summary>
    /// plCursorPos: o cursor de edição quando parado (WAVDisplayer1CursorChange) e o cursor de reprodução
    /// a cada passo do timer (WAVDisplayer1PlayCursorChange).
    /// </summary>
    [ObservableProperty]
    private string _cursorText = TimeFormat.ToVss(0);

    partial void OnCursorMsChanged(int value)
    {
        if (!IsPlaying)
        {
            CursorText = TimeFormat.ToVss(value);
        }
    }

    partial void OnPlayCursorMsChanged(int value)
    {
        if (IsPlaying)
        {
            CursorText = TimeFormat.ToVss(value);
        }
    }

    /// <summary>Pasta Backup ao lado do executável (g_BackupDirectory).</summary>
    private static string BackupDirectory => StoragePaths.ResolveBackupDirectory(AppContext.BaseDirectory);

    private bool HasProject => Project is not null;

    private bool CanSave => Project is not null && IsDirty;

    // ---------------------------------------------------------------- arquivo

    [RelayCommand]
    private async Task OpenProject()
    {
        if (_dialogs.OpenFile(ProjectFilter) is { } path)
        {
            await LoadProject(path).ConfigureAwait(true);
        }
    }

    /// <summary>ActionNewProjectExecute: fecha o projeto, recolhe os dados e cria o .vssprj.</summary>
    [RelayCommand]
    private async Task NewProject()
    {
        if (!CheckSubtitlesAreSaved())
        {
            return;
        }

        CloseProject();

        if (_dialogs.ShowProjectForm(new ProjectFormData { NewProject = true }) is not { } form)
        {
            return;
        }

        // ActionNewProjectExecute grava o .vssprj a partir do TProjectForm; os padrões de vídeo e
        // dicionário vêm do INI (StartupDetachVideo/StartupShowVideo/StartupDictionnary).
        var project = new VssProject
        {
            FileName = form.ProjectFile,
            VideoSource = form.VideoSource,
            WavFile = form.WavFile,
            PeakFile = form.PeakFile,
            WavMode = form.WavMode,
            SubtitlesFile = form.SubtitlesFile,
            SubtitlesVO = form.SubtitlesVO,
            DetachedVideo = Settings.DetachedVideo,
            ShowVideo = Settings.ShowVideo,
            ShowVO = File.Exists(form.SubtitlesVO),
            Dictionary = Settings.Dictionary,
        };

        try
        {
            VssProjectFile.Save(project, form.ProjectFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Could not save \"{form.ProjectFile}\".\n{ex.Message}");
            return;
        }

        await LoadProject(form.ProjectFile).ConfigureAwait(true);
        // IsUTF8 fica no documento (no legado, CurrentProject.IsUTF8 depois de LoadProject).
        _document.IsUtf8 = form.SaveAsUtf8;
    }

    /// <summary>TMainForm.LoadProject: forma de onda, legendas, VO, áudio, vídeo e script.</summary>
    public async Task LoadProject(string path)
    {
        if (!File.Exists(path))
        {
            _dialogs.ShowError($"The project file {path} doesn't exist.");
            return;
        }

        if (!CheckSubtitlesAreSaved())
        {
            return;
        }

        CloseProject();
        // TCursorManager(crHourGlass) durante todo o LoadProject.
        using var waitCursor = _dialogs.WaitCursor();
        try
        {
            var project = VssProjectFile.Load(path);
            ShowStatusBarMessage("Loading waveform...");
            var peaks = LoadPeaks(project, out bool projectChanged);

            ShowStatusBarMessage("Loading subtitles...");
            _document = LoadDocument(project.SubtitlesFile);
            LoadSubtitles(_document);
            ShowStyleColumn = SubtitleFile.HasStyles(project.SubtitlesFile);

            Peaks = peaks;
            Project = project;

            // ----- Load VO
            ShowStatusBarMessage("Loading reference VO...");
            LoadVO(project.SubtitlesVO);
            VideoPanelWidth = project.VideoPanelWidth;

            // ----- Load audio/video (AudioOnlyRenderer + VideoRenderer no original) ---
            await OpenMediaAsync(project).ConfigureAwait(true);

            // Sem forma de onda: a duração do vídeo; senão última legenda + 1 minuto, ou 5 minutos.
            UpdateLength();

            if (IsVideoOpen)
            {
                IsVideoDetached = project.DetachedVideo;
                ShowingVideo = project.ShowVideo;
                SetVideoSubtitleFile(project.SubtitlesFile);
                VideoWindowNormal = new VideoWindowBounds(project.VideoWindowNormalLeft, project.VideoWindowNormalTop,
                    project.VideoWindowNormalWidth, project.VideoWindowNormalHeight);
                if (!ShowingSubs)
                {
                    ShowHideSubs();
                }
            }

            // ----- Load script
            LoadProjectScript(project);

            // ----- Restore position ---
            if (project.WavDisplayerPositionStartMs != -1 && project.WavDisplayerPositionStopMs != -1)
            {
                Waveform?.ZoomRange(project.WavDisplayerPositionStartMs, project.WavDisplayerPositionStopMs);
            }
            else
            {
                Waveform?.ZoomRange(0, 15_000);
            }

            if (project.FocusedTimeMs != -1)
            {
                SelectNodeAtIndex(FindInsertPos(project.FocusedTimeMs) - 1);
            }

            // ----- Load dictionnary ---
            int dictIdx = SpellChecker.GetDictIdx(project.Dictionary);
            if (dictIdx != -1)
            {
                LoadDict(dictIdx);
            }

            // ------ Load presets -----
            if (project.Presets.Length > 0)
            {
                LoadPresetFile(Path.Combine(PresetsPath, project.Presets));
            }

            // LoadSRT marca o projeto como alterado após autocorreção; a troca de modo do WAV também.
            IsDirty = _document.AutoCorrected || projectChanged;
            AddRecentProject(project.FileName);
            ShowStatusBarMessage("Project loaded.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            CloseProject();
            _dialogs.ShowError($"Could not open \"{path}\".\n{ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void Close()
    {
        if (CheckSubtitlesAreSaved())
        {
            CloseProject();
        }
    }

    /// <summary>FormCloseQuery: pergunta se deve gravar; devolve false para cancelar o fechamento.</summary>
    public bool CanCloseWindow() => CheckSubtitlesAreSaved();

    /// <summary>ActionSaveExecute: grava as legendas (com .bak antes) e o projeto.</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (Project is null)
        {
            return;
        }

        if (DelphiTrim(Project.SubtitlesFile).Length > 0)
        {
            if (SubtitleFile.PromoteToUtf8IfNeeded(_document))
            {
                _dialogs.ShowWarning(CharsetWarning);
            }

            if (SaveTo(Project.SubtitlesFile, BaseWriteOptions))
            {
                IsDirty = false;
                SetVideoSubtitleFile(Project.SubtitlesFile);
            }
        }

        SaveProject(Project, silent: false);
    }

    /// <summary>ActionSaveAsExecute: exporta uma cópia; o projeto continua apontando para o arquivo original.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void SaveAs()
    {
        if (Project is null)
        {
            return;
        }

        string current = Project.SubtitlesFile;
        int filterIndex = Path.GetExtension(current).ToLowerInvariant() switch
        {
            ".srt" => 1,
            ".ssa" => 3,
            ".ass" => 4,
            _ => 7,
        };
        if (_dialogs.SaveFile(SaveAsFilter, current, filterIndex) is not { } choice)
        {
            return;
        }

        string target = choice.FilterIndex is > 0 and < 8
            ? Path.ChangeExtension(choice.Path, SaveAsExtensions[choice.FilterIndex])
            : choice.Path;
        var options = BaseWriteOptions with
        {
            PreviousExtension = Path.GetExtension(current),
            Translate = choice.FilterIndex == 2 ? SubtitleWriteOptions.StripText : null,
        };
        SaveTo(target, options);
    }

    /// <summary>ActionProjectPropertiesExecute: aplica só o que mudou no projeto aberto.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task ProjectProperties()
    {
        if (Project is not { } project)
        {
            return;
        }

        var form = _dialogs.ShowProjectForm(new ProjectFormData
        {
            NewProject = false,
            VideoSource = project.VideoSource,
            WavFile = project.WavFile,
            PeakFile = project.PeakFile,
            SubtitlesFile = project.SubtitlesFile,
            SubtitlesVO = project.SubtitlesVO,
            ProjectFile = project.FileName,
            WavMode = project.WavMode,
            SaveAsUtf8 = _document.IsUtf8,
        });
        if (form is null)
        {
            return;
        }

        bool projectChanged = false;
        bool videoChanged = false;
        bool subtitlesChanged = false;

        // ----- Arquivo de legendas (com troca de formato do mesmo arquivo) -----
        if (project.SubtitlesFile != form.SubtitlesFile)
        {
            if (!CheckSubtitlesAreSaved())
            {
                return;
            }

            if (HasExtensionChanged(project.SubtitlesFile, form.SubtitlesFile))
            {
                string previous = project.SubtitlesFile;
                project.SubtitlesFile = form.SubtitlesFile;
                if (!File.Exists(project.SubtitlesFile))
                {
                    if (SubtitleFile.PromoteToUtf8IfNeeded(_document))
                    {
                        _dialogs.ShowWarning(CharsetWarning);
                    }

                    if (!SaveTo(form.SubtitlesFile, BaseWriteOptions with { PreviousExtension = Path.GetExtension(previous) }))
                    {
                        project.SubtitlesFile = previous;
                        return;
                    }
                }
            }

            project.SubtitlesFile = form.SubtitlesFile;
            _document = LoadDocument(project.SubtitlesFile);
            LoadSubtitles(_document);
            ShowStyleColumn = SubtitleFile.HasStyles(project.SubtitlesFile);
            // LoadSRT marca o projeto como alterado quando corrige tempos.
            if (_document.AutoCorrected)
            {
                IsDirty = true;
            }

            projectChanged = true;
            subtitlesChanged = true;
        }

        // ----- Legenda de referência (VO) -----
        if (project.SubtitlesVO != form.SubtitlesVO)
        {
            project.SubtitlesVO = form.SubtitlesVO;
            LoadVO(project.SubtitlesVO);
            projectChanged = true;
        }

        // ----- Fonte de vídeo -----
        if (project.VideoSource != form.VideoSource)
        {
            project.VideoSource = form.VideoSource;
            project.VideoPanelWidth = 0;
            project.VideoPanelHeight = 0;
            VideoPanelWidth = 0;
            projectChanged = true;
            videoChanged = true;
            await OpenMediaAsync(project).ConfigureAwait(true);
            if (!ShowingSubs)
            {
                ShowHideSubs();
            }
        }

        // ----- Modo WAV, arquivo WAV e arquivo de picos (TWAVDisplayer.LoadWAV) -----
        bool waveChanged = project.WavMode != form.WavMode || project.WavFile != form.WavFile ||
                           project.PeakFile != form.PeakFile;
        if (project.WavMode != form.WavMode)
        {
            project.WavMode = form.WavMode;
            projectChanged = true;
        }

        if (project.WavFile != form.WavFile)
        {
            project.WavFile = form.WavFile;
            projectChanged = true;
        }

        if (project.PeakFile != form.PeakFile)
        {
            project.PeakFile = form.PeakFile;
            projectChanged = true;
        }

        if (waveChanged)
        {
            Peaks = LoadPeaks(project, out bool modeChanged);
            projectChanged |= modeChanged;
            if (!IsVideoOpen)
            {
                await OpenMediaAsync(project).ConfigureAwait(true);
            }
            else
            {
                ApplyPlaybackAudio(project, ShowingVideo);
            }

            UpdateLength();
        }

        // ----- Nome do projeto e codificação -----
        if (project.FileName != form.ProjectFile)
        {
            project.FileName = form.ProjectFile;
            projectChanged = true;
        }

        if (_document.IsUtf8 != form.SaveAsUtf8)
        {
            _document.IsUtf8 = form.SaveAsUtf8;
            projectChanged = true;
        }

        if (projectChanged)
        {
            SaveProject(project, silent: false);
        }

        if (videoChanged || subtitlesChanged)
        {
            SetVideoSubtitleFile(project.SubtitlesFile);
        }

        // Esconde o vídeo quando ele não ficou aberto (ActionShowHideVideo.Execute).
        if (!IsVideoOpen && ShowingVideo)
        {
            ShowHideVideo();
        }
    }

    [RelayCommand]
    private static void Exit() => System.Windows.Application.Current.MainWindow?.Close();

    // ---------------------------------------------------------------- zoom e navegação

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ZoomIn() => Waveform?.ZoomIn();

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ZoomOut() => Waveform?.ZoomOut();

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ZoomSelection() => Waveform?.ZoomSelection();

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ZoomAll() => Waveform?.ZoomAll();

    /// <summary>ActionNextSubExecute: SelectNextSub e, havendo legendas, ActionPlay.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void NextSub() => PlaySibling(+1);

    /// <summary>ActionPreviousSubExecute: SelectPreviousSub e, havendo legendas, ActionPlay.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void PreviousSub() => PlaySibling(-1);

    private void PlaySibling(int step)
    {
        SelectSibling(step);
        if (Subtitles.Count > 0 && PlayCommand.CanExecute(null))
        {
            Play();
        }
    }

    /// <summary>
    /// vtvSubsListDblClick: ZoomAndSelectRange da legenda focada; tocando, desliga o auto-scroll da onda.
    /// No duplo clique do usuário (Sender atribuído), com o vídeo à mostra e parado, mostra o quadro inicial.
    /// </summary>
    public void ActivateSelectedSubtitle(bool fromUser = false)
    {
        if (SelectedSubtitle is { } item)
        {
            Waveform?.ZoomAndSelectRange(item.Model.StartMs, item.Model.StopMs);
            if (IsPlaying)
            {
                AutoScrollWavDisplay = false;
            }

            if (ShowingVideo && fromUser && !IsPlaying)
            {
                ShowStartFrame();
            }
        }
    }

    /// <summary>
    /// WAVDisplayer1SelectedRange: a legenda escolhida na onda (duplo clique, ou a borda que começou a
    /// ser arrastada) é focada na lista; sem ser dinâmica, mostra o quadro inicial com o vídeo à mostra.
    /// </summary>
    public void OnWaveformRangeSelected(int index, bool isDynamic)
    {
        if (index < 0 || index >= Subtitles.Count)
        {
            return;
        }

        SelectedSubtitle = Subtitles[index];
        if (ShowingVideo && !IsPlaying && !isDynamic)
        {
            ShowStartFrame();
        }
    }

    /// <summary>WAVDisplayer1SelectedRangeChange: novos tempos da legenda durante o arraste.</summary>
    public void OnWaveformRangeChanging(RangeTimesChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Index < 0 || change.Index >= Subtitles.Count)
        {
            return;
        }

        var item = Subtitles[change.Index];
        item.Model.StartMs = change.StartMs;
        item.Model.StopMs = change.StopMs;
        item.RefreshTimes();
        // A legenda do vídeo só é refeita no fim do arraste (SetTimeTask), como no legado.
        _disableVideoUpdatePreview = true;
        try
        {
            IsDirty = true;
        }
        finally
        {
            _disableVideoUpdatePreview = false;
        }

        RefreshRanges();
        UpdateEditorStatus(_caret.Line, _caret.Column, _caret.FirstVisible);
    }

    /// <summary>
    /// WAVDisplayer1SelectedRangeChanged → TUndoableSetTimeTask.DoTask → SetSubtitleTime: grava os
    /// tempos e reordena a lista (FullSortTreeAndSubList).
    /// </summary>
    public void OnWaveformRangeChanged(RangeTimesChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Index < 0 || change.Index >= Subtitles.Count)
        {
            return;
        }

        DoAndPush(new SetTimeTask(this, change.Index, change.OldStartMs, change.OldStopMs, change.StartMs, change.StopMs));
    }

    /// <summary>FullSortTreeAndSubList: ordena as legendas e leva a lista para a mesma ordem.</summary>
    private void SortSubtitles()
    {
        _document.Sort();
        for (int i = 0; i < _document.Items.Count; i++)
        {
            int current = IndexOfModel(_document.Items[i], i);
            if (current != i)
            {
                Subtitles.Move(current, i);
            }

            Subtitles[i].Index = i + 1;
        }
    }

    private int IndexOfModel(Subtitle model, int from)
    {
        for (int i = from; i < Subtitles.Count; i++)
        {
            if (ReferenceEquals(Subtitles[i].Model, model))
            {
                return i;
            }
        }

        return from;
    }

    /// <summary>ActionShowHideSceneChangeExecute.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ShowHideSceneChange()
    {
        ShowSceneChange = !ShowSceneChange;
        Settings.ShowSceneChange = ShowSceneChange;
    }

    // ---------------------------------------------------------------- reprodução e vídeo

    /// <summary>
    /// Cria o motor de mídia sobre a janela nativa do painel de vídeo (no original,
    /// <c>VideoRenderer.SetDisplayWindow(PanelVideo.Handle)</c>). Chamado pela janela depois de
    /// carregada, quando o HWND do painel já existe.
    /// </summary>
    public void AttachMedia(nint videoHandle)
    {
        if (_engine is not null)
        {
            return;
        }

        try
        {
            _engine = new MpvMediaEngine(videoHandle);
            _engine.PlaybackStopped += OnPlaybackStopped;
        }
        catch (Exception ex) when (ex is DllNotFoundException or InvalidOperationException or EntryPointNotFoundException)
        {
            System.Diagnostics.Trace.TraceWarning($"Media engine unavailable: {ex.Message}");
        }
    }

    /// <summary>Libera o motor antes de a janela nativa ser destruída (ao fechar o programa).</summary>
    public void Dispose()
    {
        // FormDestroy: o dicionário pessoal e StartStopServer(True).
        SavePersonalDictionary();
        StopWebServer();
        _playTimer.Stop();
        // O DisposeAsync do motor não espera nada de assíncrono (retorna ValueTask já concluído).
        _engine?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>PanelVideoResize: guarda a largura do painel de vídeo escolhida e grava o projeto em silêncio.</summary>
    public void SaveVideoPanelSize(int width, int height)
    {
        VideoPanelWidth = width;
        if (Project is not { } project || (project.VideoPanelWidth == width && project.VideoPanelHeight == height))
        {
            return;
        }

        project.VideoPanelWidth = width;
        project.VideoPanelHeight = height;
        SaveProject(project, silent: true);
    }

    /// <summary>ActionPlayExecute: sem seleção toca do cursor até o fim; com seleção, toca a seleção.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play()
    {
        FocusWaveformInTimingMode();
        AutoScrollWavDisplay = true;
        UpdateSubtitleForPreview(_videoPreviewNeedSubtitleUpdate);
        if (SelectionIsEmpty)
        {
            _playingMode = PlayingModeType.All;
            PlayRange(CursorMs, LengthMs, loop: false);
        }
        else
        {
            _playingMode = PlayingModeType.Selection;
            PlayRange(SelectionStartMs, SelectionStopMs, loop: false);
        }
    }

    /// <summary>ActionStopExecute.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Stop() => _engine?.Stop();

    /// <summary>ActionPauseExecute: pausa se está tocando, senão inicia a reprodução.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Pause()
    {
        if (IsPlaying)
        {
            TogglePause();
        }
        else
        {
            Play();
        }
    }

    /// <summary>ActionLoopExecute.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Loop()
    {
        FocusWaveformInTimingMode();
        AutoScrollWavDisplay = true;
        UpdateSubtitleForPreview(_videoPreviewNeedSubtitleUpdate);
        if (SelectionIsEmpty)
        {
            _playingMode = PlayingModeType.All;
            PlayRange(CursorMs, LengthMs, loop: true);
        }
        else
        {
            _playingMode = PlayingModeType.Selection;
            PlayRange(SelectionStartMs, SelectionStopMs, loop: true);
        }
    }

    /// <summary>ActionPlaySelStartExecute: 500 ms a partir do início da seleção.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void PlaySelectionStart()
    {
        if (!SelectionIsEmpty)
        {
            UpdateSubtitleForPreview(_videoPreviewNeedSubtitleUpdate);
            _playingMode = PlayingModeType.SelectionStart;
            PlayRange(SelectionStartMs, Math.Min(SelectionStartMs + StartEndPlayingDuration, SelectionStopMs), loop: false);
        }
    }

    /// <summary>ActionPlaySelEndExecute: 500 ms até o fim da seleção.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void PlaySelectionEnd()
    {
        if (!SelectionIsEmpty)
        {
            UpdateSubtitleForPreview(_videoPreviewNeedSubtitleUpdate);
            _playingMode = PlayingModeType.SelectionEnd;
            PlayRange(Math.Max(SelectionStartMs, SelectionStopMs - StartEndPlayingDuration), SelectionStopMs, loop: false);
        }
    }

    /// <summary>ActionLoopSelStartExecute.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void LoopSelectionStart()
    {
        if (!SelectionIsEmpty)
        {
            FocusWaveformInTimingMode();
            UpdateSubtitleForPreview(_videoPreviewNeedSubtitleUpdate);
            _playingMode = PlayingModeType.SelectionStart;
            PlayRange(SelectionStartMs, Math.Min(SelectionStartMs + StartEndPlayingDuration, SelectionStopMs), loop: true);
        }
    }

    /// <summary>ActionLoopSelEndExecute.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void LoopSelectionEnd()
    {
        if (!SelectionIsEmpty)
        {
            FocusWaveformInTimingMode();
            UpdateSubtitleForPreview(_videoPreviewNeedSubtitleUpdate);
            _playingMode = PlayingModeType.SelectionEnd;
            PlayRange(Math.Max(SelectionStartMs, SelectionStopMs - StartEndPlayingDuration), SelectionStopMs, loop: true);
        }
    }

    /// <summary>ActionPlayToEndExecute: da seleção (ou do cursor) até o fim.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void PlayToEnd()
    {
        FocusWaveformInTimingMode();
        AutoScrollWavDisplay = true;
        if (SelectionIsEmpty)
        {
            _playingMode = PlayingModeType.All;
            PlayRange(CursorMs, LengthMs, loop: false);
        }
        else
        {
            _playingMode = PlayingModeType.Selection;
            PlayRange(SelectionStartMs, LengthMs, loop: false);
        }
    }

    /// <summary>ActionSetPlaybackRate*Execute: 60 a 100%.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void SetPlaybackRate(string? percent)
    {
        if (_engine is not null && double.TryParse(percent, NumberStyles.Integer, CultureInfo.InvariantCulture, out double value))
        {
            _engine.PlaybackRate = value / 100.0;
        }
    }

    /// <summary>ActionShowHideVideoExecute.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ShowHideVideo()
    {
        if (!IsVideoOpen && !ShowingVideo)
        {
            ShowStatusBarMessage("Video file is not opened...");
            return;
        }

        ShowingVideo = !ShowingVideo;
        if (Project is { } audioProject) ApplyPlaybackAudio(audioProject, ShowingVideo);
        if (ShowingVideo)
        {
            UpdateSubtitleForPreview(false);
        }

        if (Project is { } project && project.ShowVideo != ShowingVideo)
        {
            project.ShowVideo = ShowingVideo;
            SaveProject(project, silent: true);
        }
    }

    /// <summary>ActionShowFrameAtCursorExecute.</summary>
    [RelayCommand(CanExecute = nameof(IsVideoOpen))]
    private void ShowFrameAtCursor() => ShowFrameAt(CursorMs);

    /// <summary>ActionShowStartFrameExecute: mpv seeks the timestamp directly, without DirectShow's offset.</summary>
    [RelayCommand(CanExecute = nameof(IsVideoOpen))]
    private void ShowStartFrame()
    {
        if (SelectedSubtitle is { } item)
        {
            ShowFrameAt(item.Model.StartMs);
        }
    }

    /// <summary>ActionShowStopFrameExecute: sem o atraso de 1 quadro, para ver o último quadro com legenda.</summary>
    [RelayCommand(CanExecute = nameof(IsVideoOpen))]
    private void ShowStopFrame()
    {
        if (SelectedSubtitle is { } item)
        {
            ShowFrameAt(item.Model.StopMs);
        }
    }

    /// <summary>WAVDisplayer1SelectionChange: com seleção, o trecho em reprodução acompanha conforme o modo.</summary>
    private void UpdatePlayRange()
    {
        if (_engine is null || !IsPlaying || SelectionIsEmpty)
        {
            return;
        }

        switch (_playingMode)
        {
            case PlayingModeType.Selection:
                _engine.UpdatePlayRange(SelectionStartMs, SelectionStopMs);
                break;
            case PlayingModeType.SelectionStart:
                _engine.UpdatePlayRange(SelectionStartMs, Math.Min(SelectionStartMs + StartEndPlayingDuration, SelectionStopMs));
                break;
            case PlayingModeType.SelectionEnd:
                _engine.UpdatePlayRange(Math.Max(SelectionStartMs, SelectionStopMs - StartEndPlayingDuration), SelectionStopMs);
                break;
        }
    }

    partial void OnSelectionStartMsChanged(int value) => UpdatePlayRange();

    partial void OnSelectionStopMsChanged(int value) => UpdatePlayRange();

    private bool CanPlay => IsMediaOpen;

    private bool SelectionIsEmpty => SelectionStopMs == 0;

    private void PlayRange(int startMs, int stopMs, bool loop)
    {
        if (_engine is not { IsOpen: true })
        {
            return;
        }

        OnStartPlaying();
        _engine.Play(startMs, stopMs, loop);
        IsPlaying = true;
        _playTimer.Start();
    }

    /// <summary>TWAVDisplayer.Pause: alterna entre pausar e continuar (o timer liga/desliga).</summary>
    private void TogglePause()
    {
        if (_playTimer.IsEnabled)
        {
            _playTimer.Stop();
            _engine?.Pause();
        }
        else
        {
            _playTimer.Start();
            _engine?.Resume();
        }
    }

    /// <summary>TWAVDisplayer.OnUpdateCursor: cursor de reprodução na posição do motor.</summary>
    private void OnPlayTimerTick()
    {
        if (_engine is not { IsOpen: true })
        {
            _playTimer.Stop();
            return;
        }

        RefreshVideoInfo();
        int position = Math.Clamp(_engine.InterpolatedPositionMs, 0, LengthMs);
        PlayCursorMs = position;
        if (AutoScrollWavDisplay && Waveform?.IsMouseInteracting != true && (position < PositionMs || position > PositionMs + PageSizeMs))
        {
            // Mantém 1/8 da vista anterior, como SetPlayCursorPos; SetPositionMs prende em [0, Length - PageSize].
            PositionMs = Math.Clamp(position - (PageSizeMs / 8), 0, Math.Max(0, LengthMs - PageSizeMs));
        }
    }

    /// <summary>InternalOnStopPlaying: o fim do trecho ou o Stop desligam o timer e escondem o cursor.</summary>
    /// <remarks>Chega pela thread de eventos do mpv. <c>BeginInvoke</c> não a bloqueia, para que o
    /// <see cref="Dispose"/> (que espera essa thread terminar) não trave a janela ao fechar.</remarks>
    private void OnPlaybackStopped(object? sender, EventArgs e) => dispatcher.BeginInvoke(() =>
    {
        _playTimer.Stop();
        IsPlaying = false;
    });

    /// <summary>VideoRenderer.ShowImageAt: mostra o quadro exato no tempo pedido.</summary>
    private void ShowFrameAt(int timeMs)
    {
        UpdateSubtitleForPreview(_videoPreviewNeedSubtitleUpdate);
        _ = _engine?.SeekAsync(timeMs);
    }

    /// <summary>
    /// UpdateSubtitleForPreview: com o vídeo à mostra, grava as legendas atuais numa cópia temporária e a
    /// carrega no vídeo quando ele está tocando, pausado ou quando é forçado; com o vídeo escondido, só
    /// marca que a cópia precisa ser refeita.
    /// </summary>
    private void UpdateSubtitleForPreview(bool forceUpdate)
    {
        if (_disableVideoUpdatePreview || Project is null || DelphiTrim(Project.SubtitlesFile).Length == 0 ||
            _engine is not { IsOpen: true } engine)
        {
            return;
        }

        if (!ShowingVideo)
        {
            _videoPreviewNeedSubtitleUpdate = true;
            return;
        }

        bool paused = IsPlaying && !_playTimer.IsEnabled;
        if (!(engine.IsPlaying || paused || forceUpdate) || !SubtitleFile.CanWrite(Project.SubtitlesFile))
        {
            return;
        }

        string tmp = SubtitlePreview.TemporaryFileName(Project.SubtitlesFile);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tmp)!);
            SubtitleFile.Save(_document, tmp, BaseWriteOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning($"Subtitle preview not saved to {tmp}: {ex.Message}");
            return;
        }

        SetVideoSubtitleFile(tmp);
        _videoPreviewNeedSubtitleUpdate = false;
    }

    /// <summary>VideoRenderer.SetSubtitleFilename.</summary>
    private void SetVideoSubtitleFile(string path)
    {
        if (_engine is { IsOpen: true } engine)
        {
            engine.SetSubtitleOverlay(path.Length > 0 && File.Exists(path) ? path : null);
        }
    }

    /// <summary>Abre a mídia do projeto no motor. Prefere o vídeo (traz áudio e imagem); sem ele, o WAV.</summary>
    private async Task OpenMediaAsync(VssProject project)
    {
        _playTimer.Stop();
        IsPlaying = false;
        _engine?.Close();
        IsMediaOpen = false;
        IsVideoOpen = false;
        // ClearVideoSceneChange: as mudanças de cena do vídeo anterior saem da onda.
        SceneChanges = null;
        ShowStatusBarMessage("Loading audio file...");
        ShowStatusBarMessage("Loading video file...");
        if (_engine is null)
        {
            return;
        }

        string source = ResolveMediaSource(project);
        if (source.Length == 0)
        {
            IsMediaOpen = false;
            IsVideoOpen = false;
            return;
        }

        try
        {
            await _engine.OpenAsync(source).ConfigureAwait(true);
            // Decodifica o primeiro quadro para o painel de vídeo já ter tamanho e imagem.
            await _engine.SeekAsync(0).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
        {
            ShowStatusBarMessage($"Can't open media file : {source}");
            System.Diagnostics.Trace.TraceWarning($"Could not open {source}: {ex.Message}");
            IsMediaOpen = false;
            IsVideoOpen = false;
            return;
        }

        IsMediaOpen = _engine.IsOpen;
        _engine.Volume = Volume;
        RefreshVideoInfo();
        ApplyPlaybackAudio(project, project.ShowVideo);
        if (IsVideoOpen)
        {
            await LoadVideoSceneChangeAsync(project.VideoSource).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// LoadVideoSceneChange: usa o .scenechange ao lado do vídeo; se não existir, extrai os
    /// quadros-chave (FFmpeg no lugar de MatroskaHelper/MP4File/VfW) e grava o arquivo.
    /// </summary>
    private async Task LoadVideoSceneChangeAsync(string videoSource)
    {
        string path = Path.ChangeExtension(videoSource, SceneChangeFile.Extension);
        int[] scenes;
        try
        {
            if (File.Exists(path))
            {
                scenes = SceneChangeFile.Load(path);
            }
            else
            {
                ShowStatusBarMessage("Extracting keyframes...");
                scenes = await new FFmpegKeyframeReader().ReadKeyframesMsAsync(videoSource, CancellationToken.None).ConfigureAwait(true);
                SceneChangeFile.Save(path, scenes);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
            or DllNotFoundException or EntryPointNotFoundException or NotSupportedException)
        {
            System.Diagnostics.Trace.TraceWarning($"Scene changes unavailable for {videoSource}: {ex.Message}");
            scenes = [];
        }

        SceneChanges = scenes;
    }

    /// <summary>Atualiza se há vídeo e a proporção do painel (UpdateVideoRendererWindow).</summary>
    private void RefreshVideoInfo() => (IsVideoOpen, VideoAspectRatio) = _engine is { IsOpen: true } engine
        ? (engine.HasVideo, engine.VideoSize.Height > 0 ? (double)engine.VideoSize.Width / engine.VideoSize.Height : 0)
        : (false, 0);

    private static string ResolveMediaSource(VssProject project)
    {
        if (project.VideoSource.Length > 0 && File.Exists(project.VideoSource))
        {
            return project.VideoSource;
        }

        return project.WavFile.Length > 0 && File.Exists(project.WavFile) ? project.WavFile : string.Empty;
    }

    /// <summary>ActionShowHideVideoExecute switches between AudioOnlyRenderer (WAV) and VideoRenderer.</summary>
    private void ApplyPlaybackAudio(VssProject project, bool showVideo)
    {
        if (_engine is not { IsOpen: true } engine) return;
        try
        {
            engine.SetExternalAudio(!showVideo && IsVideoOpen && File.Exists(project.WavFile) ? project.WavFile : null);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _dialogs.LogMessage($"Could not select project audio: {ex.Message}", silent: false);
        }
    }

    /// <summary>UpdateLinesCounter: chamado pelo editor quando o cursor, o texto ou a rolagem mudam.</summary>
    public void UpdateEditorStatus(int lineIndex, int column, int firstVisibleLine)
    {
        _caret = (lineIndex, column, firstVisibleLine);
        string text = SelectedSubtitle?.Text ?? string.Empty;
        LinesCounterText = SubtitleTextStats.LinesCounter(text, firstVisibleLine);
        int? duration = SelectedSubtitle is { } s ? s.Model.StopMs - s.Model.StartMs : null;
        EditorStatusText = SubtitleTextStats.StatusText(text, lineIndex, column, firstVisibleLine, duration);
    }

    /// <summary>SelectNode: foca a legenda e faz o mesmo que o duplo clique.</summary>
    private void SelectNodeAtIndex(int index)
    {
        if (index >= 0 && index < Subtitles.Count)
        {
            SelectedSubtitle = Subtitles[index];
            ActivateSelectedSubtitle();
        }
    }

    /// <summary>TRangeList.FindInsertPos(time, -1): posição de inserção pela ordem de início.</summary>
    private int FindInsertPos(int timeMs)
    {
        int lo = 0, hi = _document.Items.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_document.Items[mid].StartMs < timeMs)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    partial void OnSelectedSubtitleChanged(SubtitleItemViewModel? value)
    {
        UpdateEditorStatus(0, 0, 0);
        UpdateMemoVO();
        if (value is not null)
        {
            CallJsOnSelectedSubtitle(value);
        }
    }

    partial void OnProjectChanged(VssProject? value)
    {
        OnPropertyChanged(nameof(IsStyleEnabled));
        OnPropertyChanged(nameof(SelBeginText));
        OnPropertyChanged(nameof(SelEndText));
        OnPropertyChanged(nameof(SelLengthText));
        OnPropertyChanged(nameof(ViewBeginText));
        OnPropertyChanged(nameof(ViewEndText));
        OnPropertyChanged(nameof(ViewLengthText));

        // Todas as ações que dependem de projeto aberto (o ActionList.OnUpdate da VCL as reavalia sozinho).
        foreach (var property in typeof(MainViewModel).GetProperties())
        {
            if (typeof(IRelayCommand).IsAssignableFrom(property.PropertyType) && property.GetValue(this) is IRelayCommand command)
            {
                command.NotifyCanExecuteChanged();
            }
        }
    }

    partial void OnVolumeChanged(int value)
    {
        Settings.Volume = value;
        if (_engine is { IsOpen: true })
        {
            _engine.Volume = value;
        }
    }

    partial void OnAutoScrollWavDisplayChanged(bool value) => Settings.AutoScrollWavDisplay = value;

    partial void OnAutoScrollSubtitlesChanged(bool value)
    {
        Settings.AutoScrollSubtitles = value;
        ApplyAutoScrollSubtitles(value);
    }

    // ---------------------------------------------------------------- auxiliares

    private void ShowStatusBarMessage(string text)
    {
        _statusBarSecondaryText = text;
        UpdateStatusBar();
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    /// <summary>CheckSubtitlesAreSaved.</summary>
    private bool CheckSubtitlesAreSaved()
    {
        if (Project is null || !IsDirty)
        {
            return true;
        }

        switch (_dialogs.AskYesNoCancel($"The subtitle file \"{Project.SubtitlesFile}\" has changed, do you want to save it before closing ?"))
        {
            case true:
                Save();
                return !IsDirty;
            case false:
                return true;
            default:
                return false;
        }
    }

    /// <summary>CloseProject.</summary>
    private void CloseProject()
    {
        _playTimer.Stop();
        _engine?.Close();
        IsPlaying = false;
        IsMediaOpen = false;
        IsVideoOpen = false;
        ShowingVideo = false;
        PlayCursorMs = 0;
        Waveform?.ClearSelection();
        Suggestions.Clear();
        Errors.Clear();
        AutoScrollSubtitles = false;
        SelectedSubtitle = null;
        Subtitles.Clear();
        Ranges = null;
        ClearVO();
        SceneChanges = null;
        Peaks = null;
        LengthMs = 0;
        PositionMs = 0;
        PageSizeMs = 0;
        CursorMs = 0;
        SelectionStartMs = 0;
        SelectionStopMs = 0;
        VerticalScaling = 100;
        ClearUndo();
        _document = new SubtitleDocument();
        StyleNames.Clear();
        Project = null;
        IsDirty = false;
    }

    /// <summary>SaveProject: grava o .vssprj com a vista atual e a legenda focada.</summary>
    private void SaveProject(VssProject project, bool silent)
    {
        if (File.Exists(project.FileName) && File.GetAttributes(project.FileName).HasFlag(FileAttributes.ReadOnly))
        {
            if (!silent)
            {
                _dialogs.ShowError($"The project file {project.FileName} can't be saved because it's in read only mode.");
            }

            return;
        }

        project.WavDisplayerPositionStartMs = PositionMs;
        project.WavDisplayerPositionStopMs = PositionMs + PageSizeMs;
        project.FocusedTimeMs = SelectedSubtitle is { } s ? s.Model.StartMs + ((s.Model.StopMs - s.Model.StartMs) / 2) : -1;
        project.Dictionary = SpellChecker.CurrentDictName;
        project.VideoWindowNormalLeft = VideoWindowNormal.Left;
        project.VideoWindowNormalTop = VideoWindowNormal.Top;
        project.VideoWindowNormalWidth = VideoWindowNormal.Width;
        project.VideoWindowNormalHeight = VideoWindowNormal.Height;
        try
        {
            VssProjectFile.Save(project, project.FileName, SaveProjectScript(project));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Could not save \"{project.FileName}\".\n{ex.Message}");
        }
    }

    /// <summary>
    /// Opções comuns de SaveSubtitlesAsSSA/ASS: PlayResX/PlayResY do vídeo aberto e a mesma versão do
    /// About (g_ApplicationVersion) em "; Written by VisualSubSync".
    /// </summary>
    private SubtitleWriteOptions BaseWriteOptions
    {
        get
        {
            var (width, height) = IsVideoOpen && _engine is { IsOpen: true, HasVideo: true } engine ? engine.VideoSize : (0, 0);
            return new SubtitleWriteOptions { VideoWidth = width, VideoHeight = height, ApplicationVersion = ApplicationVersion };
        }
    }

    private bool SaveTo(string target, SubtitleWriteOptions options)
    {
        if (!SubtitleFile.CanWrite(target))
        {
            _dialogs.ShowError(target == Project?.SubtitlesFile
                ? $"Cannot save the file \"{target}\". Please, specify a valid file extension in the \"Project Properties\" dialog under the \"File\" menu."
                : $"Cannot save the file \"{target}\". Please, specify a valid file extension.");
            return false;
        }

        try
        {
            if (Settings.EnableBackup)
            {
                TryBackup(target);
            }

            SubtitleFile.Save(_document, target, options);
            // SaveSubtitles sem BackupOnly (Save, Save As, troca de formato, tradução): limpa o estado alterado.
            ApplyAutoBackupSettings(); // Reset auto-backup timing
            IsDirty = false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Could not save \"{target}\".\n{ex.Message}");
            return false;
        }
    }

    /// <summary>Como WideCopyFile no legado, uma falha na cópia de segurança não impede a gravação.</summary>
    private void TryBackup(string target)
    {
        try
        {
            SubtitleFile.Backup(target, BackupDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning($"Backup failed for {target}: {ex.Message}");
            _dialogs.LogMessage($"Backup failed for {target}: {ex.Message}", silent: false);
        }
    }

    /// <summary>LoadSubtitles: arquivo inexistente vira documento vazio (com estilo Default em SSA/ASS).</summary>
    private static SubtitleDocument LoadDocument(string path)
    {
        if (File.Exists(path) && SubtitleFile.CanRead(path))
        {
            return SubtitleFile.Load(path);
        }

        var document = new SubtitleDocument();
        if (SubtitleFile.HasStyles(path))
        {
            document.EnsureDefaultStyle();
        }

        return document;
    }

    private void LoadSubtitles(SubtitleDocument document)
    {
        // ErrorReportForm.Clear: o original só limpa ao fechar ou trocar o arquivo de legendas; aqui
        // também ao recarregar, para não ficar com erros de legendas que não existem mais.
        Errors.Clear();
        ClearUndo();
        ResetFind();
        if (_subtitleFilter is not null)
        {
            _subtitleFilter = null;
            SubtitleFilterChanged?.Invoke(this, EventArgs.Empty);
        }

        Subtitles.Clear();
        for (int i = 0; i < document.Items.Count; i++)
        {
            // LoadSubtitles: UpdateSubTimeFromText (EnableExperimentalKaraoke).
            Karaoke.UpdateSubTimesFromText(document.Items[i]);
            Subtitles.Add(new SubtitleItemViewModel(document.Items[i], i + 1, OnSubtitleTextChanged));
        }

        RefreshRanges();
        _styleFormIndex = -1;
        UpdateStylesComboBox();
    }

    /// <summary>MemoSubtitleTextChange: o texto vai para a legenda, a onda e a lista; o projeto fica alterado.</summary>
    private void OnSubtitleTextChanged(SubtitleItemViewModel item, string oldText)
    {
        RecordTextChange(item, oldText);
        Karaoke.UpdateSubTimesFromText(item.Model);
        IsDirty = true;
        RefreshRanges();
        UpdateEditorStatus(_caret.Line, _caret.Column, _caret.FirstVisible);
    }

    private void RefreshRanges()
    {
        Ranges = [.. _document.Items.Select(s => new WaveformRange(s.StartMs, s.StopMs, s.Text, s.SubTimes.Length > 0 ? [.. s.SubTimes] : null))];
        RefreshVoTexts();
    }

    /// <summary>
    /// Escolha do arquivo de picos de LoadProject. Gerar picos a partir do WAV ou do vídeo depende do
    /// motor de mídia; aqui só se usa o .peak existente. Como TWAVDisplayer.LoadWAV, picos fracos são
    /// normalizados e o .peak é regravado.
    /// </summary>
    private PeakData? LoadPeaks(VssProject project, out bool projectChanged)
    {
        projectChanged = false;
        string? peakPath = null;
        if (project.WavMode == ProjectWavMode.PeakOnly)
        {
            if (File.Exists(project.PeakFile))
            {
                peakPath = project.PeakFile;
            }
            else if (File.Exists(project.WavFile))
            {
                project.WavMode = ProjectWavMode.External;
                projectChanged = true;
                _dialogs.ShowWarning($"Peak file {project.PeakFile} hasn't been found, project has been switched to \"WAV file\" mode");
                peakPath = Path.ChangeExtension(project.WavFile, PeakFile.Extension);
            }
            else
            {
                _dialogs.ShowWarning($"Can't open peak file : {project.PeakFile}");
            }
        }
        else if (project.WavMode == ProjectWavMode.External)
        {
            if (File.Exists(project.WavFile))
            {
                peakPath = Path.ChangeExtension(project.WavFile, PeakFile.Extension);
            }
            else if (File.Exists(project.PeakFile))
            {
                project.WavMode = ProjectWavMode.PeakOnly;
                projectChanged = true;
                _dialogs.ShowWarning($"WAV file {project.WavFile} hasn't been found, project has been switched to \"Peak file only\" mode");
                peakPath = project.PeakFile;
            }
            else if (project.WavFile.Length > 0 && File.Exists(Path.ChangeExtension(project.WavFile, PeakFile.Extension)))
            {
                project.WavMode = ProjectWavMode.PeakOnly;
                project.PeakFile = Path.ChangeExtension(project.WavFile, PeakFile.Extension);
                projectChanged = true;
                peakPath = project.PeakFile;
            }
            else
            {
                _dialogs.ShowWarning($"Can't open WAV file : {project.WavFile}");
            }
        }

        // LoadWAV só aceita o .peak maior que o cabeçalho; um arquivo curto é refeito a partir do WAV.
        bool peakUsable = peakPath is not null && File.Exists(peakPath) && new FileInfo(peakPath).Length > PeakFile.HeaderSize;
        if (peakPath is not null && !peakUsable && File.Exists(project.WavFile))
        {
            using var wav = WavFile.Open(project.WavFile);
            if (wav is not null)
            {
                PeakData? created = null;
                _dialogs.RunPeakCreation(progress => created = wav.CreatePeaks(progress));
                created ??= wav.CreatePeaks();
                try { PeakFile.Save(created, peakPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _dialogs.LogMessage($"Could not save {peakPath}: {ex.Message}", silent: false);
                }

                return created;
            }
        }

        if (peakPath is null || !peakUsable)
        {
            return null;
        }

        PeakData peaks;
        try
        {
            peaks = PeakFile.Load(peakPath);
        }
        catch (InvalidDataException ex)
        {
            // O legado lê qualquer cabeçalho; aqui um .peak inválido não impede o projeto de abrir.
            _dialogs.LogMessage($"Invalid peak file {peakPath}: {ex.Message}", silent: false);
            return null;
        }

        if (PeakFile.Normalize(peaks))
        {
            try
            {
                PeakFile.Save(peaks, peakPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceWarning($"Could not rewrite {peakPath}: {ex.Message}");
            }
        }

        return peaks;
    }

    /// <summary>
    /// Duração da vista: a da forma de onda; senão a do vídeo; senão a última legenda + 1 minuto,
    /// ou 5 minutos (mesma regra de LoadProject).
    /// </summary>
    private void UpdateLength()
    {
        int mediaDuration = _engine is { IsOpen: true } ? _engine.DurationMs : 0;
        LengthMs = Peaks?.LengthMs ?? (mediaDuration > 0
            ? mediaDuration
            : _document.Items.Count > 0 ? _document.Items[^1].StopMs + 60_000 : 5 * 60 * 1000);
    }

    /// <summary>
    /// HasExtensionChanged: a extensão mudou e o resto do caminho é o mesmo (troca de formato do
    /// mesmo arquivo), como WideChangeFileExt(..., '.dummy') no legado.
    /// </summary>
    private static bool HasExtensionChanged(string oldFile, string newFile)
    {
        if (string.Equals(Path.GetExtension(oldFile), Path.GetExtension(newFile), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(
            Path.ChangeExtension(oldFile, ".dummy"), Path.ChangeExtension(newFile, ".dummy"), StringComparison.Ordinal);
    }

    private static string DelphiTrim(string s) => Core.Text.DelphiText.Trim(s);
}
