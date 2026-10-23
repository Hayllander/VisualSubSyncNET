using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.App.Services;
using VisualSubSync.Core.Projects;
using VisualSubSync.Core.Text;
using VisualSubSync.Core.TextPipe;
using VisualSubSync.Core.Undo;

namespace VisualSubSync.App.ViewModels;

/// <summary>
/// Text pipe (main.pas): mostrar/esconder, carregar, salvar e limpar o MemoTextPipe, criar ou trocar
/// legendas com o trecho selecionado e o .vssscript gravado ao lado do projeto.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>MemoTextPipe.Visible (e SplitterMemoTextPipe).</summary>
    [ObservableProperty]
    private bool _isTextPipeVisible;

    /// <summary>A opção marcada no menu do text pipe (GetTextPipeAutoOption).</summary>
    [ObservableProperty]
    private TextPipeAutoOption _textPipeAutoOption;

    /// <summary>CurrentProject.TextPipeSource quando não há projeto aberto.</summary>
    private string _textPipeSource = string.Empty;

    public ITextPipeView? TextPipe { get; set; }

    private string TextPipeSource
    {
        get => Project?.TextPipeSource ?? _textPipeSource;
        set
        {
            _textPipeSource = value;
            if (Project is { } project)
            {
                project.TextPipeSource = value;
            }
        }
    }

    /// <summary>pmiAddAsSubtitle.Enabled.</summary>
    public bool CanAddSubFromPipe => TextPipe is { SelLength: > 0 } && HasProject && !SelectionIsEmpty;

    /// <summary>pmiReplaceSubtitleFromPipe.Enabled.</summary>
    public bool CanReplaceFromPipe => TextPipe is { SelLength: > 0 } && SelectedSubtitle is not null;

    partial void OnIsTextPipeVisibleChanged(bool value) => Settings.ShowTextPipe = value;

    partial void OnTextPipeAutoOptionChanged(TextPipeAutoOption value) => Settings.TextPipeAutoOption = (int)value;

    /// <summary>ActionShowHideTextPipeExecute.</summary>
    [RelayCommand]
    private void ShowHideTextPipe() => IsTextPipeVisible = !IsTextPipeVisible;

    /// <summary>ActionLoadTextPipeExecute: texto, RTF ou o texto das legendas de um SRT.</summary>
    [RelayCommand]
    private void LoadTextPipe()
    {
        if (TextPipe is not { } pipe || _dialogs.OpenFileWithFilter(TextPipeFile.OpenFilter) is not { } choice)
        {
            return;
        }

        try
        {
            pipe.Clear();
            if (choice.FilterIndex == TextPipeFile.StrippedFilterIndex)
            {
                pipe.SetText(TextPipeFile.ReadStrippedSrt(choice.Path));
                pipe.Select(0, 0);
            }
            else
            {
                LoadTextPipeFile(pipe, choice.Path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _dialogs.ShowError($"Could not open \"{choice.Path}\".\n{ex.Message}");
            return;
        }

        TextPipeSource = choice.Path;
        if (Project is { } project)
        {
            SaveProject(project, silent: true);
        }

        IsTextPipeVisible = true;
    }

    /// <summary>ActionClearTextPipeExecute.</summary>
    [RelayCommand]
    private void ClearTextPipe() => TextPipe?.Clear();

    /// <summary>ActionSaveTextPipeAsExecute: texto simples (filtro 1) ou RTF (filtro 2).</summary>
    [RelayCommand]
    private void SaveTextPipeAs()
    {
        if (TextPipe is not { } pipe || _dialogs.SaveFile(TextPipeFile.SaveFilter, TextPipeSource, 1) is not { } choice)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(choice.Path, choice.FilterIndex == 1 ? TextPipeFile.EncodePlainText(pipe.Text) : pipe.SaveRtf());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Could not save \"{choice.Path}\".\n{ex.Message}");
            return;
        }

        TextPipeSource = choice.Path;
        if (Project is { } project)
        {
            SaveProject(project, silent: true);
        }
    }

    /// <summary>ActionAddSubFromPipeExecute: nova legenda com a seleção da onda e o trecho do pipe.</summary>
    [RelayCommand]
    private void AddSubFromPipe()
    {
        if (!CanAddSubFromPipe || TextPipe is not { } pipe)
        {
            return;
        }

        var add = new AddTask(this, SelectionStartMs, SelectionStopMs, autoSelect: true, DelphiText.Trim(SelectedPipeText(pipe)));
        DoAndPush(new CompositeTask(add, ColorizeOrDeleteTextPipe(pipe)));
        if (!IsTimingMode && IsSubtitleEditionEnabled)
        {
            EditorFocusRequested?.Invoke(this, false);
        }
    }

    /// <summary>
    /// ActionReplaceFromPipeExecute: o texto da legenda focada vira o trecho do pipe e a próxima
    /// legenda é selecionada.
    /// </summary>
    [RelayCommand]
    private void ReplaceFromPipe()
    {
        if (!CanReplaceFromPipe || TextPipe is not { } pipe || SelectedSubtitle is not { } item)
        {
            return;
        }

        int index = Subtitles.IndexOf(item);
        var change = new MultiChangeTask(this, [new TextChange(index, item.Text, DelphiText.Trim(SelectedPipeText(pipe)))]);
        DoAndPush(new CompositeTask(change, ColorizeOrDeleteTextPipe(pipe)));
        if (index + 1 < Subtitles.Count)
        {
            SelectSubtitleAt(index + 1);
        }
    }

    /// <summary>ColorizeOrDeleteTextPipe.</summary>
    private TextPipeTask ColorizeOrDeleteTextPipe(ITextPipe pipe) =>
        new(pipe, TextPipeAutoOption, TextPipeTask.ColorFor(Subtitles.Count));

    private static string SelectedPipeText(ITextPipe pipe)
    {
        string text = pipe.Text;
        int start = Math.Clamp(pipe.SelStart, 0, text.Length);
        return text.Substring(start, Math.Clamp(pipe.SelLength, 0, text.Length - start));
    }

    /// <summary>Lines.LoadFromFile: RTF quando o arquivo começa por "{\rtf", senão texto.</summary>
    private static void LoadTextPipeFile(ITextPipeView pipe, string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (TextPipeFile.IsRtf(bytes))
        {
            pipe.LoadRtf(bytes);
        }
        else
        {
            pipe.SetText(TextPipeFile.ReadPlainText(bytes));
        }
    }

    /// <summary>LoadProject, "Load script": o .vssscript e a posição salva, com a linha no alto.</summary>
    private void LoadProjectScript(VssProject project)
    {
        string script = Path.ChangeExtension(project.FileName, VssProjectFile.TextPipeExtension);
        if (TextPipe is not { } pipe || !File.Exists(script))
        {
            return;
        }

        ShowStatusBarMessage("Loading script file...");
        try
        {
            LoadTextPipeFile(pipe, script);
            pipe.ScrollToPosition(project.TextPipePosition);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            System.Diagnostics.Trace.TraceWarning($"Could not load {script}: {ex.Message}");
        }
    }

    /// <summary>SaveProject: com texto no pipe, grava o .vssscript (RTF) e a posição do cursor.</summary>
    private bool SaveProjectScript(VssProject project)
    {
        if (TextPipe is not { } pipe || DelphiText.Trim(pipe.Text).Length == 0)
        {
            return false;
        }

        File.WriteAllBytes(Path.ChangeExtension(project.FileName, VssProjectFile.TextPipeExtension), pipe.SaveRtf());
        project.TextPipePosition = pipe.SelStart;
        return true;
    }
}
