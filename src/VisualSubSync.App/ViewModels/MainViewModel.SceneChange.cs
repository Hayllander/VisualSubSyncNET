using System.IO;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Scenes;

namespace VisualSubSync.App.ViewModels;

/// <summary>
/// Inserir e apagar mudanças de cena pela onda (ActionInsertSceneChange, ActionDeleteSceneChange,
/// TMainForm.InsertSceneChange/DeleteSceneChange): a lista muda, a onda redesenha e, se o .scenechange
/// do vídeo existir, ele é regravado.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>ActionInsertSceneChangeExecute: uma mudança de cena no cursor, se ainda não houver.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void InsertSceneChangeAtCursor()
    {
        int cursor = CursorMs;
        if (SceneChangeList.Contains(SceneChanges ?? [], cursor, cursor))
        {
            return;
        }

        DoAndPush(new InsertSceneChangeTask(this, cursor));
    }

    /// <summary>ActionDeleteSceneChangeExecute: apaga as mudanças de cena dentro da seleção.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void DeleteSceneChangesInSelection()
    {
        if (SelectionIsEmpty || !SceneChangeList.Contains(SceneChanges ?? [], SelectionStartMs, SelectionStopMs))
        {
            return;
        }

        DoAndPush(new DeleteSceneChangeTask(this, SelectionStartMs, SelectionStopMs));
    }

    /// <summary>TMainForm.InsertSceneChange.</summary>
    internal void InsertSceneChanges(IEnumerable<int> times)
    {
        var list = (SceneChanges ?? []).ToList();
        SceneChangeList.Insert(list, times);
        SetSceneChanges(list);
    }

    /// <summary>TMainForm.DeleteSceneChange: devolve as mudanças de cena apagadas.</summary>
    internal int[] DeleteSceneChanges(int startMs, int stopMs)
    {
        var list = (SceneChanges ?? []).ToList();
        int[] deleted = SceneChangeList.Delete(list, startMs, stopMs);
        SetSceneChanges(list);
        return deleted;
    }

    private void SetSceneChanges(List<int> list)
    {
        SceneChanges = list.ToArray();
        if (Project is null || DelphiTrim(Project.VideoSource).Length == 0)
        {
            return;
        }

        string path = Path.ChangeExtension(Project.VideoSource, SceneChangeFile.Extension);
        if (File.Exists(path))
        {
            try
            {
                SceneChangeFile.Save(path, SceneChanges);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceWarning($"Scene changes not saved to {path}: {ex.Message}");
            }
        }
    }
}
