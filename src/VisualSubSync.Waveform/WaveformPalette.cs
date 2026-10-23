namespace VisualSubSync.Waveform;

/// <summary>
/// Cores de WAVDisplayerUnit.pas e MiniScrollBarUnit.pas. TColor do Delphi é $00BBGGRR; aqui os
/// valores estão em 0xRRGGBB, o formato dos pixels Bgr32 do bitmap.
/// </summary>
internal static class WaveformPalette
{
    public const int Background = 0x000000;        // WAV_BACK_COLOR clBlack
    public const int Wave = 0x4AF2A7;              // WAV_COLOR $00A7F24A
    public const int ZeroLine = 0x0A8B51;          // ZERO_LINE_COLOR $00518B0A
    public const int Range1 = 0xFF3333;            // RANGE_COLOR_1 $003333FF
    public const int Range2 = 0x0080FF;            // RANGE_COLOR_2 $00FF8000
    public const int RangeNotEditable = 0xC0C0C0;  // RANGE_COLOR_NOT_EDITABLE $00C0C0C0
    public const int RulerBack = 0x414751;         // RULER_BACK_COLOR $00514741
    public const int RulerTopBottomLine = 0xAEB5BE; // RULER_TOP_BOTTOM_LINE_COLOR $00BEB5AE
    public const int RulerText = 0xE0E0E0;         // RULER_TEXT_COLOR $00E0E0E0
    public const int RulerTextShadow = 0x000000;   // RULER_TEXT_SHADOW_COLOR clBlack
    public const int Cursor = 0xFFFF00;            // CURSOR_COLOR clYellow (desenhado em XOR)
    public const int DisabledBack = 0x808080;      // DISABLED_BACK_COLOR clGray
    public const int ScrollBarBack = 0x808080;     // TMiniScrollBar.Color clGray
    public const int ScrollThumb = 0x3A6EA5;       // FColorNormal $00A56E3A
    public const int ScrollThumbDown = 0x5E96D5;   // FColorDown $00D5965E
    public const int SceneChange = 0xFFC936;       // PaintSceneChange: Pen.Color $36C9FF
}
