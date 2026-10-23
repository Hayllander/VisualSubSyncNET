using VisualSubSync.Core.Settings;

namespace VisualSubSync.Core.Waveform;

/// <summary>O que a roda do mouse faz na forma de onda.</summary>
public enum WheelAction
{
    Nothing,
    HorizontalZoom,
    VerticalZoom,
    TimeScroll,
}

/// <summary>
/// DoMouseWheelUp/DoMouseWheelDown do TWAVDisplayer: a primeira ação cuja tecla está pressionada,
/// na ordem zoom horizontal, zoom vertical e rolagem. "None" vale quando nem Shift, nem Alt, nem Ctrl
/// estão pressionados.
/// </summary>
public static class WheelActions
{
    public static WheelAction Resolve(WheelModifier hZoom, WheelModifier vZoom, WheelModifier timeScroll, bool shift, bool alt, bool ctrl)
    {
        if (Matches(hZoom))
        {
            return WheelAction.HorizontalZoom;
        }

        if (Matches(vZoom))
        {
            return WheelAction.VerticalZoom;
        }

        return Matches(timeScroll) ? WheelAction.TimeScroll : WheelAction.Nothing;

        bool Matches(WheelModifier m) => m switch
        {
            WheelModifier.None => !shift && !alt && !ctrl,
            WheelModifier.Shift => shift,
            WheelModifier.Alt => alt,
            WheelModifier.Ctrl => ctrl,
            _ => false,
        };
    }
}
