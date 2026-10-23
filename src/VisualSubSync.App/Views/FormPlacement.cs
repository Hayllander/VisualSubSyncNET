using System.Windows;
using VisualSubSync.Core.Settings;

namespace VisualSubSync.App.Views;

/// <summary>LoadFormPosition / SaveFormPosition (main.pas) para as janelas auxiliares.</summary>
internal static class FormPlacement
{
    /// <summary>
    /// Aplica a posição gravada (e o tamanho, nas janelas redimensionáveis); uma janela fora de todas as
    /// telas vai para 100,100, como no original.
    /// </summary>
    public static void Restore(Window window, VssSettings settings, string formName)
    {
        if (settings.GetFormPlacement(formName) is { } p)
        {
            Apply(window, p);
        }
    }

    /// <summary>LoadFormPosition com a posição já lida (a janela principal guarda a sua em MainForm).</summary>
    public static void Apply(Window window, WindowPlacement p)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        bool sizable = window.SizeToContent == SizeToContent.Manual;
        double width = sizable && p.Width > 0 ? p.Width : window.Width;
        double height = sizable && p.Height > 0 ? p.Height : window.Height;
        if (sizable)
        {
            window.Width = width;
            window.Height = height;
        }

        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        screen.Inflate(-10, -10);
        var bounds = new Rect(p.Left, p.Top, double.IsNaN(width) ? 1 : width, double.IsNaN(height) ? 1 : height);
        bool visible = screen.IntersectsWith(bounds);
        window.Left = visible ? p.Left : 100;
        window.Top = visible ? p.Top : 100;
        // TWindowState(_State): 0 normal, 1 minimizada, 2 maximizada.
        window.WindowState = p.State switch
        {
            2 => WindowState.Maximized,
            1 => WindowState.Minimized,
            _ => WindowState.Normal,
        };
    }

    /// <summary>SaveFormPosition: a posição normal (GetWindowPlacement) e o estado.</summary>
    public static void Store(Window window, VssSettings settings, string formName)
    {
        var bounds = window.RestoreBounds.IsEmpty ? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight) : window.RestoreBounds;
        int state = window.WindowState switch
        {
            WindowState.Maximized => 2,
            WindowState.Minimized => 1,
            _ => 0,
        };
        settings.SetFormPlacement(formName, new WindowPlacement((int)bounds.Left, (int)bounds.Top, (int)bounds.Width, (int)bounds.Height, state));
    }
}
