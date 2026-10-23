using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VisualSubSync.App.ViewModels;

/// <summary>A posição normal (fora da tela cheia) da janela de vídeo destacada, em pixels.</summary>
public readonly record struct VideoWindowBounds(int Left, int Top, int Width, int Height);

/// <summary>ActionDetachVideo de main.pas e os valores do TDetachedVideoForm gravados no projeto.</summary>
public sealed partial class MainViewModel
{
    /// <summary>MenuItemDetachVideoWindow.Checked: o vídeo fica na janela destacada em vez do PanelVideo.</summary>
    [ObservableProperty]
    private bool _isVideoDetached;

    /// <summary>
    /// DetachedVideoForm.NormalLeft/Top/Width/Height: vêm do projeto quando o vídeo abre
    /// (SetNormalPos) e vão para o projeto em cada SaveProject; começam em 0, 0, 320, 240 (CreateParams).
    /// </summary>
    [ObservableProperty]
    private VideoWindowBounds _videoWindowNormal = new(0, 0, 320, 240);

    /// <summary>ActionDetachVideoExecute: alterna e grava DetachedVideo no projeto.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void DetachVideo()
    {
        IsVideoDetached = !IsVideoDetached;
        if (Project is { } project && project.DetachedVideo != IsVideoDetached)
        {
            project.DetachedVideo = IsVideoDetached;
            SaveProject(project, silent: true);
        }
    }

    /// <summary>A janela destacada guardou outra posição normal ao entrar em tela cheia.</summary>
    public void SetVideoWindowNormal(VideoWindowBounds bounds) => VideoWindowNormal = bounds;
}
