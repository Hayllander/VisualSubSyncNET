using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Plugins;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.App.ViewModels;

/// <summary>Um nó da lista do Error report (TErrorTreeData), desenhado como em vtvErrorListDrawNode.</summary>
public sealed class ErrorItemViewModel(ErrorItem error) : ObservableObject
{
    public ErrorItem Error { get; } = error;

    /// <summary>Primeira linha, em negrito: GlobalMsg e, quando houver, " (ContextMsg)".</summary>
    public string Title => Error.ErrorMessage.Length > 0 ? Error.PluginMessage + " (" + Error.ErrorMessage + ")" : Error.PluginMessage;

    public string Times => TimeFormat.ToVss(Error.Subtitle.StartMs) + " -> " + TimeFormat.ToVss(Error.Subtitle.StopMs);

    /// <summary>StringConvertCRLFToPipe do texto da legenda.</summary>
    public string Line => SuggestionItem.ConvertCrLfToPipe(Error.Subtitle.Text);

    /// <summary>A bolinha da cor do plugin (TColor, ordem do Windows).</summary>
    public Brush ColorBrush
    {
        get
        {
            int c = Error.Color;
            var brush = new SolidColorBrush(Color.FromRgb((byte)(c & 0xFF), (byte)((c >> 8) & 0xFF), (byte)((c >> 16) & 0xFF)));
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>vtvErrorList.Repaint: a legenda pode ter sido corrigida.</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);
}

/// <summary>
/// Verificação de erros pelos plugins JavaScript (ActionCheckErrors, ActionFixErrorMain,
/// FixErrorInList, CanFixError) e o estado da janela Error report (ErrorReportFormUnit.pas).
/// </summary>
public sealed partial class MainViewModel : IVssCoreEngine
{
    private PluginContext? _pluginContext;

    /// <summary>UndoableMultiChangeTask: só existe enquanto um plugin corrige legendas.</summary>
    private List<TextChange>? _pluginChanges;

    /// <summary>g_PluginPath: a pasta jsplugin ao lado do executável.</summary>
    public static string PluginPath => Path.Combine(AppContext.BaseDirectory, "jsplugin");

    /// <summary>vtvErrorList: os erros na ordem em que foram encontrados.</summary>
    public ObservableCollection<ErrorItemViewModel> Errors { get; } = [];

    /// <summary>TntStatusBar1.Panels[0] do Error report.</summary>
    [ObservableProperty]
    private string _errorReportStatus = string.Empty;

    /// <summary>
    /// g_VSSCoreWrapper e ListJSPlugin, preparados no primeiro uso (o original faz isso ao abrir) e
    /// atualizados com as preferências atuais a cada chamada (ApplyMiscSettings).
    /// </summary>
    private PluginContext PluginContext
    {
        get
        {
            if (_pluginContext is null)
            {
                _pluginContext = new PluginContext { Engine = this };
                _pluginContext.ChangingStart += OnPluginChangingStart;
                _pluginContext.ChangingStop += OnPluginChangingStop;
                _pluginContext.ChangingText += OnPluginChangingText;
                Settings.LoadPlugins(PluginSettings.Build(PluginPath, _pluginContext, LogPluginError));
            }

            var context = _pluginContext;
            context.CpsTarget = Settings.SpaceKeyCpsTarget;
            context.MinimumDuration = Settings.SpaceKeyMinimalDuration;
            context.MinimumBlank = Settings.SpaceKeyBlankBetweenSubtitles;
            context.MaximumDuration = Settings.MaximumSubtitleDuration;
            context.SceneChangeStartOffset = Settings.SceneChangeStartOffset;
            context.SceneChangeStopOffset = Settings.SceneChangeStopOffset;
            context.SceneChangeFilterOffset = Settings.SceneChangeFilterOffset;
            context.SceneChangeVisible = ShowSceneChange;
            context.SceneChanges = SceneChanges ?? [];
            (context.VideoWidth, context.VideoHeight) = _engine is { HasVideo: true } engine ? engine.VideoSize : (0, 0);
            return context;
        }
    }

    private PluginSettings PluginSettings => Settings.Plugins ?? throw new InvalidOperationException("Plugins not loaded.");

    /// <summary>JSPEnum.OnJSPluginError := LogForm.LogMsg.</summary>
    private void LogPluginError(string message) => _dialogs.LogMessage(message, silent: false);

    /// <summary>ActionCheckErrorsExecute.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void CheckErrors()
    {
        using var cursor = _dialogs.WaitCursor();
        _dialogs.ClearLog();
        _dialogs.LogMessage("Starting error checking :", silent: true);
        Errors.Clear();
        var watch = Stopwatch.StartNew();
        var context = PluginContext;
        var found = ErrorChecker.Check(_document.Items, PluginPath, context, PluginSettings, LogPluginError);
        long execTime = watch.ElapsedMilliseconds;
        foreach (var error in found)
        {
            Errors.Add(new ErrorItemViewModel(error));
        }

        string message;
        if (Errors.Count > 0)
        {
            _dialogs.ShowErrorReport(this);
            message = string.Create(CultureInfo.InvariantCulture, $"{Errors.Count} error(s) found");
        }
        else
        {
            message = "No error found";
        }

        message += string.Create(CultureInfo.InvariantCulture, $" (in {execTime} ms).");
        ErrorReportStatus = message;
        _dialogs.LogMessage(message, silent: true);
    }

