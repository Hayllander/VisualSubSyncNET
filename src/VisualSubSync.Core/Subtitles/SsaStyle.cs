using System.Globalization;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Subtitles;

/// <summary>
/// Estilo SSA/ASS (TSSAStyle em StyleFormUnit.pas). As cores ficam em ABGR (0xAABBGGRR) e o
/// alinhamento sempre na numeração do ASS (1..9); a conversão para SSA ocorre só ao gravar.
/// </summary>
public sealed class SsaStyle
{
    public string Name { get; set; } = "New Style";

    public string FontName { get; set; } = "Arial";

    public double FontSize { get; set; } = 24;

    /// <summary>clYellow.</summary>
    public uint PrimaryColor { get; set; } = 0x0000FFFF;

    public uint SecondaryColor { get; set; }

    public uint OutlineColor { get; set; }

    public uint BackColor { get; set; }

    public int Bold { get; set; }

    public int Italic { get; set; }

    public int Underline { get; set; }

    public int Strikeout { get; set; }

    public double ScaleX { get; set; } = 100;

    public double ScaleY { get; set; } = 100;

    public double Spacing { get; set; }

    public double Angle { get; set; }

    public int BorderStyle { get; set; } = 1;

    public double Outline { get; set; } = 2;

    public double Shadow { get; set; }

    /// <summary>Alinhamento ASS: 1-3 embaixo, 4-6 meio, 7-9 em cima.</summary>
    public int Alignment { get; set; } = 2;

    public int MarginL { get; set; } = 15;

    public int MarginR { get; set; } = 15;

    public int MarginV { get; set; } = 15;

    public int AlphaLevel { get; set; }

    public int Encoding { get; set; }

    public SsaStyle Clone() => (SsaStyle)MemberwiseClone();

    /// <summary>SSAToASSAlignment: 1,2,3 → 1,2,3; 9,10,11 → 4,5,6; 5,6,7 → 7,8,9; outros → 2.</summary>
    public static int SsaToAssAlignment(int ssa) => ssa switch
    {
        1 or 2 or 3 => ssa,
        9 or 10 or 11 => ssa - 5,
        5 or 6 or 7 => ssa + 2,
        _ => 2,
    };

    /// <summary>ASSToSSAAlignment, inverso de <see cref="SsaToAssAlignment"/>.</summary>
    public static int AssToSsaAlignment(int ass) => ass switch
    {
        1 or 2 or 3 => ass,
        4 or 5 or 6 => ass + 5,
        7 or 8 or 9 => ass - 2,
        _ => 2,
    };

    /// <summary>AssColorString2ABGR: "&amp;HAABBGGRR&amp;", "&amp;HBBGGRR" ou decimal.</summary>
    public static uint ParseColor(string color)
    {
        ArgumentNullException.ThrowIfNull(color);
        if (color.Length == 0)
        {
            return 0;
        }

        if (!color.StartsWith("&H", StringComparison.Ordinal))
        {
            return unchecked((uint)DelphiText.StrToIntDef(color, 0));
        }

        string clean = color.Replace("&", string.Empty, StringComparison.Ordinal)
                            .Replace("H", string.Empty, StringComparison.Ordinal);
        int len = clean.Length;
        uint r = HexByte(clean, len - 2), g = HexByte(clean, len - 4), b = HexByte(clean, len - 6);
        uint a = len > 6 ? HexByte(clean, len - 8) : 0;
        return (a << 24) | (b << 16) | (g << 8) | r;
    }

    /// <summary>ABGRColor2AssColorString: "&amp;HAABBGGRR" em hexadecimal maiúsculo.</summary>
    public static string FormatColor(uint abgr) =>
        string.Create(CultureInfo.InvariantCulture,
            $"&H{abgr >> 24:X2}{(abgr >> 16) & 0xFF:X2}{(abgr >> 8) & 0xFF:X2}{abgr & 0xFF:X2}");

    /// <summary>getAsSSA. Cores em decimal com sinal, como o Format('%d') do Delphi.</summary>
    public string ToSsaLine() => string.Create(CultureInfo.InvariantCulture,
        $"Style: {Name},{FontName},{RoundDelphi(FontSize)},{(int)PrimaryColor},{(int)SecondaryColor},{(int)OutlineColor},{(int)BackColor},{Bold},{Italic},{BorderStyle},{RoundDelphi(Outline)},{RoundDelphi(Shadow)},{AssToSsaAlignment(Alignment)},{MarginL},{MarginR},{MarginV},{AlphaLevel},{Encoding}");

    /// <summary>getAsASS.</summary>
    public string ToAssLine() => string.Create(CultureInfo.InvariantCulture,
        $"Style: {Name},{FontName},{RoundDelphi(FontSize)},{FormatColor(PrimaryColor)},{FormatColor(SecondaryColor)},{FormatColor(OutlineColor)},{FormatColor(BackColor)},{Bold},{Italic},{Underline},{Strikeout},{DelphiText.FloatToStr(ScaleX)},{DelphiText.FloatToStr(ScaleY)},{DelphiText.FloatToStr(Spacing)},{DelphiText.FloatToStr(Angle)},{BorderStyle},{DelphiText.FloatToStr(Outline)},{DelphiText.FloatToStr(Shadow)},{Alignment},{MarginL},{MarginR},{MarginV},{Encoding}");

    /// <summary>Round do Delphi arredonda empates para o par (5.5 → 6, 2.5 → 2).</summary>
    private static long RoundDelphi(double value) => (long)Math.Round(value, MidpointRounding.ToEven);

    private static uint HexByte(string s, int index)
    {
        // Copy(S, index+1, 2) do Delphi: índice negativo é tratado como início da string.
        if (index < 0)
        {
            int length = 2 + index;
            if (length <= 0)
            {
                return 0;
            }

            return Parse(s[..Math.Min(length, s.Length)]);
        }

        return index >= s.Length ? 0 : Parse(s.Substring(index, Math.Min(2, s.Length - index)));

        static uint Parse(string hex) =>
            uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint v) ? v : 0;
    }
}
