using System.Globalization;

namespace VisualSubSync.Core.Hotkeys;

/// <summary>
/// A aba Hotkeys das preferências (ListHotkeys, ComboHotkeyMode, bttSet/Clear/ResetAll): uma cópia
/// dos atalhos em ordem alfabética do nome (SortType = stText), editada no modo escolhido.
/// </summary>
public sealed class HotkeyEditor
{
    public HotkeyEditor(HotkeyTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var comparer = StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: false);
        Entries = [.. table.Entries.Select(e => e.Clone()).OrderBy(e => e.Caption, comparer)];
    }

    /// <summary>As linhas da lista, na ordem mostrada.</summary>
    public IReadOnlyList<HotkeyEntry> Entries { get; }

    /// <summary>ComboHotkeyMode: false = Normal, true = Timing.</summary>
    public bool TimingMode { get; set; }

    /// <summary>GetCurrentModeShortCut.</summary>
    public int ShortcutOf(HotkeyEntry? entry) => entry?.ShortcutFor(TimingMode) ?? 0;

    /// <summary>
    /// bttSetHotkeyClick: a primeira linha que já usa o atalho no modo atual perde o atalho, e a linha
    /// focada recebe o novo. Sem linha focada, só a primeira parte acontece, como no original.
    /// </summary>
    public void Set(HotkeyEntry? focused, int shortcut)
    {
        foreach (var entry in Entries)
        {
            int current = entry.ShortcutFor(TimingMode);
            if (current != 0 && current == shortcut)
            {
                entry.SetShortcutFor(TimingMode, 0);
                break;
            }
        }

        focused?.SetShortcutFor(TimingMode, shortcut);
    }

    /// <summary>bttClearHotkeyClick.</summary>
    public void Clear(HotkeyEntry? focused) => focused?.SetShortcutFor(TimingMode, 0);

    /// <summary>bttResetAllHotkeysClick: os dois modos voltam aos atalhos padrão.</summary>
    public void ResetAll()
    {
        foreach (var entry in Entries)
        {
            entry.NormalShortcut = entry.DefaultNormalShortcut;
            entry.TimingShortcut = entry.DefaultTimingShortcut;
        }
    }
}
