using VisualSubSync.Core.Settings;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Subtitles;

/// <summary>
/// O estado dos campos de TStyleForm: textos dos edits, fonte da prévia, alinhamentos e as quatro cores
/// dos painéis, além da base e dos deslocamentos H/S/L usados pelos botões de matiz.
/// </summary>
public sealed class StyleFormFields
{
    public string Name { get; set; } = string.Empty;

    /// <summary>stFontPreview.Font: nome, tamanho, estilo, charset (DEFAULT_CHARSET) e cor.</summary>
    public FontSpec Font { get; set; } = new("Arial", 16, 0, 1, "clWindowText");

    /// <summary>stFontPreview.Caption (vazio depois de ClearForm).</summary>
    public string FontCaption { get; set; } = string.Empty;

    /// <summary>rgHAlignment: 0 = Left, 1 = Center, 2 = Right, -1 = nada.</summary>
    public int HAlign { get; set; } = -1;

    /// <summary>rgVAlignment: 0 = Top, 1 = Middle, 2 = Bottom, -1 = nada.</summary>
    public int VAlign { get; set; } = -1;

    public string MarginL { get; set; } = string.Empty;

    public string MarginR { get; set; } = string.Empty;

    public string MarginV { get; set; } = string.Empty;

    public string Outline { get; set; } = string.Empty;

    public string Shadow { get; set; } = string.Empty;

    public string ScaleX { get; set; } = string.Empty;

    public string ScaleY { get; set; } = string.Empty;

    public string Spacing { get; set; } = string.Empty;

    public string Angle { get; set; } = string.Empty;

    /// <summary>pnlPrimaryColor, pnlSecondaryColor, pnlOutlineColor e pnlBackColor (valor ABGR guardado).</summary>
    public uint[] Colors { get; } = new uint[4];

    /// <summary>FPrimaryColor..FBackColor: as cores sobre as quais H/S/L são aplicados.</summary>
    public uint[] BaseColors { get; } = new uint[4];

    /// <summary>FHue (0..360), FSaturation e FLuminance (-100..100).</summary>
    public double Hue { get; set; }

    public double Saturation { get; set; }

    public double Luminance { get; set; }
}

/// <summary>
/// TStyleForm (StyleFormUnit.pas) sem a interface: a lista de estilos do documento é editada no lugar,
/// como no original, e os campos seguem DataToForm/FormToData.
/// </summary>
public sealed class StyleEditor
{
    public StyleEditor(List<SsaStyle> styles, bool assMode)
    {
        ArgumentNullException.ThrowIfNull(styles);
        Styles = styles;
        AssMode = assMode;
    }

    /// <summary>lstStyles: os estilos do documento, na ordem do arquivo.</summary>
    public List<SsaStyle> Styles { get; }

    /// <summary>ConfigureMode: Scale X/Y, Spacing e Angle só no ASS, assim como os efeitos da fonte.</summary>
    public bool AssMode { get; }

    /// <summary>lstStyles.ItemIndex.</summary>
    public int SelectedIndex { get; set; } = -1;

    /// <summary>EnableControls (FormCreate começa desabilitado).</summary>
    public bool ControlsEnabled { get; private set; }

    /// <summary>HaveStylesChanged.</summary>
    public bool StylesChanged { get; private set; }

    /// <summary>bttApply.Enabled e bttReset.Enabled, calculados por CheckChange.</summary>
    public bool CanApply { get; private set; } = true;

    public StyleFormFields Fields { get; } = new();

    public SsaStyle? SelectedStyle => SelectedIndex >= 0 && SelectedIndex < Styles.Count ? Styles[SelectedIndex] : null;

    /// <summary>PreSelect: o estilo com esse nome (sem distinguir maiúsculas), ou o primeiro se nada está selecionado.</summary>
    public void PreSelect(string styleName)
    {
        int index = IndexOf(styleName);
        if (index != -1)
        {
            SelectedIndex = index;
        }
        else if (SelectedIndex == -1 && Styles.Count > 0)
        {
            SelectedIndex = 0;
        }
    }

    /// <summary>FormShow.</summary>
    public void Show()
    {
        if (Styles.Count > 0)
        {
            ControlsEnabled = true;
            ShowSelection();
        }

        StylesChanged = false;
        ShowSelection();
    }

    /// <summary>ShowSelection (clique na lista e Reset).</summary>
    public void ShowSelection()
    {
        if (SelectedStyle is { } style)
        {
            DataToForm(style);
            CheckChange();
        }
    }

    /// <summary>bttNewClick: um estilo com os valores padrão de TSSAStyle.Create.</summary>
    public void New()
    {
        Styles.Add(new SsaStyle());
        SelectedIndex = Styles.Count - 1;
        ShowSelection();
        ControlsEnabled = true;
        StylesChanged = true;
    }

