using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VisualSubSync.App.ViewModels;

/// <summary>O menu File > Load presets (FillPresetsMenu, LoadPresetFile, ActionLoadPresets) de main.pas.</summary>
public sealed partial class MainViewModel
{
    /// <summary>g_PresetsPath: a pasta presets ao lado do executável.</summary>
    public static string PresetsPath => Path.Combine(AppContext.BaseDirectory, "presets");

    /// <summary>O nome do arquivo de presets carregado por último (o item marcado no menu).</summary>
    [ObservableProperty]
    private string _checkedPreset = string.Empty;

    /// <summary>FillPresetsMenu: os *.ini da pasta, em ordem alfabética sem distinguir maiúsculas.</summary>
    public static IReadOnlyList<string> GetPresetFiles()
    {
        if (!Directory.Exists(PresetsPath))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(PresetsPath, "*.ini")
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>
    /// LoadPresetFile: o arquivo por cima das preferências (sem servidor, cópias e fontes), depois
    /// SetShortcut, ApplyMouseSettings, ApplyFontSettings e ApplyMiscSettings.
    /// </summary>
    public void LoadPresetFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        Settings.LoadPreset(path);
        OnPropertyChanged(nameof(IsSubtitleEditionEnabled));
        OnPropertyChanged(nameof(WaveformSelectionMode));
        ShowSceneChange = Settings.ShowSceneChange;
        ShowTextInWavDisplay = Settings.ShowTextInWavDisplay;
        OnPropertyChanged(nameof(Settings));
        PreferencesApplied?.Invoke(this, new PreferencesAppliedEventArgs(swapChanged: false, fontsChanged: true));
        CheckedPreset = Path.GetFileName(path);
    }

    /// <summary>OnLoadPresetMenuItemClick: o projeto guarda o nome escolhido e fica modificado.</summary>
    [RelayCommand]
    private void LoadPreset(string name)
    {
        LoadPresetFile(Path.Combine(PresetsPath, name));
        if (Project is not null)
        {
            Project.Presets = name;
            IsDirty = true;
        }
    }

    /// <summary>ActionLoadPresetsExecute ("Browse..."): carrega sem gravar no projeto.</summary>
    [RelayCommand]
    private void BrowsePresets()
    {
        string? path = _dialogs.OpenFile("Presets file|*.INI|All files (*.*)|*.*");
        if (path is not null)
        {
            LoadPresetFile(path);
        }
    }
}