    /// <summary>ActionShowErrorReportExecute: ErrorReportForm.Visible := not ErrorReportForm.Visible.</summary>
    [RelayCommand]
    private void ShowErrorReport() => _dialogs.ToggleErrorReport(this);

    /// <summary>ActionNextErrorExecute: ErrorReportForm.GoToNextError.</summary>
    [RelayCommand]
    private void NextError() => _dialogs.GoToNextError();

    /// <summary>ActionFixSelectedErrorsExecute: ErrorReportForm.FixSelectedErrors.</summary>
    [RelayCommand]
    private void FixSelectedErrors() => _dialogs.FixSelectedErrors();

    /// <summary>SubListPopupMenuPopup: pmiFixError fica ativo quando algum plugin ativado sabe corrigir.</summary>
    public bool CanFixErrorMain => SelectedCount > 0 && ErrorChecker.AnyCanFixError(PluginPath, PluginContext, PluginSettings, LogPluginError);

    /// <summary>ActionFixErrorMainExecute: corrige as legendas selecionadas com todos os plugins ativados.</summary>
    [RelayCommand]
    private void FixErrorMain()
    {
        if (SelectedSubtitle is null)
        {
            return;
        }

        var selected = _selectedItems.OrderBy(Subtitles.IndexOf).Select(i => i.Model).ToList();
        var context = PluginContext;
        RunPluginChanges(() => ErrorChecker.FixSubtitles(_document.Items, selected, PluginPath, context, PluginSettings, LogPluginError));
    }

    /// <summary>TMainForm.CanFixError.</summary>
    public bool CanFixError(string pluginFileName) =>
        ErrorChecker.CanFixError(pluginFileName, PluginContext, PluginSettings, LogPluginError);

    /// <summary>FixErrorInList.</summary>
    public void FixErrors(IReadOnlyList<ErrorItemViewModel> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        using var cursor = _dialogs.WaitCursor();
        var watch = Stopwatch.StartNew();
        var context = PluginContext;
        int fixedCount = 0;
        RunPluginChanges(() => fixedCount = ErrorChecker.FixErrors(_document.Items, [.. errors.Select(e => e.Error)], context, PluginSettings, LogPluginError));
        long execTime = watch.ElapsedMilliseconds;
        foreach (var error in Errors)
        {
            error.Refresh();
        }

        ErrorReportStatus = string.Create(CultureInfo.InvariantCulture, $"{fixedCount} error(s) fixed (in {execTime} ms).");
    }

    /// <summary>
    /// O trecho comum de ActionFixErrorMain e FixErrorInList: os plugins mudam as legendas, as
    /// mudanças vão para uma TUndoableMultiChangeTask (empilhada sem refazer) e a tela é atualizada.
    /// </summary>
    private void RunPluginChanges(Action fix)
    {
        _pluginChanges = [];
        var changes = _pluginChanges;
        try
        {
            fix();
        }
        finally
        {
            _pluginChanges = null;
        }

        foreach (var change in changes)
        {
            Subtitles[change.Index].RefreshAll();
        }

        RefreshRanges();
        UpdateEditorStatus(_caret.Line, _caret.Column, _caret.FirstVisible);
        if (changes.Count > 0)
        {
            _undoStack.Push(new MultiChangeTask(this, changes));
            IsDirty = true;
        }
    }

    private TextChange? PluginChangeFor(Subtitle sub)
    {
        if (_pluginChanges is null)
        {
            return null;
        }

        int index = _document.Items.IndexOf(sub);
        var change = _pluginChanges.Find(c => c.Index == index);
        if (change is null)
        {
            change = new TextChange(index);
            _pluginChanges.Add(change);
        }

        return change;
    }

    /// <summary>OnSubtitleRangeJSWrapperChangeStart: o valor antigo é guardado só na primeira mudança.</summary>
    private void OnPluginChangingStart(Subtitle sub, int value)
    {
        if (PluginChangeFor(sub) is not { } change)
        {
            return;
        }

        change.OldStart ??= sub.StartMs;
        change.NewStart = value;
        TrackSelectionWith(sub, isStart: true, value);
    }

    private void OnPluginChangingStop(Subtitle sub, int value)
    {
        if (PluginChangeFor(sub) is not { } change)
        {
            return;
        }

        change.OldStop ??= sub.StopMs;
        change.NewStop = value;
        TrackSelectionWith(sub, isStart: false, value);
    }

