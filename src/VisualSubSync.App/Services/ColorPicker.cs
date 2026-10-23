using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VisualSubSync.App.Services;

/// <summary>
/// TColorDialog com as opções padrão: o ChooseColor do Windows começando na cor atual (CC_RGBINIT),
/// com as 16 cores personalizadas guardadas enquanto o programa roda.
/// </summary>
internal static class ColorPicker
{
    private const int CC_RGBINIT = 0x1;

    private static readonly int[] CustomColors = Enumerable.Repeat(0xFFFFFF, 16).ToArray();

    /// <summary>Devolve a cor escolhida como COLORREF (0x00BBGGRR), ou null se cancelado.</summary>
    public static uint? Choose(Window owner, uint colorRef)
    {
        IntPtr custom = Marshal.AllocHGlobal(sizeof(int) * 16);
        try
        {
            Marshal.Copy(CustomColors, 0, custom, 16);
            var cc = new CHOOSECOLORW
            {
                lStructSize = Marshal.SizeOf<CHOOSECOLORW>(),
                hwndOwner = new WindowInteropHelper(owner).Handle,
                rgbResult = (int)(colorRef & 0xFFFFFF),
                lpCustColors = custom,
                Flags = CC_RGBINIT,
            };
            bool ok = ChooseColorW(ref cc);
            Marshal.Copy(custom, CustomColors, 0, 16);
            return ok ? (uint)cc.rgbResult & 0xFFFFFF : null;
        }
        finally
        {
            Marshal.FreeHGlobal(custom);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CHOOSECOLORW
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public int rgbResult;
        public IntPtr lpCustColors;
        public int Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChooseColorW(ref CHOOSECOLORW cc);
}
