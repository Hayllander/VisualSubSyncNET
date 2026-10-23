using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace VisualSubSync.Poc.Player;

/// <summary>
/// Janela nativa filha onde o mpv desenha (opção "wid"). Por causa do "airspace" do WPF,
/// nada em WPF pode ser desenhado por cima desta área; legendas vêm do próprio mpv.
/// </summary>
public sealed partial class VideoHost : HwndHost
{
    private const int WsChild = 0x40000000, WsVisible = 0x10000000, WsClipChildren = 0x02000000;

    public nint VideoHandle { get; private set; }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        VideoHandle = CreateWindowExW(0, "static", string.Empty, WsChild | WsVisible | WsClipChildren,
            0, 0, 1, 1, hwndParent.Handle, 0, 0, 0);
        return new HandleRef(this, VideoHandle);
    }

    protected override void DestroyWindowCore(HandleRef hwnd) => DestroyWindow(hwnd.Handle);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint CreateWindowExW(int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hwnd);
}
