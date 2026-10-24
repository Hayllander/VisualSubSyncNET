using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Subtitles.Formats;

namespace VisualSubSync.App.ViewModels;

/// <summary>O que a janela principal refaz depois do OK nas preferências.</summary>
/// <param name="SwapChanged">SwapSubtitlesList mudou: SwapSubList.</param>
/// <param name="FontsChanged">Uma das fontes mudou: ApplyFontSettings.</param>
public sealed class PreferencesAppliedEventArgs(bool swapChanged, bool fontsChanged) : EventArgs
{
    public bool SwapChanged { get; } = swapChanged;

    public bool FontsChanged { get; } = fontsChanged;
}

/// <summary>ShowPreferences e as rotinas Apply* de main.pas, com a cópia automática (TimerAutoBackup).</summary>
public sealed partial class MainViewModel
{
    private DispatcherTimer? _autoBackupTimer;

    /// <summary>A janela aplica fontes, troca da lista, roda do mouse e atalhos dos menus.</summary>
    public event EventHandler<PreferencesAppliedEventArgs>? PreferencesApplied;

    /// <summary>ActionPreferencesExecute.</summary>
    [RelayCommand]
    private void Preferences() => ShowPreferences(null);

    /// <summary>ActionErrorPreferencesExecute: abre na aba Error checking.</summary>
    [RelayCommand]
    private void ErrorPreferences() => ShowPreferences("tsErrorChecking");

    /// <summary>ShowPreferences(TabSheet).</summary>
    private void ShowPreferences(string? tabName)
    {
        bool oldSwap = Settings.SwapSubtitlesList;
        var oldListFont = Settings.SubListFont;
        var oldTextFont = Settings.SubTextFont;
        var oldVideoStyle = (
            Settings.SubVideoFont,
            Settings.TransparencySubVideoFont,
            Settings.OutlineSubVideoFont,
            Settings.OutlineSubVideoColorFont,
            Settings.TransparencyOutlineSubVideoFont,
            Settings.ShadowSubVideoFont,
            Settings.ShadowSubVideoColorFont,
            Settings.TransparencyShadowSubVideoFont);
        if (!_dialogs.ShowPreferences(Settings, tabName))
        {
            return;
        }

        if (_server is not null)
        {
            _server.EnableCompression = Settings.WebServerEnableCompression;
        }

        // MemoSubtitleText.Enabled, SetShortcut e ApplyMouseSettings (SelMode).
        OnPropertyChanged(nameof(IsSubtitleEditionEnabled));
        OnPropertyChanged(nameof(WaveformSelectionMode));
        ApplyAutoBackupSettings();

        // ApplyMiscSettings: mudança de cena, espaço mínimo e o texto na forma de onda.
        ShowSceneChange = Settings.ShowSceneChange;
        ShowTextInWavDisplay = Settings.ShowTextInWavDisplay;
        if (oldVideoStyle != (
            Settings.SubVideoFont,
            Settings.TransparencySubVideoFont,
            Settings.OutlineSubVideoFont,
            Settings.OutlineSubVideoColorFont,
            Settings.TransparencyOutlineSubVideoFont,
            Settings.ShadowSubVideoFont,
            Settings.ShadowSubVideoColorFont,
            Settings.TransparencyShadowSubVideoFont))
        {
            ApplyVideoSubtitleStyle();
            _videoPreviewNeedSubtitleUpdate = true;
            if (ShowingVideo)
            {
                UpdateSubtitleForPreview(true);
            }
        }

        OnPropertyChanged(nameof(Settings));
        PreferencesApplied?.Invoke(this, new PreferencesAppliedEventArgs(
            Settings.SwapSubtitlesList != oldSwap,
            Settings.SubListFont != oldListFont || Settings.SubTextFont != oldTextFont));
    }

    /// <summary>ApplyAutoBackupSettings: o intervalo recomeça; 0 minutos desliga.</summary>
    public void ApplyAutoBackupSettings()
    {
        if (_autoBackupTimer is null)
        {
            _autoBackupTimer = new DispatcherTimer();
            _autoBackupTimer.Tick += (_, _) => OnAutoBackupTimer();
        }

        _autoBackupTimer.Stop();
        if (Settings.AutoBackupEvery > 0)
        {
            _autoBackupTimer.Interval = TimeSpan.FromMinutes(Settings.AutoBackupEvery);
            _autoBackupTimer.Start();
        }
    }

    /// <summary>
    /// TimerAutoBackupTimer: com algo para salvar, grava Backup\&lt;legenda&gt;.bak no formato da legenda,
    /// sem mexer no arquivo do projeto nem no estado de modificado.
    /// </summary>
    private void OnAutoBackupTimer()
    {
        if (!CanSave || Project is null)
        {
            return;
        }

        string subtitles = Project.SubtitlesFile;
        if (!SubtitleFile.CanWrite(subtitles))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(BackupDirectory);
            string target = Path.Combine(BackupDirectory, Path.ChangeExtension(Path.GetFileName(subtitles), ".bak"));
            SubtitleFile.Save(_document, target, BaseWriteOptions, formatOf: subtitles);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning($"Auto backup failed: {ex.Message}");
            _dialogs.LogMessage($"Auto backup failed: {ex.Message}", silent: false);
        }
    }

    /// <summary>WAVDisplayer1StartPlaying: com AutoSaveWhenPlaying, ActionSave.Execute (só se habilitada).</summary>
    private void OnStartPlaying()
    {
        if (Settings.AutoSaveWhenPlaying && SaveCommand.CanExecute(null))
        {
            SaveCommand.Execute(null);
        }
    }
}
