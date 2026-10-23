using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using VisualSubSync.Core.Settings;

namespace VisualSubSync.App.Services;

/// <summary>
/// FontDialog1 (TFontDialog) com Options = [fdAnsiOnly, fdEffects, fdNoVectorFonts, fdScalableOnly]:
/// o ChooseFont do Windows, começando na fonte atual; a escolha volta como Font2String.
/// </summary>
internal static partial class FontPicker
{
    private const int CF_SCREENFONTS = 0x1;
    private const int CF_INITTOLOGFONTSTRUCT = 0x40;
    private const int CF_EFFECTS = 0x100;
    private const int CF_ANSIONLY = 0x400;
    private const int CF_NOVECTORFONTS = 0x800;
    private const int CF_SCALABLEONLY = 0x20000;

    /// <summary>Screen.PixelsPerInch do Delphi 7 (sem DPI awareness).</summary>
    private const int PixelsPerInch = 96;

    public static FontSpec? Choose(Window owner, FontSpec font) =>
        Choose(owner, font, CF_SCREENFONTS | CF_INITTOLOGFONTSTRUCT | CF_EFFECTS | CF_ANSIONLY | CF_NOVECTORFONTS | CF_SCALABLEONLY);

    /// <summary>
    /// FontDialog1 de TStyleForm: as opções padrão do TFontDialog, com fdEffects só no modo ASS
    /// (ConfigureMode).
    /// </summary>
    public static FontSpec? ChooseForStyle(Window owner, FontSpec font, bool effects) =>
        Choose(owner, font, CF_SCREENFONTS | CF_INITTOLOGFONTSTRUCT | (effects ? CF_EFFECTS : 0));

    private static FontSpec? Choose(Window owner, FontSpec font, int flags)
    {
        ArgumentNullException.ThrowIfNull(font);
        var logFont = new LOGFONTW
        {
            lfHeight = -(int)Math.Round(font.SizePt * PixelsPerInch / 72.0, MidpointRounding.AwayFromZero),
            lfWeight = font.Bold ? 700 : 400,
            lfItalic = (byte)(font.Italic ? 1 : 0),
            lfUnderline = (byte)(font.Underline ? 1 : 0),
            lfStrikeOut = (byte)(font.Strikeout ? 1 : 0),
            lfCharSet = (byte)font.Charset,
            lfFaceName = font.Name,
        };
        IntPtr pLogFont = Marshal.AllocHGlobal(Marshal.SizeOf<LOGFONTW>());
        try
        {
            Marshal.StructureToPtr(logFont, pLogFont, false);
            int rgb = font.ToRgb() ?? ToRgb(SystemColors.WindowTextColor);
            var cf = new CHOOSEFONTW
            {
                lStructSize = Marshal.SizeOf<CHOOSEFONTW>(),
                hwndOwner = new WindowInteropHelper(owner).Handle,
                lpLogFont = pLogFont,
                Flags = flags,
                rgbColors = ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF),
            };
            if (!ChooseFontW(ref cf))
            {
                return null;
            }

            var result = Marshal.PtrToStructure<LOGFONTW>(pLogFont);
            int size = (int)Math.Round(-result.lfHeight * 72.0 / PixelsPerInch, MidpointRounding.AwayFromZero);
            int style = (result.lfWeight >= 700 ? 1 : 0) | (result.lfItalic != 0 ? 2 : 0) |
                        (result.lfUnderline != 0 ? 4 : 0) | (result.lfStrikeOut != 0 ? 8 : 0);
            return new FontSpec(result.lfFaceName, size, style, result.lfCharSet, FontSpec.ColorToString(cf.rgbColors & 0xFFFFFF));
        }
        finally
        {
            Marshal.FreeHGlobal(pLogFont);
        }
    }

    private static int ToRgb(System.Windows.Media.Color c) => (c.R << 16) | (c.G << 8) | c.B;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LOGFONTW
    {
        public int lfHeight;
        public int lfWidth;
        public int lfEscapement;
        public int lfOrientation;
        public int lfWeight;
        public byte lfItalic;
        public byte lfUnderline;
        public byte lfStrikeOut;
        public byte lfCharSet;
        public byte lfOutPrecision;
        public byte lfClipPrecision;
        public byte lfQuality;
        public byte lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string lfFaceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CHOOSEFONTW
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hDC;
        public IntPtr lpLogFont;
        public int iPointSize;
        public int Flags;
        public int rgbColors;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr hInstance;
        public IntPtr lpszStyle;
        public short nFontType;
        public short ___MISSING_ALIGNMENT__;
        public int nSizeMin;
        public int nSizeMax;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChooseFontW(ref CHOOSEFONTW cf);
}
