using System.Windows;
using System.Windows.Threading;
using VisualSubSync.App.ViewModels;
using VisualSubSync.Core.Settings;

namespace VisualSubSync.App.Views;

/// <summary>DetachedVideoForm: criada com a janela principal e escondida até o vídeo ser destacado.</summary>
public partial class MainWindow
{
    private DetachedVideoWindow? _detachedVideo;
    private readonly DispatcherTimer _videoChildTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    private void InitializeDetachedVideo(MainViewModel viewModel)
    {
        var window = new DetachedVideoWindow();
        _detachedVideo = window;
        FormPlacement.Restore(window, viewModel.Settings, "DetachedVideoForm");
        window.DoubleClicked += (_, _) => viewModel.DetachVideoCommand.Execute(null);
        window.NormalPosChanged += (_, _) =>
        {
            var p = window.NormalPos;
            viewModel.SetVideoWindowNormal(new VideoWindowBounds(p.X, p.Y, p.Width, p.Height));
        };
        Loaded += (_, _) => window.Owner = this;

        // As janelas que o mpv cria ficam desabilitadas, para os cliques chegarem à superfície.
        _videoChildTimer.Tick += (_, _) => Video.Surface?.DisableChildren();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsVideoDetached))
            {
                ApplyVideoPanelVisibility();
            }
            else if (e.PropertyName == nameof(MainViewModel.VideoWindowNormal))
            {
                var b = viewModel.VideoWindowNormal;
                window.SetNormalPos(new Int32Rect(b.Left, b.Top, b.Width, b.Height));
            }
        };
    }

    /// <summary>
    /// ActionDetachVideoExecute / ActionShowHideVideoExecute: o vídeo vai para a janela destacada ou
    /// volta para o PanelVideo, e só o lugar em uso aparece enquanto o vídeo está à mostra.
    /// </summary>
    private void PlaceVideoSurface(bool showing, bool detached)
    {
        if (_detachedVideo is not { } window)
        {
            return;
        }

        if (Video.Surface is { } surface)
        {
            if (detached)
            {
                window.Adopt(surface);
            }
            else
            {
                window.Release();
                surface.MoveTo(Video.ContainerHandle);
            }
        }

        if (showing && detached)
        {
            window.ShowNoActivate();
        }
        else
        {
            window.Hide();
        }

        _videoChildTimer.IsEnabled = showing;
    }

    /// <summary>SaveSettings: SaveFormPosition(DetachedVideoForm) e [Windows] DetachedVideo/ShowVideo.</summary>
    private void SaveDetachedVideoSettings(VssSettings settings)
    {
        settings.DetachedVideo = _viewModel.IsVideoDetached;
        settings.ShowVideo = _viewModel.ShowingVideo;
        if (_detachedVideo is { } window)
        {
            if (!double.IsNaN(window.Left) && !double.IsNaN(window.Top))
            {
                FormPlacement.Store(window, settings, "DetachedVideoForm");
            }

            _videoChildTimer.Stop();
            window.CloseForGood();
        }
    }
}
