using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Subtitles.Formats;

namespace VisualSubSync.App.ViewModels;

/// <summary>ActionStyles, cbStyles e RenameStyle de main.pas.</summary>
public sealed partial class MainViewModel
{
    // lstStyles.ItemIndex: o TStyleForm é reaproveitado e lembra a seleção até outro arquivo ser carregado.
    private int _styleFormIndex = -1;
    private bool _updatingStyleCombo;

    /// <summary>cbStyles.Items.</summary>
    public ObservableCollection<string> StyleNames { get; } = [];

    /// <summary>cbStyles.ItemIndex: -1 quando a seleção mistura estilos.</summary>
    [ObservableProperty]
    private int _selectedStyleIndex = -1;

    /// <summary>EnableStyleControls: só com um .ass ou .ssa carregado.</summary>
    public bool IsStyleEnabled => Project is not null && ShowStyleColumn;

    partial void OnShowStyleColumnChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStyleEnabled));
        StylesCommand.NotifyCanExecuteChanged();
    }

    /// <summary>ActionStylesExecute: a janela edita os estilos do documento no lugar.</summary>
    [RelayCommand(CanExecute = nameof(IsStyleEnabled))]
    private void Styles()
    {
        var editor = new StyleEditor(_document.Styles, SubtitleFile.IsAss(Project!.SubtitlesFile)) { SelectedIndex = _styleFormIndex };
        string current = SelectedStyleIndex >= 0 && SelectedStyleIndex < StyleNames.Count ? StyleNames[SelectedStyleIndex] : string.Empty;
        editor.PreSelect(current);
        _dialogs.ShowStyles(editor, RenameStyle, () => IsDirty = true);
        _styleFormIndex = editor.SelectedIndex;
        if (editor.StylesChanged)
        {
            UpdateStylesComboBox();
            IsDirty = true;
        }
    }

    /// <summary>UpdateStylesComboBox: os nomes na ordem da lista de estilos.</summary>
    private void UpdateStylesComboBox()
    {
        _updatingStyleCombo = true;
        StyleNames.Clear();
        foreach (var style in _document.Styles)
        {
            StyleNames.Add(style.Name);
        }

        _updatingStyleCombo = false;
        UpdateStylesComboboxFromSelection();
    }

    /// <summary>
    /// UpdateStylesComboboxFromSelection: o estilo comum às legendas selecionadas, ou nada quando há
    /// mais de um (comparação sem distinguir maiúsculas, como TStrings.IndexOf).
    /// </summary>
    private void UpdateStylesComboboxFromSelection()
    {
        // Sem legenda selecionada (GetFirstSelected = nil) o combo fica como estava.
        IReadOnlyList<SubtitleItemViewModel> selected = _selectedItems;
        int index = -1;
        if (selected.Count > 0 && selected.All(i => i.Model.Style == selected[0].Model.Style))
        {
            string style = selected[0].Model.Style;
            index = StyleNames.ToList().FindIndex(n => StringComparer.CurrentCultureIgnoreCase.Equals(n, style));
        }
        else if (selected.Count == 0)
        {
            index = SelectedStyleIndex;
        }

        _updatingStyleCombo = true;
        SelectedStyleIndex = index;
        _updatingStyleCombo = false;
    }

    /// <summary>cbStylesSelect: o estilo vai para todas as legendas selecionadas (sem desfazer, como no original).</summary>
    partial void OnSelectedStyleIndexChanged(int value)
    {
        if (_updatingStyleCombo || value < 0 || value >= StyleNames.Count)
        {
            return;
        }

        string newStyle = StyleNames[value];
        bool changed = false;
        foreach (var item in _selectedItems.OrderBy(Subtitles.IndexOf))
        {
            if (item.Model.Style != newStyle)
            {
                item.Model.Style = newStyle;
                item.RefreshAll();
                changed = true;
            }
        }

        if (changed)
        {
            IsDirty = true;
        }
    }

    /// <summary>RenameStyle: as legendas com o nome antigo passam a usar o novo.</summary>
    private void RenameStyle(string oldName, string newName)
    {
        bool changed = false;
        foreach (var item in Subtitles)
        {
            if (item.Model.Style == oldName)
            {
                item.Model.Style = newName;
                item.RefreshAll();
                changed = true;
            }
        }

        if (changed)
        {
            IsDirty = true;
        }
    }
}
