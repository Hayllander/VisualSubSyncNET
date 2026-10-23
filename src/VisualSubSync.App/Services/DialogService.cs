using System.IO;
using System.Windows;
using Microsoft.Win32;
using VisualSubSync.App.Views;
using VisualSubSync.Core.Settings;

namespace VisualSubSync.App.Services;

public sealed class DialogService(VssSettings settings) : IDialogService
{
    private FindWindow? _find;
    private VerticalScalingWindow? _verticalScaling;
    private SilentZoneWindow? _silentZones;
    private SuggestionWindow? _suggestions;
    private LogWindow? _log;
    private ErrorReportWindow? _errorReport;
    private string? _preferencesTab;

    public bool IsFindShowing => _find?.IsVisible == true;

    public void ShowFind(ViewModels.MainViewModel viewModel)
    {
        if (_find is null)
        {
            _find = new FindWindow(viewModel) { Owner = Application.Current.MainWindow };
            FormPlacement.Restore(_find, settings, "FindForm");
        }

        _find.Show();
        _find.Activate();
    }

    public GotoResult? ShowGoto()
    {
        var window = new GotoWindow { Owner = Application.Current.MainWindow };
        return window.ShowDialog() == true ? window.Result : null;
    }

    public string? OpenFile(string filter, string fileName = "", int filterIndex = 1)
    {
        var dialog = new OpenFileDialog { Filter = filter, FileName = fileName, FilterIndex = filterIndex };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    public (string Path, int FilterIndex)? OpenFileWithFilter(string filter, int filterIndex = 1)
    {
        var dialog = new OpenFileDialog { Filter = filter, FilterIndex = filterIndex };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? (dialog.FileName, dialog.FilterIndex) : null;
    }

    public void ToggleVerticalScaling(ViewModels.MainViewModel viewModel)
    {
        _verticalScaling ??= new VerticalScalingWindow(viewModel) { Owner = Application.Current.MainWindow };
        if (_verticalScaling.IsVisible)
        {
            _verticalScaling.Hide();
        }
        else
        {
            _verticalScaling.Show();
        }
    }

    public void ShowSilentZones(ViewModels.MainViewModel viewModel)
    {
        if (_silentZones is null)
        {
            _silentZones = new SilentZoneWindow(viewModel) { Owner = Application.Current.MainWindow };
            FormPlacement.Restore(_silentZones, settings, "SilentZoneForm");
        }

        _silentZones.Show();
        _silentZones.UpdateData();
    }

    public void ToggleSuggestions(ViewModels.MainViewModel viewModel)
    {
        if (_suggestions is null)
        {
            _suggestions = new SuggestionWindow(viewModel) { Owner = Application.Current.MainWindow };
            FormPlacement.Restore(_suggestions, settings, "SuggestionForm");
        }

        if (_suggestions.IsVisible)
        {
            _suggestions.Hide();
        }
        else
        {
            _suggestions.Show();
        }
    }

    private ErrorReportWindow ErrorReport(ViewModels.MainViewModel viewModel)
    {
        if (_errorReport is null)
        {
            _errorReport = new ErrorReportWindow(viewModel) { Owner = Application.Current.MainWindow };
            FormPlacement.Restore(_errorReport, settings, "ErrorReportForm");
        }

        return _errorReport;
    }

    public void ShowErrorReport(ViewModels.MainViewModel viewModel) => ErrorReport(viewModel).Show();

    public void ToggleErrorReport(ViewModels.MainViewModel viewModel)
    {
        var window = ErrorReport(viewModel);
        if (window.IsVisible)
        {
            window.Hide();
        }
        else
        {
            window.Show();
        }
    }

    public void GoToNextError() => _errorReport?.GoToNextError();

    public void FixSelectedErrors() => _errorReport?.FixSelectedErrors();

    public void ToggleLog()
    {
        var log = Log;
        if (log.IsVisible)
        {
            log.Hide();
        }
        else
        {
            log.Show();
        }
    }

    public void LogMessage(string message, bool silent)
    {
        if (silent)
        {
            Log.SilentLogMsg(message);
        }
        else
        {
            Log.LogMsg(message);
        }
    }

    public void ClearLog() => Log.Clear();

    public bool ShowPreferences(VssSettings settings, string? tabName)
    {
        // O original reaproveita o formulário: sem aba pedida, volta a última, e a janela reabre onde
        // estava. A posição vem do ini e vai para ele ao sair (PrefFormInitialized).
        var window = new PreferencesWindow(settings, tabName ?? _preferencesTab) { Owner = Application.Current.MainWindow };
        FormPlacement.Restore(window, settings, "PreferencesForm");
        bool ok = window.ShowDialog() == true;
        _preferencesTab = window.SelectedTabName;
        FormPlacement.Store(window, settings, "PreferencesForm");
        return ok;
    }

    public void ShowSpellCheck(Core.Spelling.SpellCheckSession session, FontSpec font, Action<int> focusSubtitle)
    {
        var window = new SpellCheckWindow(session, font, focusSubtitle) { Owner = Application.Current.MainWindow };
        FormPlacement.Restore(window, settings, "SpellCheckForm");
        window.ShowDialog();
        FormPlacement.Store(window, settings, "SpellCheckForm");
    }

    public void ShowStyles(Core.Subtitles.StyleEditor editor, Action<string, string> renameStyle, Action setDirty)
    {
        var window = new StyleWindow(editor, renameStyle, setDirty) { Owner = Application.Current.MainWindow };
        window.ShowDialog();
    }

    public void StoreWindowPlacements()
    {
        // SaveSettings: as janelas que já foram abertas.
        if (_log is not null)
        {
            FormPlacement.Store(_log, settings, "LogForm");
        }

        if (_find is not null)
        {
            FormPlacement.Store(_find, settings, "FindForm");
        }

        if (_suggestions is not null)
        {
            FormPlacement.Store(_suggestions, settings, "SuggestionForm");
        }

        if (_errorReport is not null)
        {
            FormPlacement.Store(_errorReport, settings, "ErrorReportForm");
        }

        if (_silentZones is not null)
        {
            FormPlacement.Store(_silentZones, settings, "SilentZoneForm");
        }
    }

    /// <summary>LogForm := TLogForm.Create(nil) + LoadFormPosition, na primeira vez que é usada.</summary>
    private LogWindow Log
    {
        get
        {
            if (_log is null)
            {
                _log = new LogWindow { Owner = Application.Current.MainWindow };
                FormPlacement.Restore(_log, settings, "LogForm");
            }

            return _log;
        }
    }

    public IReadOnlyList<string> OpenFiles(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter, Multiselect = true };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileNames : [];
    }

