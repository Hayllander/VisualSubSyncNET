using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace VisualSubSync.App.Controls;

/// <summary>
/// O PanelVideo: uma janela nativa filha que segura a <see cref="VideoSurface"/> onde o mpv desenha
/// (opção "wid"), no lugar do antigo <c>VideoRenderer.SetDisplayWindow(PanelVideo.Handle)</c>. Por causa
/// do "airspace" do WPF, nada em WPF pode ser desenhado por cima desta área; as legendas vêm do próprio mpv.
/// </summary>
public sealed class VideoHost : HwndHost
{
    private VideoSurface? _container;

    /// <summary>A superfície do vídeo; criada junto com o container e levada para a janela destacada quando preciso.</summary>
    internal VideoSurface? Surface { get; private set; }

    /// <summary>HWND passado ao motor de mídia. Só existe depois que o controle é apresentado.</summary>
    public nint VideoHandle => Surface?.Handle ?? 0;

    /// <summary>HWND do container, para onde a superfície volta quando o vídeo deixa de ser destacado.</summary>
    internal nint ContainerHandle => _container?.Handle ?? 0;

    /// <summary>Avisa que o HWND existe; a janela usa para ligar o motor de mídia.</summary>
    public event EventHandler? HandleCreated;

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _container = VideoSurface.CreateContainer(hwndParent.Handle);
        Surface = VideoSurface.CreateSurface(_container.Handle);
        HandleCreated?.Invoke(this, EventArgs.Empty);
        return new HandleRef(this, _container.Handle);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        Surface?.Destroy();
        Surface = null;
        _container?.Destroy();
        _container = null;
    }
}
