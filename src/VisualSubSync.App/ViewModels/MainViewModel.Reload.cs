using System.IO;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Subtitles.Formats;
using VisualSubSync.Core.Text;

namespace VisualSubSync.App.ViewModels;

/// <summary>ActionReload, ActionOpenProjectFolder e ActionInsertTextFile.</summary>
public sealed partial class MainViewModel
{
    /// <summary>
    /// ActionReloadExecute: depois de perguntar se salva, relê as legendas do disco, volta a focar a
    /// mesma posição, limpa o desfazer e deixa o projeto sem alterações.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void Reload()
    {
        if (Project is not { } project || !CheckSubtitlesAreSaved())
        {
            return;
        }

        int index = SelectedSubtitle is { } item ? Subtitles.IndexOf(item) : -1;
        SubtitleDocument reloaded;
        try
        {
            reloaded = LoadDocument(project.SubtitlesFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _dialogs.ShowError($"Could not reload \"{project.SubtitlesFile}\".\n{ex.Message}");
            return;
        }
        if (File.Exists(project.SubtitlesVO))
        {
            ShowStatusBarMessage("Reloading VO reference subtitles...");
            LoadVO(project.SubtitlesVO);
        }

        ShowStatusBarMessage("Reloading subtitles...");
        _document = reloaded;
        LoadSubtitles(_document);
        ShowStyleColumn = SubtitleFile.HasStyles(project.SubtitlesFile);
        SelectNodeAtIndex(index);
        ClearUndo();
        IsDirty = false;
    }

    /// <summary>ActionOpenProjectFolderExecute: abre a pasta do projeto no Explorer.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void OpenProjectFolder()
    {
        if (Project is { } project)
        {
            _dialogs.ExploreFolder(Path.GetDirectoryName(project.FileName) ?? string.Empty);
        }
    }

    /// <summary>
    /// ActionInsertTextFileExecute: cada arquivo escolhido entra numa TUndoableMultiAddTask; um .srt com
    /// os próprios tempos, os outros uma legenda por linha depois da última.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void InsertTextFile()
    {
        foreach (string path in _dialogs.OpenFiles(InsertedFile.Filter))
        {
            List<Subtitle> subs;
            try
            {
                subs = InsertedFile.IsSrt(path)
                    ? InsertedFile.ReadSrt(path)
                    : InsertedFile.FromLines(TextFileReader.ReadAllText(path).Text, _document.Items.Count > 0 ? _document.Items[^1].StopMs : null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _dialogs.ShowError(ex.Message);
                continue;
            }

            if (subs.Count > 0)
            {
                DoAndPush(new MultiAddTask(this, subs));
                IsDirty = true;
            }
        }
    }
}
