using System.IO;
using CommunityToolkit.Mvvm.Input;

namespace VisualSubSync.App.ViewModels;

/// <summary>File > Open Recent (TMRUList e OnRecentMenuItemClick) e o projeto aberto ao iniciar (ProcessParams).</summary>
public sealed partial class MainViewModel
{
    /// <summary>A lista mudou: a janela refaz o submenu (FillMenuItem).</summary>
    public event EventHandler? RecentProjectsChanged;

    public IReadOnlyList<string> RecentProjects => Settings.RecentProjects.Files;

    /// <summary>OnRecentMenuItemClick: LoadProject(MenuItem.Caption).</summary>
    [RelayCommand]
    private Task OpenRecentProject(string path) => LoadProject(path);

    /// <summary>OnClearItemClick.</summary>
    [RelayCommand]
    private void ClearRecentProjects()
    {
        Settings.RecentProjects.Clear();
        RecentProjectsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>OnClearDeadEntriesItemClick.</summary>
    [RelayCommand]
    private void ClearDeadRecentProjects()
    {
        Settings.RecentProjects.ClearDeadEntries(File.Exists);
        RecentProjectsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>MRUList.AddFile(CurrentProject.Filename), no fim de LoadProject.</summary>
    private void AddRecentProject(string path)
    {
        Settings.RecentProjects.Add(path);
        RecentProjectsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// ProcessParams: com um argumento, abre o projeto dele (ou o cria a partir de uma legenda); sem
    /// argumento único, abre o projeto mais recente se a preferência pedir e ele existir.
    /// </summary>
    public async Task ProcessParams(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 1)
        {
            string? project = null;
            try
            {
                project = Core.Projects.StartupProject.Resolve(args[0], Settings.DetachedVideo, Settings.ShowVideo);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _dialogs.ShowError(ex.Message);
            }

            if (project is not null)
            {
                await LoadProject(project).ConfigureAwait(true);
            }
        }
        else if (Settings.LoadMostRecentProjectOnStartup && File.Exists(Settings.RecentProjects.MostRecent))
        {
            await LoadProject(Settings.RecentProjects.MostRecent).ConfigureAwait(true);
        }

        LoadAfterParamsSettings();
    }
}
