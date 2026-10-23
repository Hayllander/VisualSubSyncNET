using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Audio;
using VisualSubSync.Core.Web;

namespace VisualSubSync.App.ViewModels;

/// <summary>ActionExportToWAV.</summary>
public sealed partial class MainViewModel
{
    /// <summary>ServerRootDir: a pasta web ao lado do executável.</summary>
    public static string ServerRootDir => Path.Combine(AppContext.BaseDirectory, "web");

    /// <summary>g_ApplicationVersion.VersionString.</summary>
    public static string ApplicationVersion
    {
        get
        {
            string? version = typeof(MainViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return version is null ? string.Empty : version.Split('+')[0];
        }
    }

    /// <summary>
    /// ActionExportToWAVExecute: pede uma pasta e grava nela um WAV por legenda, recortado do WAV do
    /// projeto, e o index_wav.html que lista todos.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task ExportToWav()
    {
        if (Project is not { } project)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(project.WavFile))
        {
            _dialogs.ShowWarning("You need to create a project with a WAV file");
            return;
        }

        if (_dialogs.SelectFolder("Select the folder where to place wav files") is not { } folder)
        {
            return;
        }

        using var wav = WavFile.Open(project.WavFile);
        if (wav is null)
        {
            return;
        }

        if (wav.IsDamaged)
        {
            _dialogs.ShowWarning($"Warning, {project.WavFile} is damaged, loading may not work correctly.");
        }

        using (_dialogs.WaitCursor())
        {
            ShowStatusBarMessage("Exporting to wav files, please wait...");
            var context = new PageContext(ApplicationVersion, project.FileName, [.. _document.Items], wav.AverageBytesPerSecond);
            try
            {
                await Task.Run(() => WavExport.Export(wav, folder, context, Path.Combine(ServerRootDir, WavExport.TemplateName)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _dialogs.ShowError(ex.Message);
            }
        }
    }
}
