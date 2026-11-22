using System.Windows;
using System.Windows.Threading;
using VisualSubSync.Core.Projects;

namespace VisualSubSync.App.Views;

/// <summary>
/// Arrastar e soltar arquivos na janela principal (recurso novo, sem equivalente no TMainForm).
/// Os eventos Preview pegam a soltura antes das caixas de texto, que recusariam arquivos;
/// arrastes de texto continuam indo para elas.
/// </summary>
public partial class MainWindow
{
    private void InitializeFileDrop()
    {
        AllowDrop = true;
        PreviewDragEnter += OnFileDragOver;
        PreviewDragOver += OnFileDragOver;
        PreviewDrop += OnFileDrop;
    }

    private static string[]? DroppedPaths(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) ? e.Data.GetData(DataFormats.FileDrop) as string[] : null;

    private static bool CanOpen(string[]? paths) => paths is not null && paths.Any(DroppedFiles.IsSupported);

    private void OnFileDragOver(object sender, DragEventArgs e)
    {
        if (DroppedPaths(e) is { } paths)
        {
            e.Effects = CanOpen(paths) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (DroppedPaths(e) is not { } paths)
        {
            return;
        }

        e.Handled = true;
        if (!CanOpen(paths))
        {
            return;
        }

        // Os diálogos (salvar alterações, novo projeto) só abrem depois que o Explorer
        // recebe a resposta da soltura; senão ele fica preso até o diálogo fechar.
        Activate();
        _ = Dispatcher.InvokeAsync(() => _viewModel.OpenDroppedFiles(paths), DispatcherPriority.Background);
    }
}