    /// <summary>
    /// OnSubtitleRangeJSWrapperChangeStart/Stop: "Subtitle match current selection, update selection
    /// accordingly" — quando a legenda presa à seleção muda de tempo, a faixa branca anda junto.
    /// </summary>
    private void TrackSelectionWith(Subtitle sub, bool isStart, int value)
    {
        if (Waveform is { } waveform && waveform.SelectedRangeIndex == _document.Items.IndexOf(sub))
        {
            waveform.TrackSelectedRangeTime(isStart, value);
        }
    }

    private void OnPluginChangingText(Subtitle sub, string value)
    {
        if (PluginChangeFor(sub) is { } change)
        {
            change.OldText ??= sub.Text;
            change.NewText = value;
        }
    }

    /// <summary>vtvErrorListFocusChanged / DblClick: MainForm.SelectNode(pError.Range.Node).</summary>
    public void SelectErrorSubtitle(ErrorItemViewModel error)
    {
        ArgumentNullException.ThrowIfNull(error);
        SelectNodeAtIndex(_document.Items.IndexOf(error.Error.Subtitle));
    }

    /// <summary>A janela pede para acrescentar legendas à seleção da lista (pmiSelectAllXXX/YYY).</summary>
    public event EventHandler<IReadOnlyList<SubtitleItemViewModel>>? SubtitleSelectionAddRequested;

    /// <summary>pmiSelectAllXXXClick / pmiSelectAllYYYClick: seleciona na lista as legendas desses erros.</summary>
    public void SelectErrorSubtitles(IEnumerable<ErrorItemViewModel> errors)
    {
        var items = errors.Select(e => _document.Items.IndexOf(e.Error.Subtitle)).Where(i => i >= 0).Select(i => Subtitles[i]).ToList();
        SubtitleSelectionAddRequested?.Invoke(this, items);
    }

    /// <summary>DeleteError: some com os erros da legenda apagada.</summary>
    private void DeleteErrorsOf(Subtitle subtitle)
    {
        for (int i = Errors.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(Errors[i].Error.Subtitle, subtitle))
            {
                Errors.RemoveAt(i);
            }
        }
    }

    /// <summary>miExportToHTMLClick.</summary>
    public void ExportErrorsToHtml()
    {
        if (_dialogs.SaveFile("HTML file (*.html)|*.html", string.Empty, 1, "html") is not { } target)
        {
            return;
        }

        try
        {
            using var stream = File.Create(target.Path);
            ErrorReportHtml.Write(stream, [.. Errors.Select(e => e.Error)], e => _document.Items.IndexOf(e.Subtitle), Project?.FileName ?? string.Empty, ApplicationVersion);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(ex.Message);
        }
    }

    // TVSSCoreEngineIntf.
    IReadOnlyList<Subtitle> IVssCoreEngine.Subtitles => _document.Items;

    IReadOnlyList<Subtitle> IVssCoreEngine.SelectedSubtitles => [.. _selectedItems.OrderBy(Subtitles.IndexOf).Select(i => i.Model)];

    void IVssCoreEngine.RegisterJavascriptAction(string name, string caption, string defaultShortcut) =>
        RegisterJavascriptAction(name, caption, defaultShortcut);

    int IVssCoreEngine.GetTextSelectionStart() => _editorSelection.Start;

    int IVssCoreEngine.GetTextSelectionLength() => _editorSelection.Length;

    int IVssCoreEngine.GetAudioCursorPosition() => CursorMs;

    /// <summary>TVSSCoreWrapper.MeasureStringWidth: GetTextExtentPoint32W numa fonte criada para a tela.</summary>
    int IVssCoreEngine.MeasureStringWidth(string fontName, int fontSize, bool bold, string text) =>
        Gdi.MeasureStringWidth(fontName, fontSize, bold, text);
}

/// <summary>As chamadas GDI de MeasureStringWidth.</summary>
internal static partial class Gdi
{
    private const int LogPixelsY = 90;

    public static int MeasureStringWidth(string fontName, int fontSize, bool bold, string text)
    {
        nint dc = CreateDCW("DISPLAY", null, null, 0);
        if (dc == 0)
        {
            return 0;
        }

        try
        {
            int height = -MulDiv(fontSize, GetDeviceCaps(dc, LogPixelsY), 72);
            nint font = CreateFontW(height, 0, 0, 0, bold ? 700 : 0, 0, 0, 0, 0, 0, 0, 0, 0, fontName);
            nint old = SelectObject(dc, font);
            GetTextExtentPoint32W(dc, text, text.Length, out var size);
            SelectObject(dc, old);
            DeleteObject(font);
            return size.Cx;
        }
        finally
        {
            DeleteDC(dc);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SizeStruct
    {
        public int Cx;
        public int Cy;
    }

    [LibraryImport("gdi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateDCW(string driver, string? device, string? output, nint initData);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial int GetDeviceCaps(nint dc, int index);

    [LibraryImport("kernel32.dll")]
    private static partial int MulDiv(int number, int numerator, int denominator);

    [LibraryImport("gdi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateFontW(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string faceName);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint dc, nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint obj);

    [LibraryImport("gdi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetTextExtentPoint32W(nint dc, string text, int length, out SizeStruct size);
}