    /// <summary>bttCopyClick: cópia do estilo selecionado com " Copy" no nome.</summary>
    public void Copy()
    {
        if (SelectedStyle is { } style)
        {
            var copy = style.Clone();
            copy.Name = style.Name + " Copy";
            Styles.Add(copy);
            SelectedIndex = Styles.Count - 1;
            ShowSelection();
            StylesChanged = true;
        }
    }

    /// <summary>bttDeleteClick: sem estilos, volta um "Default"; a seleção fica na mesma posição.</summary>
    public void Delete()
    {
        if (SelectedStyle is null)
        {
            return;
        }

        int index = SelectedIndex;
        Styles.RemoveAt(index);
        StylesChanged = true;
        if (Styles.Count == 0)
        {
            Styles.Add(new SsaStyle { Name = "Default" });
        }

        if (index >= Styles.Count)
        {
            index = Styles.Count - 1;
        }

        SelectedIndex = index;
        ShowSelection();
    }

    /// <summary>
    /// bttApplyClick (UpdateSelectionData): devolve a mensagem de erro quando o nome tem vírgula ou já
    /// existe. Um nome novo é trocado também nas legendas por <paramref name="renameStyle"/>.
    /// </summary>
    public string? Apply(Action<string, string> renameStyle)
    {
        ArgumentNullException.ThrowIfNull(renameStyle);
        if (SelectedStyle is not { } style)
        {
            return null;
        }

        if (Fields.Name.Contains(',', StringComparison.Ordinal))
        {
            return "Comma is not an authorized character in style name.";
        }

        if (style.Name != Fields.Name)
        {
            if (IndexOf(Fields.Name) != -1)
            {
                return "This style name already exists";
            }

            renameStyle(style.Name, Fields.Name);
        }

        FormToData(style);
        StylesChanged = true;
        CheckChange();
        ClearHsl();
        return null;
    }

    /// <summary>bttFontClick: a fonte escolhida vai para a prévia.</summary>
    public void SetFont(FontSpec font)
    {
        ArgumentNullException.ThrowIfNull(font);
        Fields.Font = font;
        Fields.FontCaption = font.ToString();
        CheckChange();
    }

    /// <summary>pnlColorClick: a cor escolhida no painel; as quatro cores passam a ser a base do H/S/L.</summary>
    public void SetColor(int panel, uint color)
    {
        Fields.Colors[panel] = color;
        Array.Copy(Fields.Colors, Fields.BaseColors, 4);
        CheckChange();
    }