    public string? SelectFolder(string caption)
    {
        var dialog = new OpenFolderDialog { Title = caption };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FolderName : null;
    }

    public IDisposable WaitCursor() => new OverrideCursor();

    public void RunPeakCreation(Action<IProgress<int>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var owner = System.Windows.Application.Current?.MainWindow;
        var window = new Views.PeakCreationProgressWindow();
        if (owner is { IsVisible: true })
        {
            window.Owner = owner;
            owner.IsEnabled = false;
        }

        window.Show();
        // O Progress é criado aqui, na thread da interface, para os avisos chegarem nela.
        var progress = new Progress<int>(window.SetProgress);
        var frame = new System.Windows.Threading.DispatcherFrame();
        var task = Task.Run(() => work(progress));
        task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        if (owner is not null)
        {
            owner.IsEnabled = true;
        }

        window.CloseForGood();
        task.GetAwaiter().GetResult();
    }

    private sealed class OverrideCursor : IDisposable
    {
        private readonly System.Windows.Input.Cursor? _previous = System.Windows.Input.Mouse.OverrideCursor;

        public OverrideCursor() => System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

        public void Dispose() => System.Windows.Input.Mouse.OverrideCursor = _previous;
    }

    public void ExploreFolder(string folder)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Explore {folder}: {ex.Message}");
        }
    }

    public (string Path, int FilterIndex)? SaveFile(string filter, string fileName, int filterIndex, string defaultExt = "")
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FilterIndex = filterIndex,
            FileName = Path.GetFileName(fileName),
            InitialDirectory = Path.GetDirectoryName(fileName) ?? string.Empty,
            AddExtension = defaultExt.Length > 0,
            DefaultExt = defaultExt,
        };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? (dialog.FileName, dialog.FilterIndex) : null;
    }

    public ProjectFormData? ShowProjectForm(ProjectFormData data)
    {
        var window = new ProjectFormWindow(this, data) { Owner = Application.Current.MainWindow };
        return window.ShowDialog() == true ? data : null;
    }

    public ExtractWavResult? ShowExtractWav(ExtractWavRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var window = new ExtractWavWindow(request) { Owner = Application.Current.MainWindow };
        return window.ShowDialog() == true ? window.Result : null;
    }

    public void ShowAbout(string version)
    {
        var window = new AboutWindow(version) { Owner = Application.Current.MainWindow };
        window.ShowDialog();
    }

    public TranslateResult? ShowTranslate(string targetFile, bool hasVo)
    {
        var window = new TranslateWindow(this, targetFile, hasVo) { Owner = Application.Current.MainWindow };
        return window.ShowDialog() == true ? window.Result : null;
    }

    public DelayResult? ShowDelay(DelayRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var window = new DelayWindow(request) { Owner = Application.Current.MainWindow };
        return window.ShowDialog() == true ? window.Result : null;
    }

    public bool Confirm(string caption, string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes;

    public bool ConfirmInformation(string caption, string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Information)
            == MessageBoxResult.Yes;

    public void ShowInformation(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowWarning(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

    public bool? AskYesNoCancel(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, "Warning", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null,
        };
}
