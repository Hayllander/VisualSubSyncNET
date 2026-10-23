using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using VisualSubSync.App.Controls;

namespace VisualSubSync.App.Views;

/// <summary>
/// TDetachedVideoForm (DetachedVideoFormUnit.pas): janela sem título, sempre por cima, com borda de
/// redimensionar e fundo preto. Arrastar com o botão esquerdo move a janela, o duplo clique volta o
/// vídeo para o painel e o menu do botão direito alterna entre tela cheia e tamanho normal.
/// </summary>
internal sealed partial class DetachedVideoWindow : Window
{
    private const uint WmSize = 0x0005, WmSysCommand = 0x0112, ScDragMove = 0xF012;
    private const int GwlStyle = -16, WsClipChildren = 0x02000000;
    private const uint SwpNoZOrder = 0x0004, SwpNoActivate = 0x0010;

    private readonly MenuItem _pmiVideoFullscreen = new() { Header = "Fullscreen" };
    private readonly MenuItem _pmiVideoNormalSize = new() { Header = "Normal size" };
    private VideoSurface? _surface;
    private bool _closing;

    public DetachedVideoWindow()
    {
        Title = "Video";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Background = Brushes.Black;
        Width = 320;
        Height = 240;
        Handle = new WindowInteropHelper(this).EnsureHandle();
        HwndSource.FromHwnd(Handle)?.AddHook(OnMessage);
        SetWindowLongPtrW(Handle, GwlStyle, GetWindowLongPtrW(Handle, GwlStyle) | WsClipChildren);

        _pmiVideoFullscreen.Click += (_, _) => Fullscreen();
        _pmiVideoNormalSize.Click += (_, _) => NormalSize();
        ContextMenu = new ContextMenu { Items = { _pmiVideoFullscreen, _pmiVideoNormalSize } };
        ContextMenu.Opened += (_, _) => VideoPopupMenuPopup();
    }

    public nint Handle { get; }

    /// <summary>FNormalLeft/Top/Width/Height, em pixels: a posição a restaurar ao sair da tela cheia.</summary>
    public Int32Rect NormalPos { get; private set; } = new(0, 0, 320, 240);

    /// <summary>Muda quando a tela cheia guarda uma nova posição normal (gravada no projeto).</summary>
    public event EventHandler? NormalPosChanged;

    /// <summary>FormDblClick: MainForm.ActionDetachVideo.Execute.</summary>
    public event EventHandler? DoubleClicked;

    public void SetNormalPos(Int32Rect pos) => NormalPos = pos;

    /// <summary>VideoRenderer.SetDisplayWindow(DetachedVideoForm.Handle).</summary>
    public void Adopt(VideoSurface surface)
    {
        if (_surface != surface)
        {
            if (_surface is not null)
            {
                _surface.Mouse -= OnVideoMouse;
            }

            _surface = surface;
            surface.Mouse += OnVideoMouse;
        }

        surface.MoveTo(Handle);
    }

    /// <summary>A superfície voltou para o painel: os cliques dela deixam de valer aqui.</summary>
    public void Release()
    {
        if (_surface is not null)
        {
            _surface.Mouse -= OnVideoMouse;
            _surface = null;
        }
    }

    /// <summary>ShowWindow(SW_SHOWNOACTIVATE): mostra sem tirar o foco da janela principal.</summary>
    public void ShowNoActivate()
    {
        if (!IsVisible)
        {
            Show();
        }
    }

    /// <summary>Fecha de verdade (fim do programa); antes disso, fechar só esconde, como no Delphi.</summary>
    public void CloseForGood()
    {
        _closing = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnClosing(e);
        if (!_closing)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void OnVideoMouse(VideoMouseAction action)
    {
        switch (action)
        {
            // FormMouseDown: ReleaseCapture e WM_SYSCOMMAND/SC_DRAGMOVE.
            case VideoMouseAction.LeftDown:
                Dispatcher.BeginInvoke(() =>
                {
                    ReleaseCapture();
                    SendMessageW(Handle, WmSysCommand, (nint)ScDragMove, 0);
                });
                break;
            case VideoMouseAction.DoubleClick:
                Dispatcher.BeginInvoke(() => DoubleClicked?.Invoke(this, EventArgs.Empty));
                break;
            case VideoMouseAction.RightUp:
                Dispatcher.BeginInvoke(() =>
                {
                    ContextMenu.PlacementTarget = this;
                    ContextMenu.Placement = PlacementMode.MousePoint;
                    ContextMenu.IsOpen = true;
                });
                break;
        }
    }

    private nint OnMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmSize)
        {
            _surface?.FitToParent();
        }

        return 0;
    }

    /// <summary>VideoPopupMenuPopup.</summary>
    private void VideoPopupMenuPopup()
    {
        bool fullscreen = IsFullscreen();
        _pmiVideoNormalSize.IsChecked = !fullscreen;
        _pmiVideoNormalSize.IsEnabled = fullscreen;
        _pmiVideoFullscreen.IsChecked = fullscreen;
        _pmiVideoFullscreen.IsEnabled = !fullscreen;
    }

    /// <summary>pmiVideoFullscreenClick: guarda a posição normal e ocupa o monitor da janela.</summary>
    private void Fullscreen()
    {
        if (IsFullscreen() || !GetWindowRect(Handle, out var r) || MonitorRect() is not { } monitor)
        {
            return;
        }

        NormalPos = new Int32Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        NormalPosChanged?.Invoke(this, EventArgs.Empty);
        SetBounds(monitor.Left, monitor.Top, monitor.Right - monitor.Left, monitor.Bottom - monitor.Top);
    }

    /// <summary>pmiVideoNormalSizeClick: volta à posição guardada (320x240 se não houver tamanho).</summary>
    private void NormalSize()
    {
        if (!IsFullscreen())
        {
            return;
        }

        var p = NormalPos;
        SetBounds(p.X, p.Y, p.Width == 0 ? 320 : p.Width, p.Height == 0 ? 240 : p.Height);
    }

    /// <summary>IsFullscreen: a janela tem exatamente os limites do monitor.</summary>
    private bool IsFullscreen() =>
        GetWindowRect(Handle, out var r) && MonitorRect() is { } m &&
        r.Left == m.Left && r.Top == m.Top && r.Right == m.Right && r.Bottom == m.Bottom;

    private Rect32? MonitorRect()
    {
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfoW(MonitorFromWindow(Handle, 2), ref info) ? info.Monitor : null;
    }

    private void SetBounds(int left, int top, int width, int height) =>
        SetWindowPos(Handle, 0, left, top, width, height, SwpNoZOrder | SwpNoActivate);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect32
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public Rect32 Monitor;
        public Rect32 Work;
        public uint Flags;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReleaseCapture();

    [LibraryImport("user32.dll")]
    private static partial nint SendMessageW(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hwnd, out Rect32 rect);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    private static partial nint GetWindowLongPtrW(nint hwnd, int index);

    [LibraryImport("user32.dll")]
    private static partial nint SetWindowLongPtrW(nint hwnd, int index, nint value);
}
