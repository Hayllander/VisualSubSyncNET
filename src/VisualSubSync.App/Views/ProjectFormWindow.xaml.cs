using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VisualSubSync.App.Services;
using VisualSubSync.Core.Projects;
using VisualSubSync.Core.Text;
using VisualSubSync.Media;
using VisualSubSync.Media.FFmpeg;

namespace VisualSubSync.App.Views;

/// <summary>
/// TProjectForm (ProjectUnit.pas): janela de novo projeto e de propriedades. As regras de cada campo
/// (ficheiro de vídeo, WAV/picos, legenda, VO e projeto) seguem o fonte original.
/// </summary>
public partial class ProjectFormWindow : Window
{
    private const string LeaveEmpty = "Leave empty for extraction";

    private const string VideoFilter =
        "Video files|*.AVI;*.OGM;*.MKV;*.MKA;*.MP4;*.DIVX;*.RM;*.RMVB;*.MPEG;*.MPG;*.VOB;*.AVS;*.WMV;*.MOV;*.FLV|All files (*.*)|*.*";

    private const string SubtitleFilter =
        "SRT files (*.srt)|*.SRT|SSA/ASS files (*.ssa,*.ass)|*.SSA;*.ASS|All files (*.*)|*.*";

    private const string WavFilter = "WAV files (*.wav)|*.WAV|All files (*.*)|*.*";

    private const string PeakFilter = "Peak files (*.peak)|*.PEAK|All files (*.*)|*.*";

    private const string ProjectFilter = "VSS project (*.vssprj)|*.VSSPRJ|All files (*.*)|*.*";

    private readonly IDialogService _dialogs;
    private readonly ProjectFormData _data;
    private IReadOnlyList<AudioStreamInfo> _audioStreams = [];
    private int _audioAnalysisVersion;

    public ProjectFormWindow(IDialogService dialogs, ProjectFormData data)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(data);
        InitializeComponent();
        _dialogs = dialogs;
        _data = data;

        if (data.NewProject)
        {
            ConfigureInNewProjectMode();
        }
        else
        {
            ConfigureInModifyProjectMode(data);
        }

