using System.Globalization;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Settings;

/// <summary>
/// Fonte no formato de Font2String/String2Font: "Nome,Tamanho,Estilo,Charset,Cor", em que Estilo
/// é o conjunto TFontStyles (1 = negrito, 2 = itálico, 4 = sublinhado, 8 = tachado) e a cor é um
/// nome do Delphi (clWindowText) ou um número $00BBGGRR.
/// </summary>
public sealed record FontSpec(string Name, int SizePt, int Style, int Charset, string Color)
{
    public bool Bold => (Style & 1) != 0;

    public bool Italic => (Style & 2) != 0;

    public bool Underline => (Style & 4) != 0;

    public bool Strikeout => (Style & 8) != 0;

    /// <summary>Tamanho em pixels a 96 DPI (unidade do WPF).</summary>
    public double SizePx => SizePt * 96 / 72.0;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Name},{SizePt},{Style},{Charset},{Color}");

    /// <summary>String2Font: só aplica quando há exatamente 5 campos; senão mantém o padrão.</summary>
    public static FontSpec Parse(string? text, FontSpec fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        string[] parts = (text ?? string.Empty).Split(',');
        if (parts.Length != 5)
        {
            return fallback;
        }

        return new FontSpec(
            DelphiText.Trim(parts[0]),
            DelphiText.StrToIntDef(DelphiText.Trim(parts[1]), 12),
            DelphiText.StrToIntDef(DelphiText.Trim(parts[2]), 0) & 0xFF,
            DelphiText.StrToIntDef(DelphiText.Trim(parts[3]), 0),
            DelphiText.Trim(parts[4]));
    }

    /// <summary>
    /// StringToColor para cores fixas: devolve 0xRRGGBB, ou null para cores do sistema (clWindowText...).
    /// </summary>
    public int? ToRgb()
    {
        int? bgr = Color.ToLowerInvariant() switch
        {
            "clblack" => 0x000000,
            "clmaroon" => 0x000080,
            "clgreen" => 0x008000,
            "clolive" => 0x008080,
            "clnavy" => 0x800000,
            "clpurple" => 0x800080,
            "clteal" => 0x808000,
            "clgray" => 0x808080,
            "clsilver" => 0xC0C0C0,
            "clred" => 0x0000FF,
            "cllime" => 0x00FF00,
            "clyellow" => 0x00FFFF,
            "clblue" => 0xFF0000,
            "clfuchsia" => 0xFF00FF,
            "claqua" => 0xFFFF00,
            "clwhite" => 0xFFFFFF,
            "clmoneygreen" => 0xC0DCC0,
            "clskyblue" => 0xF0CAA6,
            "clcream" => 0xF0FBFF,
            "clmedgray" => 0xA4A0A0,
            _ when Color.StartsWith("cl", StringComparison.OrdinalIgnoreCase) => null,
            _ => DelphiText.StrToIntDef(Color, -1) is int v and >= 0 ? v & 0xFFFFFF : null,
        };
        return bgr is int c ? ((c & 0xFF) << 16) | (c & 0xFF00) | ((c >> 16) & 0xFF) : null;
    }

    /// <summary>Os nomes de cor fixa de Graphics.pas (Colors), com o valor TColor ($00BBGGRR).</summary>
    private static readonly (string Name, int Bgr)[] ColorNames =
    [
        ("clBlack", 0x000000), ("clMaroon", 0x000080), ("clGreen", 0x008000), ("clOlive", 0x008080),
        ("clNavy", 0x800000), ("clPurple", 0x800080), ("clTeal", 0x808000), ("clGray", 0x808080),
        ("clSilver", 0xC0C0C0), ("clRed", 0x0000FF), ("clLime", 0x00FF00), ("clYellow", 0x00FFFF),
        ("clBlue", 0xFF0000), ("clFuchsia", 0xFF00FF), ("clAqua", 0xFFFF00), ("clWhite", 0xFFFFFF),
        ("clMoneyGreen", 0xC0DCC0), ("clSkyBlue", 0xF0CAA6), ("clCream", 0xF0FBFF), ("clMedGray", 0xA4A0A0),
    ];

    /// <summary>
    /// ColorToString de uma cor escolhida no diálogo de fonte (TColor $00BBGGRR): o nome da VCL quando
    /// há um, senão "$" e oito dígitos hexadecimais.
    /// </summary>
    public static string ColorToString(int bgr)
    {
        foreach (var (name, value) in ColorNames)
        {
            if (value == bgr)
            {
                return name;
            }
        }

        return "$" + bgr.ToString("X8", CultureInfo.InvariantCulture);
    }
}
