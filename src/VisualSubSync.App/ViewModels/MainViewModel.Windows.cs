using CommunityToolkit.Mvvm.Input;

namespace VisualSubSync.App.ViewModels;

/// <summary>Janelas auxiliares do menu View: ActionZoomVertical, ActionShowSilentZones e ActionShowHideLogs.</summary>
public sealed partial class MainViewModel
{
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ZoomVertical() => _dialogs.ToggleVerticalScaling(this);

    [RelayCommand]
    private void ShowSilentZones() => _dialogs.ShowSilentZones(this);

    [RelayCommand]
    private void ShowHideLogs() => _dialogs.ToggleLog();

    /// <summary>TMainForm.SetSelection: seleciona o trecho solto e mostra 20 s em volta do meio dele.</summary>
    public void SetSelection(int startMs, int stopMs)
    {
        Waveform?.SetSelection(startMs, stopMs);
        int center = startMs + ((stopMs - startMs) / 2);
        Waveform?.ZoomRange(center - 10_000, center + 10_000);
    }

    /// <summary>SaveFormPosition das janelas auxiliares, chamado pela janela antes de gravar as configurações.</summary>
    public void StoreWindowPlacements() => _dialogs.StoreWindowPlacements();
}