        Loaded += (_, _) => _ = RefreshAudioTracksAsync();
    }

    // ---------------------------------------------------------------- modos

    /// <summary>ConfigureInNewProjectMode.</summary>
    private void ConfigureInNewProjectMode()
    {
        Title = "New Project";
        CreateButton.Visibility = Visibility.Visible;
        OkButton.Visibility = Visibility.Collapsed;
        WavSelectMode(ProjectWavMode.NoWaveform);
        Clear();
        VideoSourceOperationNone.IsChecked = true;
        UpdateVideoSourceOperationState();
    }

    /// <summary>ConfigureInModifyProjectMode.</summary>
    private void ConfigureInModifyProjectMode(ProjectFormData data)
    {
        Title = "Project Properties";
        CreateButton.Visibility = Visibility.Collapsed;
        OkButton.Visibility = Visibility.Visible;
        VideoFileEdit.Text = data.VideoSource;
        WavFileEdit.Text = data.WavFile.Length == 0 ? LeaveEmpty : data.WavFile;
        SubtitleFileEdit.Text = data.SubtitlesFile;
        SubtitleVOEdit.Text = data.SubtitlesVO;
        ProjectFileEdit.Text = data.ProjectFile;
        PeakFileEdit.Text = data.PeakFile.Length == 0 ? LeaveEmpty : data.PeakFile;
        WavSelectMode(data.WavMode);
        SaveAsUtf8Check.IsChecked = data.SaveAsUtf8;
        UpdateFormatCombobox();
        UpdateColor();
        UpdateExtractEnabled();
        VideoSourceOperationNone.IsChecked = true;
        UpdateVideoSourceOperationState();
    }

    /// <summary>Clear.</summary>
    private void Clear()
    {
        VideoFileEdit.Text = string.Empty;
        SubtitleFileEdit.Text = string.Empty;
        SubtitleVOEdit.Text = string.Empty;
        ProjectFileEdit.Text = string.Empty;
        SaveAsUtf8Check.IsChecked = false;
        SubtitleFormatCombo.SelectedIndex = 0;
        UpdateExtractEnabled();
        ClearAudioTracks();

        PeakFileEdit.Text = LeaveEmpty;
        PeakFileEdit.Foreground = SystemColors.InactiveCaptionBrush;
        WavFileEdit.Text = LeaveEmpty;
        WavFileEdit.Foreground = SystemColors.InactiveCaptionBrush;
    }

    // ---------------------------------------------------------------- modo WAV

    /// <summary>WAVSelectMode: marca o modo e habilita os campos e botões correspondentes.</summary>
    private void WavSelectMode(ProjectWavMode mode)
    {
        NoWaveformRadio.IsChecked = mode == ProjectWavMode.NoWaveform;
        ExternalRadio.IsChecked = mode == ProjectWavMode.External;
        PeakOnlyRadio.IsChecked = mode == ProjectWavMode.PeakOnly;
        ApplyWaveMode();
    }

    /// <summary>rbInternalWAVClick.</summary>
    private void OnWaveModeChanged(object sender, RoutedEventArgs e) => ApplyWaveMode();

    private void ApplyWaveMode()
    {
        bool external = ExternalRadio.IsChecked == true;
        bool peakOnly = PeakOnlyRadio.IsChecked == true;
        WavFileEdit.IsEnabled = external;
        BrowseWavButton.IsEnabled = external;
        PeakFileEdit.IsEnabled = peakOnly;
        BrowsePeakButton.IsEnabled = peakOnly;
    }

    /// <summary>GetWAVMode.</summary>
    private ProjectWavMode GetWavMode() => NoWaveformRadio.IsChecked == true
        ? ProjectWavMode.NoWaveform
        : ExternalRadio.IsChecked == true ? ProjectWavMode.External : ProjectWavMode.PeakOnly;

    /// <summary>GetWAVFilename: vazio quando o campo mostra "Leave empty for extraction".</summary>
    private string GetWavFilename() => WavFileEdit.Text == LeaveEmpty ? string.Empty : WavFileEdit.Text;

    /// <summary>GetPeakFilename.</summary>
    private string GetPeakFilename() => PeakFileEdit.Text == LeaveEmpty ? string.Empty : PeakFileEdit.Text;

    // ---------------------------------------------------------------- cores e placeholder

    /// <summary>EditPeakFilenameEnter / EditPeakFilenameExit: o placeholder aparece e some com o foco.</summary>
    private void OnPlaceholderGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox edit && edit.Text == LeaveEmpty)
        {
            edit.Text = string.Empty;
            edit.Foreground = SystemColors.WindowTextBrush;
        }
    }

    private void OnPlaceholderLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox edit && edit.Text.Length == 0)
        {
            edit.Text = LeaveEmpty;
        }

        UpdateColor();
    }

    /// <summary>EditUpdateColor.</summary>
    private void OnPeakTextChanged(object sender, TextChangedEventArgs e) => UpdateColor();

    private void OnWavTextChanged(object sender, TextChangedEventArgs e) => UpdateColor();

    /// <summary>UpdateColor: cinza quando o campo está em branco (equivale a clInactiveCaption).</summary>
    private void UpdateColor()
    {
        WavFileEdit.Foreground = GetWavFilename().Length > 0 ? SystemColors.WindowTextBrush : SystemColors.InactiveCaptionBrush;
        PeakFileEdit.Foreground = GetPeakFilename().Length > 0 ? SystemColors.WindowTextBrush : SystemColors.InactiveCaptionBrush;
    }

    // ---------------------------------------------------------------- formato da legenda

    /// <summary>cbSubtitleFormatChange: troca a extensão do arquivo de legenda.</summary>
    private void OnSubtitleFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        // Na VCL, ItemIndex atribuído por código não dispara OnChange: só a escolha do usuário troca a extensão.
        if (_updatingFormat || SubtitleFileEdit is null || SubtitleFileEdit.Text.Length == 0)
        {
            return;
        }

        string ext = SubtitleFormatCombo.SelectedIndex switch
        {
            0 => ".srt",
            1 => ".ssa",
            2 => ".ass",
            _ => string.Empty,
        };
        if (ext.Length > 0)
        {
            SubtitleFileEdit.Text = Path.ChangeExtension(SubtitleFileEdit.Text, ext);
        }
    }

    private bool _updatingFormat;

    /// <summary>UpdateFormatCombobox.</summary>
    private void UpdateFormatCombobox()
    {
        _updatingFormat = true;
        try
        {
            SubtitleFormatCombo.SelectedIndex = Path.GetExtension(SubtitleFileEdit.Text).ToLowerInvariant() switch
            {
                ".srt" => 0,
                ".ssa" => 1,
                ".ass" => 2,
                _ => SubtitleFormatCombo.SelectedIndex,
            };
        }
        finally
        {
            _updatingFormat = false;
        }
    }

    // ---------------------------------------------------------------- navegação

    private void OnBrowseVideo(object sender, RoutedEventArgs e)
    {
        if (_dialogs.OpenFile(VideoFilter, VideoFileEdit.Text) is { } path)
        {
            VideoFileEdit.Text = path;

            string wav = Path.ChangeExtension(path, ".wav");
            string peak = Path.ChangeExtension(path, ".peak");
            if (DelphiText.Trim(GetWavFilename()).Length == 0 && File.Exists(wav))
            {
                WavFileEdit.Text = wav;
                WavSelectMode(ProjectWavMode.External);
            }
            else if (DelphiText.Trim(GetPeakFilename()).Length == 0 && File.Exists(peak))
            {
                PeakFileEdit.Text = peak;
                WavSelectMode(ProjectWavMode.PeakOnly);
            }
            else
            {
                WavSelectMode(ProjectWavMode.PeakOnly);
            }

            if (DelphiText.Trim(SubtitleFileEdit.Text).Length == 0)
            {
                SubtitleFileEdit.Text = Path.ChangeExtension(path, ".srt");
                UpdateFormatCombobox();
            }

            if (DelphiText.Trim(ProjectFileEdit.Text).Length == 0)
            {
                ProjectFileEdit.Text = Path.ChangeExtension(path, ".vssprj");
            }
        }

        UpdateExtractEnabled();
        _ = RefreshAudioTracksAsync();
    }

    private void OnBrowseWav(object sender, RoutedEventArgs e)
    {
        if (_dialogs.OpenFile(WavFilter, GetWavFilename()) is { } path)
        {
            WavFileEdit.Text = path;
            AutoCompleteSubtitleAndProject(path, ".srt");
        }
    }

    private void OnBrowsePeak(object sender, RoutedEventArgs e)
    {
        if (_dialogs.OpenFile(PeakFilter, GetPeakFilename()) is { } path)
        {
            PeakFileEdit.Text = path;
            AutoCompleteSubtitleAndProject(path, ".srt");
        }
    }

    private void OnBrowseSubtitle(object sender, RoutedEventArgs e)
    {
        int index = Path.GetExtension(SubtitleFileEdit.Text).ToLowerInvariant() switch
        {
            ".srt" => 1,
            ".ssa" or ".ass" => 2,
            _ => 3,
        };
        if (_dialogs.OpenFile(SubtitleFilter, SubtitleFileEdit.Text, index) is { } path)
        {
            SubtitleFileEdit.Text = path;
            UpdateFormatCombobox();
            if (DelphiText.Trim(ProjectFileEdit.Text).Length == 0)
            {
                ProjectFileEdit.Text = Path.ChangeExtension(path, ".vssprj");
            }
        }
    }

    private void OnBrowseProject(object sender, RoutedEventArgs e)
    {
        if (_dialogs.OpenFile(ProjectFilter, ProjectFileEdit.Text) is { } path)
        {
            ProjectFileEdit.Text = path;
        }
    }

    private void OnBrowseVO(object sender, RoutedEventArgs e)
    {
        if (_dialogs.OpenFile(SubtitleFilter, SubtitleVOEdit.Text) is { } path)
        {
            SubtitleVOEdit.Text = path;
        }
    }

    /// <summary>Preenche legenda e projeto vazios a partir do nome escolhido (WAV/picos).</summary>
    private void AutoCompleteSubtitleAndProject(string source, string subtitleExtension)
    {
        if (DelphiText.Trim(SubtitleFileEdit.Text).Length == 0)
        {
            SubtitleFileEdit.Text = Path.ChangeExtension(source, subtitleExtension);
            UpdateFormatCombobox();
        }

        if (DelphiText.Trim(ProjectFileEdit.Text).Length == 0)
        {
            ProjectFileEdit.Text = Path.ChangeExtension(source, ".vssprj");
        }
    }

    /// <summary>bttExtractWAVFromVideoClick.</summary>
    private void OnExtractWav(object sender, RoutedEventArgs e) => ShowExtractForm(WavExtractionType.OnlyPeakFile);

    private void OnVideoSourceOperationChanged(object sender, RoutedEventArgs e) => UpdateVideoSourceOperationState();

    private void OnVideoSourceOperationChanged(object sender, SelectionChangedEventArgs e) => UpdateVideoSourceOperationState();

    private void OnVideoSourceOperationExecute(object sender, RoutedEventArgs e)
    {
        if (VideoSourceOperationGeneratePeakFile.IsChecked == true)
        {
            ShowExtractForm(WavExtractionType.OnlyPeakFile);
        }
    }

    /// <summary>ShowExtractForm: abre o diálogo de extração e adota o arquivo gerado.</summary>
    private bool ShowExtractForm(WavExtractionType type)
    {
        if (DelphiText.Trim(VideoFileEdit.Text).Length == 0 || !File.Exists(VideoFileEdit.Text))
        {
            _dialogs.ShowError($"The video file \"{VideoFileEdit.Text}\" doesn't exist.");
            VideoFileEdit.Focus();
            return false;
        }

        string destination = Path.ChangeExtension(VideoFileEdit.Text, ".wav");
        int audioStreamIndex = SelectedAudioStreamIndex();
        if (_dialogs.ShowExtractWav(new ExtractWavRequest(VideoFileEdit.Text, destination, type, audioStreamIndex)) is not { } result)
        {
            return false;
        }

        SelectAudioStream(result.AudioStreamIndex);
        if (result.OnlyPeak)
        {
            PeakFileEdit.Text = Path.ChangeExtension(VideoFileEdit.Text, ".peak");
            WavSelectMode(ProjectWavMode.PeakOnly);
        }
        else
        {
            WavFileEdit.Text = result.DestinationFile;
            WavSelectMode(ProjectWavMode.External);
        }

        UpdateColor();
        return true;
    }

    // ---------------------------------------------------------------- confirmação

    /// <summary>bttCreateNewProjectClick (novo projeto) e bttOkClick (propriedades).</summary>
    private void OnCreateOrOk(object sender, RoutedEventArgs e)
    {
        if (!CheckData(_data.NewProject))
        {
            return;
        }

        if (_data.NewProject && File.Exists(ProjectFileEdit.Text))
        {
            // ask for replacement or cancel
            string message = $"The project file named {ProjectFileEdit.Text} already exist.\r\nDo you want to overwrite this file ?";
            if (!_dialogs.Confirm("Please confirme :", message))
            {
                return;
            }
        }

        ReadFormIntoData();
        DialogResult = true;
    }

    /// <summary>Leva os campos do formulário para o estado devolvido ao chamador.</summary>
    private void ReadFormIntoData()
    {
        _data.VideoSource = VideoFileEdit.Text;
        _data.WavFile = GetWavFilename();
        _data.PeakFile = GetPeakFilename();
        _data.SubtitlesFile = SubtitleFileEdit.Text;
        _data.SubtitlesVO = SubtitleVOEdit.Text;
        _data.ProjectFile = ProjectFileEdit.Text;
        _data.WavMode = GetWavMode();
        _data.AudioStreamIndex = SelectedAudioStreamIndex();
        _data.SaveAsUtf8 = SaveAsUtf8Check.IsChecked == true;
    }

    /// <summary>CheckData: valida a extensão da legenda e o WAV/picos, extraindo quando autorizado.</summary>
    private bool CheckData(bool askForExtraction)
    {
        bool videoFileExists = DelphiText.Trim(VideoFileEdit.Text).Length > 0 && File.Exists(VideoFileEdit.Text);

        // We need a valid extension subtitle filename
        string ext = Path.GetExtension(SubtitleFileEdit.Text).ToLowerInvariant();
        if (DelphiText.Trim(SubtitleFileEdit.Text).Length > 0 && ext is not (".srt" or ".ass" or ".ssa"))
        {
            _dialogs.ShowError("Invalid file extension.");
            SubtitleFileEdit.Focus();
            return false;
        }

        // We need a least subtitle filename
        if (!CheckFileCanBeCreated(SubtitleFileEdit.Text))
        {
            _dialogs.ShowError("The subtitle file name is missing or is invalid.");
            SubtitleFileEdit.Focus();
            return false;
        }

        // External WAV file mode
        if (ExternalRadio.IsChecked == true && !File.Exists(GetWavFilename()))
        {
            if ((askForExtraction || WavFileEdit.Text == LeaveEmpty) && videoFileExists)
            {
                if (!ShowExtractForm(WavExtractionType.FastConversion))
                {
                    return false;
                }
            }
            else
            {
                _dialogs.ShowError($"The WAV file \"{WavFileEdit.Text}\" doesn't exist.");
                WavFileEdit.Focus();
                return false;
            }
        }

        // Peak file only mode
        if (PeakOnlyRadio.IsChecked == true && !File.Exists(GetPeakFilename()))
        {
            if ((askForExtraction || PeakFileEdit.Text == LeaveEmpty) && videoFileExists)
            {
                if (!ShowExtractForm(WavExtractionType.OnlyPeakFile))
                {
                    return false;
                }
            }
            else
            {
                _dialogs.ShowError($"The peak file \"{PeakFileEdit.Text}\" doesn't exist.");
                PeakFileEdit.Focus();
                return false;
            }
        }

        return true;
    }

    /// <summary>CheckFileCanBeCreated: arquivo existente ou que possa ser criado no local.</summary>
    private static bool CheckFileCanBeCreated(string fileName)
    {
        if (File.Exists(fileName))
        {
            return true;
        }

        try
        {
            using var stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read, 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>bttExtractWAVFromVideo.Enabled := WideFileExists(EditVideoFilename.Text).</summary>
    private void UpdateExtractEnabled() => ExtractWavButton.IsEnabled =
        DelphiText.Trim(VideoFileEdit.Text).Length > 0 && File.Exists(VideoFileEdit.Text);

    private async Task RefreshAudioTracksAsync()
    {
        int request = ++_audioAnalysisVersion;
        string source = VideoFileEdit.Text;
        if (!IsLoaded || DelphiText.Trim(VideoFileEdit.Text).Length == 0 || !File.Exists(VideoFileEdit.Text))
        {
            ClearAudioTracks();
            return;
        }

        int previous = _data.AudioStreamIndex;
        if (VideoSourceOperationAudioTracks.SelectedItem is AudioStreamInfo selected)
        {
            previous = selected.Index;
        }
        try
        {
            var streams = await new FFmpegAudioExtractor()
                .GetAudioStreamsAsync(source, CancellationToken.None).ConfigureAwait(true);
            if (request != _audioAnalysisVersion || VideoFileEdit.Text != source) return;
            _audioStreams = streams;
        }
        catch (Exception)
        {
            if (request != _audioAnalysisVersion || VideoFileEdit.Text != source) return;
            ClearAudioTracks();
            return;
        }

        VideoSourceOperationAudioTracks.ItemsSource = _audioStreams;
        VideoSourceOperationAudioTracks.DisplayMemberPath = nameof(AudioStreamInfo.DisplayName);
        VideoSourceOperationAudioTracks.Visibility = _audioStreams.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        VideoSourceOperationAudioTracksOnlyCenter.Visibility = _audioStreams.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SelectAudioStream(Math.Clamp(previous, 0, Math.Max(_audioStreams.Count - 1, 0)));
        if (_audioStreams.Count > 0)
        {
            VideoSourceOperationGeneratePeakFile.IsChecked = true;
        }

        UpdateVideoSourceOperationState();
    }

    private void ClearAudioTracks()
    {
        _audioStreams = [];
        VideoSourceOperationAudioTracks.ItemsSource = null;
        VideoSourceOperationAudioTracks.Visibility = Visibility.Collapsed;
        VideoSourceOperationAudioTracksOnlyCenter.Visibility = Visibility.Collapsed;
        VideoSourceOperationNone.IsChecked = true;
        UpdateVideoSourceOperationState();
    }

    private void UpdateVideoSourceOperationState()
    {
        bool hasVideo = DelphiText.Trim(VideoFileEdit.Text).Length > 0 && File.Exists(VideoFileEdit.Text);
        bool hasAudioTrack = _audioStreams.Count > 0;
        VideoSourceOperationGeneratePeakFile.IsEnabled = hasVideo && hasAudioTrack;
        VideoSourceOperationAudioTracks.IsEnabled = hasVideo && hasAudioTrack;
        VideoSourceOperationExecute.IsEnabled = hasVideo && hasAudioTrack && VideoSourceOperationGeneratePeakFile.IsChecked == true;
    }

    private int SelectedAudioStreamIndex() => VideoSourceOperationAudioTracks.SelectedItem is AudioStreamInfo info ? info.Index : 0;

    private void SelectAudioStream(int audioStreamIndex)
    {
        if (_audioStreams.Count == 0)
        {
            VideoSourceOperationAudioTracks.SelectedIndex = -1;
            return;
        }

        VideoSourceOperationAudioTracks.SelectedIndex = Math.Clamp(audioStreamIndex, 0, _audioStreams.Count - 1);
    }
}
