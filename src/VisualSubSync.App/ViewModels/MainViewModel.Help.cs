using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.Input;

namespace VisualSubSync.App.ViewModels;

/// <summary>O menu "?" de main.pas: MenuItemHelpIndexClick, MenuItemHelpIndexWAVDisplayControlClick e MenuItemAboutClick.</summary>
public sealed partial class MainViewModel
{
    /// <summary>MenuItemHelpIndexClick: help\index.html ao lado do executável, no programa padrão.</summary>
    [RelayCommand]
    private static void HelpIndex() => OpenHelpPage("index.html");

    /// <summary>MenuItemHelpIndexWAVDisplayControlClick.</summary>
    [RelayCommand]
    private static void HelpWavDisplayControl() => OpenHelpPage("wavdisplaycontrol.html");

    /// <summary>MenuItemAboutClick: TAboutForm.ShowModal.</summary>
    [RelayCommand]
    private void About() => _dialogs.ShowAbout(ApplicationVersion);

    /// <summary>Tnt_ShellExecuteW(0, 'open', ...): sem mensagem quando o arquivo não abre, como no original.</summary>
    private static void OpenHelpPage(string page)
    {
        string fileName = Path.Combine(AppContext.BaseDirectory, "help", page);
        try
        {
            Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Trace.TraceWarning($"Could not open {fileName}: {ex.Message}");
        }
    }
}
