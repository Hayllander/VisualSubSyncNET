namespace VisualSubSync.Core.Subtitles;

/// <summary>RGBHSLColorUnit.pas (JclGraphUtils): conversões RGB/HSL em Single e ChangeColorHSL.</summary>
public static class HslColor
{
    /// <summary>
    /// ChangeColorHSL: soma os deslocamentos ao H/S/L da cor (S e L limitados a 0..1) e mantém o byte
    /// alto (alfa) como estava.
    /// </summary>
    public static uint ChangeColorHsl(uint color, float hueOffset, float saturationOffset, float luminanceOffset)
    {
        byte r = (byte)(color & 0xFF), g = (byte)((color >> 8) & 0xFF), b = (byte)((color >> 16) & 0xFF);
        uint a = color >> 24;
        RgbToHls(r / 255.0f, g / 255.0f, b / 255.0f, out float h, out float l, out float s);
        float newHue = h + hueOffset;
        float newSaturation = Math.Clamp(s + saturationOffset, 0, 1);
        float newLuminance = Math.Clamp(l + luminanceOffset, 0, 1);
        HlsToRgb(newHue, newLuminance, newSaturation, out float re, out float gr, out float bl);
        return (a << 24) | ((uint)ToByte(bl) << 16) | ((uint)ToByte(gr) << 8) | ToByte(re);
    }

    public static void RgbToHls(float r, float g, float b, out float h, out float l, out float s)
    {
        float cmax = Math.Max(r, Math.Max(g, b));
        float cmin = Math.Min(r, Math.Min(g, b));
        l = (cmax + cmin) / 2;
        if (cmax == cmin)
        {
            h = 0;
            s = 0;
            return;
        }

        float d = cmax - cmin;
        s = l < 0.5f ? d / (cmax + cmin) : d / (2 - cmax - cmin);
        if (r == cmax)
        {
            h = (g - b) / d;
        }
        else if (g == cmax)
        {
            h = 2 + ((b - r) / d);
        }
        else
        {
            h = 4 + ((r - g) / d);
        }

        h /= 6;
        if (h < 0)
        {
            h += 1;
        }
    }

    public static void HlsToRgb(float h, float l, float s, out float r, out float g, out float b)
    {
        if (s == 0)
        {
            r = g = b = l;
            return;
        }

        float m2 = l <= 0.5f ? l * (1 + s) : l + s - (l * s);
        float m1 = (2 * l) - m2;
        r = HueToColorValue(h + (1 / 3f));
        g = HueToColorValue(h);
        b = HueToColorValue(h - (1 / 3f));

        float HueToColorValue(float hue)
        {
            hue -= MathF.Floor(hue);
            if (6 * hue < 1)
            {
                return m1 + ((m2 - m1) * hue * 6);
            }

            if (2 * hue < 1)
            {
                return m2;
            }

            return 3 * hue < 2 ? m1 + ((m2 - m1) * ((2 / 3f) - hue) * 6) : m1;
        }
    }

    /// <summary>Round do Delphi (empate para o par), guardado num Byte.</summary>
    private static byte ToByte(float value) => unchecked((byte)(int)MathF.Round(value * 255, MidpointRounding.ToEven));
}