    /// <summary>OnPopupTrackbarValueChanged: 0 = H, 1 = S, 2 = L. Devolve o novo texto do botão.</summary>
    public string SetHsl(int component, double value)
    {
        string text;
        switch (component)
        {
            case 0:
                text = "H=";
                Fields.Hue = value;
                break;
            case 1:
                text = "S=";
                Fields.Saturation = value;
                break;
            default:
                text = "L=";
                Fields.Luminance = value;
                break;
        }

        float h = (float)(Fields.Hue / 360.0), s = (float)(Fields.Saturation / 100.0), l = (float)(Fields.Luminance / 100.0);
        for (int i = 0; i < 4; i++)
        {
            Fields.Colors[i] = HslColor.ChangeColorHsl(Fields.BaseColors[i], h, s, l);
        }

        CheckChange();
        return text + RoundDelphi(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>CheckChange: Apply e Reset só ficam habilitados quando os campos diferem do estilo.</summary>
    public void CheckChange()
    {
        if (Styles.Count > 0 && SelectedStyle is { } style)
        {
            var maybeChanged = style.Clone();
            FormToData(maybeChanged);
            CanApply = !SameValues(maybeChanged, style);
        }
    }

    /// <summary>FormToData.</summary>
    public void FormToData(SsaStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        var f = Fields;
        style.Name = f.Name;
        style.FontName = f.Font.Name;
        style.FontSize = f.Font.SizePt;
        style.Bold = f.Font.Bold ? -1 : 0;
        style.Italic = f.Font.Italic ? -1 : 0;
        style.Underline = f.Font.Underline ? -1 : 0;
        style.Strikeout = f.Font.Strikeout ? -1 : 0;

        // 7 8 9 / 4 5 6 / 1 2 3; sem um dos dois alinhamentos marcado, o valor fica como está.
        if (f.HAlign is >= 0 and <= 2 && f.VAlign is >= 0 and <= 2)
        {
            style.Alignment = ((2 - f.VAlign) * 3) + f.HAlign + 1;
        }

        style.MarginR = DelphiText.StrToIntDef(f.MarginR, 0);
        style.MarginL = DelphiText.StrToIntDef(f.MarginL, 0);
        style.MarginV = DelphiText.StrToIntDef(f.MarginV, 0);
        style.PrimaryColor = f.Colors[0];
        style.SecondaryColor = f.Colors[1];
        style.OutlineColor = f.Colors[2];
        style.BackColor = f.Colors[3];
        style.Outline = DelphiText.StrToFloatDef(f.Outline, 0);
        style.Shadow = DelphiText.StrToFloatDef(f.Shadow, 0);
        style.ScaleX = DelphiText.StrToFloatDef(f.ScaleX, 0);
        style.ScaleY = DelphiText.StrToFloatDef(f.ScaleY, 0);
        style.Spacing = DelphiText.StrToFloatDef(f.Spacing, 0);
        style.Angle = DelphiText.StrToFloatDef(f.Angle, 0);
    }

    /// <summary>DataToForm. Um alinhamento fora de 1..9 deixa os botões como estavam, como no original.</summary>
    private void DataToForm(SsaStyle style)
    {
        var f = Fields;
        f.Name = style.Name;
        int fontStyle = (style.Bold != 0 ? 1 : 0) | (style.Italic != 0 ? 2 : 0) |
                        (style.Underline != 0 ? 4 : 0) | (style.Strikeout != 0 ? 8 : 0);
        f.Font = f.Font with { Name = style.FontName, SizePt = (int)RoundDelphi(style.FontSize), Style = fontStyle, Color = "clBlack" };
        f.FontCaption = f.Font.ToString();

        if (style.Alignment is >= 1 and <= 9)
        {
            f.HAlign = (style.Alignment - 1) % 3;
            f.VAlign = 2 - ((style.Alignment - 1) / 3);
        }

        f.MarginL = style.MarginL.ToString(System.Globalization.CultureInfo.InvariantCulture);
        f.MarginR = style.MarginR.ToString(System.Globalization.CultureInfo.InvariantCulture);
        f.MarginV = style.MarginV.ToString(System.Globalization.CultureInfo.InvariantCulture);
        f.Colors[0] = style.PrimaryColor;
        f.Colors[1] = style.SecondaryColor;
        f.Colors[2] = style.OutlineColor;
        f.Colors[3] = style.BackColor;
        f.Outline = DelphiText.FloatToStr(style.Outline);
        f.Shadow = DelphiText.FloatToStr(style.Shadow);
        f.ScaleX = DelphiText.FloatToStr(style.ScaleX);
        f.ScaleY = DelphiText.FloatToStr(style.ScaleY);
        f.Spacing = DelphiText.FloatToStr(style.Spacing);
        f.Angle = DelphiText.FloatToStr(style.Angle);
        ClearHsl();
    }

    /// <summary>ClearHSLBtt: deslocamentos zerados e as cores atuais como base.</summary>
    public void ClearHsl()
    {
        Fields.Hue = 0;
        Fields.Saturation = 0;
        Fields.Luminance = 0;
        Array.Copy(Fields.Colors, Fields.BaseColors, 4);
    }

    /// <summary>TStrings.IndexOf: comparação sem distinguir maiúsculas (AnsiCompareText).</summary>
    private int IndexOf(string name) =>
        Styles.FindIndex(s => StringComparer.CurrentCultureIgnoreCase.Equals(s.Name, name));

    /// <summary>TSSAStyle.Equals: todos os campos (o Encoding e o AlphaLevel também).</summary>
    public static bool SameValues(SsaStyle a, SsaStyle b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return a.Name == b.Name && a.FontName == b.FontName && a.FontSize.Equals(b.FontSize) &&
               a.PrimaryColor == b.PrimaryColor && a.SecondaryColor == b.SecondaryColor &&
               a.OutlineColor == b.OutlineColor && a.BackColor == b.BackColor && a.Bold == b.Bold &&
               a.Italic == b.Italic && a.Underline == b.Underline && a.Strikeout == b.Strikeout &&
               a.ScaleX.Equals(b.ScaleX) && a.ScaleY.Equals(b.ScaleY) && a.Spacing.Equals(b.Spacing) &&
               a.Angle.Equals(b.Angle) && a.BorderStyle == b.BorderStyle && a.Outline.Equals(b.Outline) &&
               a.Shadow.Equals(b.Shadow) && a.Alignment == b.Alignment && a.MarginL == b.MarginL &&
               a.MarginR == b.MarginR && a.MarginV == b.MarginV && a.AlphaLevel == b.AlphaLevel &&
               a.Encoding == b.Encoding;
    }

    private static long RoundDelphi(double value) => (long)Math.Round(value, MidpointRounding.ToEven);
}
