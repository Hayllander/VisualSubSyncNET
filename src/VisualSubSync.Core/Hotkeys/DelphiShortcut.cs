using System.Globalization;

namespace VisualSubSync.Core.Hotkeys;

/// <summary>
/// TShortCut do Delphi (Menus.pas): o byte baixo é a tecla virtual (VK_*) e os bits altos são os
/// modificadores. <see cref="ToText"/> e <see cref="FromText"/> seguem ShortCutToText e
/// TextToShortCut, que gravam e leem a seção [Hotkeys] do VisualSubSync.ini.
/// </summary>
public static class DelphiShortcut
{
    public const int Shift = 0x2000;
    public const int Ctrl = 0x4000;
    public const int Alt = 0x8000;

    private static readonly string[] SpaceToDown = ["Space", "PgUp", "PgDn", "End", "Home", "Left", "Up", "Right", "Down"];

    /// <summary>
    /// GetSpecialName: nome que o Windows dá às demais teclas (GetKeyNameText). Depende do layout do
    /// teclado; a janela troca esta tabela pela do sistema. O padrão é o layout americano.
    /// </summary>
    public static Func<int, string?> SpecialName { get; set; } = UsSpecialName;

    public static int Make(int virtualKey, bool shift, bool ctrl, bool alt) =>
        (virtualKey & 0xFF) | (shift ? Shift : 0) | (ctrl ? Ctrl : 0) | (alt ? Alt : 0);

    /// <summary>ShortCutToText: "Shift+", "Ctrl+", "Alt+" nessa ordem e o nome da tecla; vazio sem nome.</summary>
    public static string ToText(int shortcut)
    {
        int key = shortcut & 0xFF;
        string? name = key switch
        {
            0x08 => "BkSp",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            >= 0x20 and <= 0x28 => SpaceToDown[key - 0x20],
            0x2D => "Ins",
            0x2E => "Del",
            >= 0x30 and <= 0x39 => ((char)key).ToString(),
            >= 0x41 and <= 0x5A => ((char)key).ToString(),
            >= 0x60 and <= 0x69 => ((char)(key - 0x60 + '0')).ToString(),
            >= 0x70 and <= 0x87 => "F" + (key - 0x6F).ToString(CultureInfo.InvariantCulture),
            _ => SpecialName(key),
        };
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        return ((shortcut & Shift) != 0 ? "Shift+" : string.Empty) +
               ((shortcut & Ctrl) != 0 ? "Ctrl+" : string.Empty) +
               ((shortcut & Alt) != 0 ? "Alt+" : string.Empty) + name;
    }

    /// <summary>
    /// TextToShortCut: tira os modificadores do começo (em qualquer ordem, também "^" para Ctrl) e
    /// procura a primeira tecla de $08 em diante com o mesmo nome, sem diferenciar maiúsculas.
    /// </summary>
    public static int FromText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int modifiers = 0;
        while (true)
        {
            if (TakeFront(ref text, "Shift+"))
            {
                modifiers |= Shift;
            }
            else if (TakeFront(ref text, "^") || TakeFront(ref text, "Ctrl+"))
            {
                modifiers |= Ctrl;
            }
            else if (TakeFront(ref text, "Alt+"))
            {
                modifiers |= Alt;
            }
            else
            {
                break;
            }
        }

        if (text.Length == 0)
        {
            return 0;
        }

        for (int key = 0x08; key <= 0xFF; key++)
        {
            if (string.Equals(text, ToText(key), StringComparison.OrdinalIgnoreCase))
            {
                return key | modifiers;
            }
        }

        return 0;
    }

    private static bool TakeFront(ref string text, string front)
    {
        if (text.StartsWith(front, StringComparison.OrdinalIgnoreCase))
        {
            text = text[front.Length..];
            return true;
        }

        return false;
    }

    /// <summary>GetKeyNameText(MapVirtualKey(vk) shl 16) num Windows com teclado americano.</summary>
    private static string? UsSpecialName(int key) => key switch
    {
        0x13 => "Pause",
        0x14 => "Caps Lock",
        0x6A => "Num *",
        0x6B => "Num +",
        0x6D => "Num -",
        0x6E => "Num Del",
        0x6F => "/",
        0x90 => "Num Lock",
        0x91 => "Scroll Lock",
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",
        _ => null,
    };
}
