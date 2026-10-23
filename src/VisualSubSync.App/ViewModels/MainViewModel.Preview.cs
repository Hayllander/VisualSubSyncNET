using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VisualSubSync.App.ViewModels;

/// <summary>
/// Legenda sobre o vídeo: CurrentProject.IsDirty com o evento OnDirtySet
/// (CurrentProjectOnDirtySet), VideoPreviewNeedSubtitleUpdate e ActionShowHideSubs.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>VideoPreviewNeedSubtitleUpdate: a cópia temporária das legendas está atrasada.</summary>
    private bool _videoPreviewNeedSubtitleUpdate;

    /// <summary>DisableVideoUpdatePreview: ligado durante o arraste de uma borda na onda.</summary>
    private bool _disableVideoUpdatePreview;

    /// <summary>ShowingSubs (MenuShowHideSubtitles.Checked).</summary>
    [ObservableProperty]
    private bool _showingSubs = true;

    /// <summary>
    /// CurrentProject.IsDirty (TVSSProject.SetIsDirty): cada vez que passa a verdadeiro, mesmo já sendo,
    /// a legenda do vídeo é atualizada (CurrentProjectOnDirtySet).
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
                SaveCommand.NotifyCanExecuteChanged();
            }

            if (value)
            {
                UpdateSubtitleForPreview(false);
                _videoPreviewNeedSubtitleUpdate = true;
                CallJsOnSubtitleModification();
            }
        }
    }

    /// <summary>
    /// ActionShowHideSubsExecute: mostra ou esconde a legenda no vídeo; com o vídeo à mostra, atualiza a
    /// legenda (tocando ou pausado) ou mostra o quadro do início da legenda focada.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ShowHideSubs()
    {
        ShowingSubs = !ShowingSubs;
        if (_engine is { IsOpen: true } engine)
        {
            engine.SubtitlesVisible = ShowingSubs;
        }

        if (ShowingVideo)
        {
            if (_engine is { IsPlaying: true } || (IsPlaying && !_playTimer.IsEnabled))
            {
                UpdateSubtitleForPreview(true);
            }
            else
            {
                ShowStartFrame();
            }
        }
    }
}
