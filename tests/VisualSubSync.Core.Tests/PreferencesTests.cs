using VisualSubSync.Core.Hotkeys;
using VisualSubSync.Core.Plugins;
using VisualSubSync.Core.Text;
using VisualSubSync.Core.Settings;
using VisualSubSync.Core.Waveform;

namespace VisualSubSync.Core.Tests;

public class PreferencesTests
{
    [Fact]
    public void Defaults_follow_TConfigObject_SetDefault()
    {
        var settings = VssSettings.Load(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.ini"));
        Assert.Equal(WheelModifier.Ctrl, settings.MouseWheelTimeScrollModifier);
        Assert.Equal(WheelModifier.Shift, settings.MouseWheelVZoomModifier);
        Assert.Equal(WheelModifier.None, settings.MouseWheelHZoomModifier);
        Assert.True(settings.EnableBackup);
        Assert.Equal(0, settings.AutoBackupEvery);
        Assert.False(settings.AutoSaveWhenPlaying);
    }

    [Fact]
    public void Save_writes_every_preference_like_SaveIni()
    {
        string path = Path.Combine(Path.GetTempPath(), $"vss-{Guid.NewGuid():N}.ini");
        try
        {
            File.WriteAllText(path, "[Hotkeys]\r\nActionPlay[Normal]=F5\r\n");
            var settings = VssSettings.Load(path);
            Assert.Equal(DelphiShortcut.FromText("F5"), settings.Hotkeys.Get("ActionPlay")!.NormalShortcut);
            settings.MouseWheelHZoomModifier = WheelModifier.Alt;
            settings.AutoBackupEvery = 5;
            settings.EnableBackup = false;
            settings.SpaceKeyCpsTarget = 21;
            settings.SubListFont = new FontSpec("Tahoma", 9, 1, 0, "clRed");
            settings.SceneChangeFilterOffset = 300;
            settings.DetachedVideo = true;
            settings.ShowVideo = true;
            settings.Save();

            string text = File.ReadAllText(path);
            Assert.Contains("UseIntegerShortcut=1", text, StringComparison.Ordinal);
            Assert.Contains("ActionPlay[Normal]=116", text, StringComparison.Ordinal);
            Assert.Contains("ActionPlay[Timing]=112", text, StringComparison.Ordinal);
            Assert.Contains("WheelHZoomModifier=1", text, StringComparison.Ordinal);
            Assert.Contains("SubList=Tahoma,9,1,0,clRed", text, StringComparison.Ordinal);
            Assert.Contains("ShowTextInWAVDisplay=1", text, StringComparison.Ordinal);

            var again = VssSettings.Load(path);
            Assert.Equal(WheelModifier.Alt, again.MouseWheelHZoomModifier);
            Assert.Equal(5, again.AutoBackupEvery);
            Assert.False(again.EnableBackup);
            Assert.Equal(21, again.SpaceKeyCpsTarget);
            Assert.Equal(300, again.SceneChangeFilterOffset);
            Assert.True(again.DetachedVideo);
            Assert.True(again.ShowVideo);
            Assert.Equal(DelphiShortcut.FromText("F5"), again.Hotkeys.Get("ActionPlay")!.NormalShortcut);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Wheel_resolves_first_matching_action_in_original_order()
    {
        // Padrão do 1.0.1: roda sozinha = zoom horizontal, Ctrl = rolagem, Shift = zoom vertical.
        var (h, v, t) = (WheelModifier.None, WheelModifier.Shift, WheelModifier.Ctrl);
        Assert.Equal(WheelAction.HorizontalZoom, WheelActions.Resolve(h, v, t, shift: false, alt: false, ctrl: false));
        Assert.Equal(WheelAction.TimeScroll, WheelActions.Resolve(h, v, t, shift: false, alt: false, ctrl: true));
        Assert.Equal(WheelAction.VerticalZoom, WheelActions.Resolve(h, v, t, shift: true, alt: false, ctrl: false));
        Assert.Equal(WheelAction.VerticalZoom, WheelActions.Resolve(h, v, t, shift: true, alt: false, ctrl: true));
        Assert.Equal(WheelAction.Nothing, WheelActions.Resolve(h, v, t, shift: false, alt: true, ctrl: false));
        // O zoom horizontal vem antes dos outros quando a mesma tecla serve a dois.
        Assert.Equal(WheelAction.HorizontalZoom,
            WheelActions.Resolve(WheelModifier.Ctrl, WheelModifier.Ctrl, WheelModifier.None, shift: false, alt: false, ctrl: true));
    }

    [Fact]
    public void Hotkey_editor_sorts_by_caption_and_moves_a_shortcut()
    {
        var table = HotkeyTable.CreateDefault();
        var editor = new HotkeyEditor(table);
        var captions = editor.Entries.Select(e => e.Caption).ToList();
        Assert.Equal(captions.OrderBy(c => c, StringComparer.CurrentCulture), captions);

        var play = editor.Entries.Single(e => e.ActionName == "ActionPlay");
        var zoomIn = editor.Entries.Single(e => e.ActionName == "ActionZoomIn");
        int f1 = DelphiShortcut.FromText("F1");
        editor.Set(zoomIn, f1);
        Assert.Equal(f1, zoomIn.NormalShortcut);
        Assert.Equal(0, play.NormalShortcut);
        Assert.Equal(f1, play.TimingShortcut);

        editor.TimingMode = true;
        editor.Clear(play);
        Assert.Equal(0, play.TimingShortcut);

        // A tabela só muda com Assign (OK).
        Assert.Equal(f1, table.Get("ActionPlay")!.NormalShortcut);
        table.Assign(editor.Entries);
        Assert.Equal(0, table.Get("ActionPlay")!.NormalShortcut);
        Assert.Equal(f1, table.Get("ActionZoomIn")!.NormalShortcut);

        editor.ResetAll();
        Assert.Equal(f1, play.NormalShortcut);
        Assert.Equal(f1, play.TimingShortcut);
        Assert.Equal(0, zoomIn.NormalShortcut);
    }

    [Fact]
    public void Hotkey_defaults_ignore_ini_values()
    {
        var ini = IniDocument.Parse("[Hotkeys]\r\nActionUndo[Normal]=Ctrl+U\r\nJSAction_X[Normal]=Ctrl+K\r\n");
        var table = HotkeyTable.CreateDefault();
        table.Load(ini);
        var js = table.AddAction("JSAction_X", "[js] X", DelphiShortcut.FromText("Ctrl+M"), ini);
        var undo = table.Get("ActionUndo")!;
        Assert.Equal(DelphiShortcut.FromText("Ctrl+U"), undo.NormalShortcut);
        Assert.Equal(DelphiShortcut.FromText("Ctrl+Z"), undo.DefaultNormalShortcut);
        Assert.Equal(DelphiShortcut.FromText("Ctrl+K"), js.NormalShortcut);
        Assert.Equal(DelphiShortcut.FromText("Ctrl+M"), js.DefaultNormalShortcut);
        Assert.Equal("Undo", undo.Caption);
    }

    [Fact]
    public void Plugin_copies_are_independent_until_assigned()
    {
        var settings = new PluginSettings();
        settings.Plugins.Add(new PluginInfo
        {
            Name = "Too long",
            Params = [new JsPluginParam { Name = "MaxPerLine", ParamType = JsPluginParamType.Integer, IntegerValue = 40 }],
        });
        var copy = settings.Plugins[0].Clone();
        copy.Enabled = false;
        copy.Params[0].SetValueFromString("45");
        Assert.True(settings.Plugins[0].Enabled);
        Assert.Equal(40, settings.Plugins[0].Params[0].IntegerValue);
        Assert.Throws<FormatException>(() => copy.Params[0].SetValueFromString("4x"));

        settings.Assign([copy]);
        Assert.False(settings.IsEnabled("Too long"));
        Assert.Equal(45, settings.Plugins[0].GetParam("MaxPerLine")!.IntegerValue);
    }

    [Fact]
    public void ColorToString_names_vcl_colors_or_writes_hex()
    {
        Assert.Equal("clBlack", FontSpec.ColorToString(0));
        Assert.Equal("clRed", FontSpec.ColorToString(0x0000FF));
        Assert.Equal("clMoneyGreen", FontSpec.ColorToString(0xC0DCC0));
        Assert.Equal("$00123456", FontSpec.ColorToString(0x123456));
        Assert.Equal(0x563412, new FontSpec("Arial", 8, 0, 0, "$00123456").ToRgb());
    }

    [Fact]
    public void Preset_overrides_present_keys_but_keeps_server_backup_and_fonts()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"vss-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string main = Path.Combine(dir, "VisualSubSync.ini");
            File.WriteAllText(main, "[Misc]\r\nSpaceKeyCPSTarget=20\r\nSpaceKeyMinimalDuration=900\r\n");
            var settings = VssSettings.Load(main);
            string preset = Path.Combine(dir, "fansub.ini");
            File.WriteAllText(preset,
                "[Misc]\r\nSpaceKeyCPSTarget=14\r\n" +
                "[WebServer]\r\nPort=9999\r\n" +
                "[Backup]\r\nAutoBackupEvery=7\r\n" +
                "[Fonts]\r\nSubList=Tahoma,12,0,0,clRed\r\n" +
                "[Mouse]\r\nWheelHZoomModifier=1\r\n" +
                "[Hotkeys]\r\nActionPlay[Normal]=F6\r\n" +
                "[WAVDisplay]\r\nShowTextInWAVDisplay=0\r\n");
            int port = settings.WebServerPort;
            var font = settings.SubListFont;

            settings.LoadPreset(preset);

            Assert.Equal(14, settings.SpaceKeyCpsTarget);
            Assert.Equal(900, settings.SpaceKeyMinimalDuration);
            Assert.Equal(port, settings.WebServerPort);
            Assert.Equal(0, settings.AutoBackupEvery);
            Assert.Equal(font, settings.SubListFont);
            Assert.Equal(WheelModifier.Alt, settings.MouseWheelHZoomModifier);
            Assert.Equal(DelphiShortcut.FromText("F6"), settings.Hotkeys.Get("ActionPlay")!.NormalShortcut);
            Assert.False(settings.ShowTextInWavDisplay);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
