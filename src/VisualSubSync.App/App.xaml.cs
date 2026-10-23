using System.Windows;
using VisualSubSync.App.Services;
using VisualSubSync.App.ViewModels;
using VisualSubSync.App.Views;
using VisualSubSync.Core.Settings;

namespace VisualSubSync.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnStartup(e);

        // GetIniFilename: VisualSubSync.ini ao lado do executável (ou em %APPDATA%).
        string exePath = Environment.ProcessPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "VisualSubSyncNET.exe");
        var settings = VssSettings.LoadForExecutable(exePath);

        var viewModel = new MainViewModel(new DialogService(settings), settings);
        viewModel.InitGeneralPlugin();
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();

        // TMainForm.ProcessParams.
        _ = viewModel.ProcessParams(e.Args);

        // TimerChkUpdates: a procura de versão nova, quando ligada nas preferências.
        _ = viewModel.CheckUpdatesOnStartup();
    }
}
