using VisualSubSync.Core.Settings;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Tests;

public class StyleEditorTests
{
    private static List<SsaStyle> TwoStyles() =>
    [
        new SsaStyle { Name = "Default", FontName = "Arial", FontSize = 28, Alignment = 2, Outline = 1.5, PrimaryColor = 0x82C0C0C0 },
        new SsaStyle { Name = "Top", FontName = "Tahoma", FontSize = 20, Alignment = 8, Bold = -1 },
    ];

    [Fact]
    public void Show_fills_fields_like_DataToForm_and_disables_apply()
    {
        var editor = new StyleEditor(TwoStyles(), assMode: true);
        editor.PreSelect("top");
        editor.Show();
        Assert.Equal(1, editor.SelectedIndex);
        Assert.True(editor.ControlsEnabled);
        Assert.False(editor.StylesChanged);
        Assert.False(editor.CanApply);
        var f = editor.Fields;
        Assert.Equal("Top", f.Name);
        Assert.Equal("Tahoma,20,1,1,clBlack", f.FontCaption);
        Assert.Equal((1, 0), (f.HAlign, f.VAlign));

        editor.SelectedIndex = 0;
        editor.ShowSelection();
        Assert.Equal("1.5", editor.Fields.Outline);
        Assert.Equal((1, 2), (editor.Fields.HAlign, editor.Fields.VAlign));
        Assert.Equal(0x82C0C0C0u, editor.Fields.Colors[0]);
        Assert.False(editor.CanApply);
    }

    [Fact]
    public void PreSelect_keeps_previous_or_picks_first()
    {
        var editor = new StyleEditor(TwoStyles(), assMode: false);
        editor.PreSelect(string.Empty);
        Assert.Equal(0, editor.SelectedIndex);
        editor.SelectedIndex = 1;
        editor.PreSelect("Missing");
        Assert.Equal(1, editor.SelectedIndex);
    }

    [Fact]
    public void Apply_checks_name_renames_subtitles_and_writes_fields()
    {
        var styles = TwoStyles();
        var editor = new StyleEditor(styles, assMode: true) { SelectedIndex = 0 };
        editor.Show();
        var renames = new List<(string, string)>();

        editor.Fields.Name = "A,B";
        editor.CheckChange();
        Assert.True(editor.CanApply);
        Assert.Equal("Comma is not an authorized character in style name.", editor.Apply((o, n) => renames.Add((o, n))));

        editor.Fields.Name = "TOP";
        Assert.Equal("This style name already exists", editor.Apply((o, n) => renames.Add((o, n))));
        Assert.Empty(renames);

        editor.Fields.Name = "Main";
        editor.Fields.HAlign = 2;
        editor.Fields.VAlign = 0;
        editor.Fields.MarginL = "x";
        editor.Fields.ScaleX = "90.5";
        Assert.Null(editor.Apply((o, n) => renames.Add((o, n))));
        Assert.Equal([("Default", "Main")], renames);
        Assert.Equal("Main", styles[0].Name);
        Assert.Equal(9, styles[0].Alignment);
        Assert.Equal(0, styles[0].MarginL);
        Assert.Equal(90.5, styles[0].ScaleX);
        Assert.True(editor.StylesChanged);
        Assert.False(editor.CanApply);
    }

    [Fact]
    public void Font_size_is_rounded_so_a_fractional_size_enables_apply()
    {
        var styles = TwoStyles();
        styles[0].FontSize = 20.5;
        var editor = new StyleEditor(styles, assMode: true) { SelectedIndex = 0 };
        editor.Show();
        Assert.Equal(20, editor.Fields.Font.SizePt);
        Assert.True(editor.CanApply);
    }

    [Fact]
    public void New_copy_and_delete_follow_the_original()
    {
        var styles = TwoStyles();
        var editor = new StyleEditor(styles, assMode: true) { SelectedIndex = 1 };
        editor.Show();
        editor.Copy();
        Assert.Equal("Top Copy", styles[2].Name);
        Assert.Equal(2, editor.SelectedIndex);
        editor.New();
        Assert.Equal("New Style", styles[3].Name);
        Assert.Equal(0x0000FFFFu, styles[3].PrimaryColor);
        Assert.Equal(3, editor.SelectedIndex);

        editor.Delete();
        Assert.Equal(2, editor.SelectedIndex);
        editor.SelectedIndex = 0;
        editor.Delete();
        editor.Delete();
        editor.Delete();
        Assert.Equal("Default", Assert.Single(styles).Name);
        Assert.Equal(0, editor.SelectedIndex);
        Assert.True(editor.StylesChanged);
    }

    [Fact]
    public void Invalid_alignment_keeps_the_previous_buttons()
    {
        var styles = TwoStyles();
        styles[1].Alignment = 0;
        var editor = new StyleEditor(styles, assMode: true) { SelectedIndex = 0 };
        editor.Show();
        editor.SelectedIndex = 1;
        editor.ShowSelection();
        Assert.Equal((1, 2), (editor.Fields.HAlign, editor.Fields.VAlign));
        Assert.True(editor.CanApply);
    }

    [Fact]
    public void Hsl_offsets_change_all_colors_from_the_base()
    {
        var styles = TwoStyles();
        styles[1].PrimaryColor = 0x000000FF; // vermelho
        styles[1].OutlineColor = 0x80FF0000; // azul com alfa
        var editor = new StyleEditor(styles, assMode: true) { SelectedIndex = 1 };
        editor.Show();
        Assert.Equal("H=120", editor.SetHsl(0, 120));
        Assert.Equal(0x0000FF00u, editor.Fields.Colors[0]);
        Assert.Equal(0x800000FFu, editor.Fields.Colors[2]);
        Assert.True(editor.CanApply);
        Assert.Equal("L=-50", editor.SetHsl(2, -50));
        Assert.Equal(0x00000000u, editor.Fields.Colors[0]);

        editor.SetColor(1, 0x00FFFFFF);
        Assert.Equal(editor.Fields.Colors, editor.Fields.BaseColors);
        Assert.Equal(-50, editor.Fields.Luminance);
    }

    [Fact]
    public void ChangeColorHsl_matches_jcl_math()
    {
        Assert.Equal(0x00FFFFFFu, HslColor.ChangeColorHsl(0x00808080, 0, 0, 0.5f));
        Assert.Equal(0x00808080u, HslColor.ChangeColorHsl(0x00808080, 0.3f, 0, 0));
        Assert.Equal(0xFF8080FFu, HslColor.ChangeColorHsl(0xFF0000FF, 0, 0, 0.25f));
        Assert.Equal(0x00BF4040u, HslColor.ChangeColorHsl(0x00FF0000, 0, -0.5f, 0));
    }

    [Fact]
    public void SetFont_updates_caption_and_ignores_color_on_apply()
    {
        var styles = TwoStyles();
        var editor = new StyleEditor(styles, assMode: true) { SelectedIndex = 0 };
        editor.Show();
        editor.SetFont(new FontSpec("Verdana", 30, 2 | 4, 0, "clRed"));
        Assert.Equal("Verdana,30,6,0,clRed", editor.Fields.FontCaption);
        Assert.Null(editor.Apply((_, _) => { }));
        Assert.Equal(("Verdana", 30.0, 0, -1, -1), (styles[0].FontName, styles[0].FontSize, styles[0].Bold, styles[0].Italic, styles[0].Underline));
        Assert.Equal("Verdana,30,6,0,clRed", editor.Fields.FontCaption);
    }
}
