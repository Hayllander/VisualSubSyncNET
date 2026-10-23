using System.Runtime.InteropServices;

namespace VisualSubSync.App.Controls;

/// <summary>Os cliques que a janela de vídeo destacada usa (TDetachedVideoForm).</summary>
internal enum VideoMouseAction
{
    LeftDown,
    DoubleClick,
    RightUp,
}

/// <summary>
/// A janela nativa onde o mpv desenha (opção "wid"). Ela muda de pai entre o PanelVideo e a janela
/// destacada sem ser recriada, como o <c>VideoRenderer.SetDisplayWindow</c> do original, e o mpv
/// acompanha o tamanho dela. A janela do próprio mpv, filha desta, fica desabilitada: assim o Windows
/// entrega os cliques a esta janela, que os repassa.
/// </summary>
internal sealed partial class VideoSurface
{
    private const string ClassName = "VisualSubSyncVideo";
    private const int WsChild = 0x40000000, WsVisible = 0x10000000, WsClipChildren = 0x02000000;
    private const uint WmSize = 0x0005, WmLButtonDown = 0x0201, WmLButtonDblClk = 0x0203, WmRButtonUp = 0x0205;
    private const uint CsDblClks = 0x0008;
    private const uint SwpNoZOrder = 0x0004, SwpNoActivate = 0x0010;

    private static readonly WndProcDelegate s_wndProc = WndProc;
    private static readonly Dictionary<nint, VideoSurface> s_byHandle = [];
    private static ushort s_atom;

    private readonly bool _fillsChild;

    private VideoSurface(nint parent, bool fillsChild)
    {
        _fillsChild = fillsChild;
        EnsureClass();
        Handle = CreateWindowExW(0, ClassName, string.Empty, WsChild | WsVisible | WsClipChildren,
            0, 0, 1, 1, parent, 0, GetModuleHandleW(null), 0);
        s_byHandle[Handle] = this;
        FitToParent();
    }

    public nint Handle { get; private set; }

    /// <summary>Clique na área do vídeo (vindo da janela do mpv ou desta, quando não há vídeo).</summary>
    public event Action<VideoMouseAction>? Mouse;

    /// <summary>
    /// Uma janela que só segura a superfície e a mantém do seu tamanho (o container do
    /// <see cref="VideoHost"/>).
    /// </summary>
    public static VideoSurface CreateContainer(nint parent) => new(parent, fillsChild: true);

    /// <summary>A superfície em que o mpv desenha.</summary>
    public static VideoSurface CreateSurface(nint parent) => new(parent, fillsChild: false);

    /// <summary>VideoRenderer.SetDisplayWindow: passa a superfície para outra janela e a ajusta ao tamanho dela.</summary>
    public void MoveTo(nint parent)
    {
        if (Handle != 0 && GetParent(Handle) != parent)
        {
            SetParent(Handle, parent);
        }

        FitToParent();
    }

    /// <summary>Ocupa toda a área cliente do pai.</summary>
    public void FitToParent()
    {
        nint parent = Handle == 0 ? 0 : GetParent(Handle);
        if (parent != 0 && GetClientRect(parent, out var r))
        {
            SetWindowPos(Handle, 0, 0, 0, r.Right - r.Left, r.Bottom - r.Top, SwpNoZOrder | SwpNoActivate);
        }
    }

    /// <summary>Desabilita as janelas que o mpv criou dentro da superfície, para os cliques chegarem aqui.</summary>
    public void DisableChildren()
    {
        for (nint child = FindWindowExW(Handle, 0, null, null); child != 0; child = FindWindowExW(Handle, child, null, null))
        {
            if (IsWindowEnabled(child))
            {
                EnableWindow(child, false);
            }
        }
    }

    public void Destroy()
    {
        if (Handle != 0)
        {
            s_byHandle.Remove(Handle);
            DestroyWindow(Handle);
            Handle = 0;
        }
    }

    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (s_byHandle.TryGetValue(hwnd, out var window))
        {
            switch (msg)
            {
                case WmSize when window._fillsChild:
                    nint child = FindWindowExW(hwnd, 0, null, null);
                    if (child != 0)
                    {
                        int width = (int)(lParam & 0xFFFF), height = (int)((lParam >> 16) & 0xFFFF);
                        SetWindowPos(child, 0, 0, 0, width, height, SwpNoZOrder | SwpNoActivate);
                    }

                    break;
                case WmLButtonDown:
                    window.Mouse?.Invoke(VideoMouseAction.LeftDown);
                    return 0;
                case WmLButtonDblClk:
                    window.Mouse?.Invoke(VideoMouseAction.DoubleClick);
                    return 0;
                case WmRButtonUp:
                    window.Mouse?.Invoke(VideoMouseAction.RightUp);
                    return 0;
            }
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    /// <summary>Classe com fundo preto (PanelVideo.Color = clBlack), seta como cursor e duplo clique.</summary>
    private static void EnsureClass()
    {
        if (s_atom != 0)
        {
            return;
        }

        var wc = new WndClassEx
        {
            Size = (uint)Marshal.SizeOf<WndClassEx>(),
            Style = CsDblClks,
            WndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
            Instance = GetModuleHandleW(null),
            Cursor = LoadCursorW(0, 32512),
            Background = GetStockObject(4),
            ClassName = Marshal.StringToHGlobalUni(ClassName),
        };
        s_atom = RegisterClassExW(ref wc);
    }

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WndClassEx
    {
        public uint Size;
        public uint Style;
        public nint WndProc;
        public int ClsExtra;
        public int WndExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public nint MenuName;
        public nint ClassName;
        public nint IconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial ushort RegisterClassExW(ref WndClassEx wc);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint CreateWindowExW(int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    private static partial nint DefWindowProcW(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial nint SetParent(nint child, nint parent);

    [LibraryImport("user32.dll")]
    private static partial nint GetParent(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint hwnd, out Rect rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowExW(nint parent, nint childAfter, string? className, string? windowName);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowEnabled(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnableWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool enable);

    [LibraryImport("user32.dll")]
    private static partial nint LoadCursorW(nint instance, nint cursorName);

    [LibraryImport("gdi32.dll")]
    private static partial nint GetStockObject(int obj);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandleW(string? moduleName);
}
