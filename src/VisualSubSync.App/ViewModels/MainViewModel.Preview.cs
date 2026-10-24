using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Media;

namespace VisualSubSync.App.ViewModels;

/// <summary>
/// Legenda sobre o vídeo: CurrentProject.IsDirty com o evento OnDirtySet
/// (CurrentProjectOnDirtySet), VideoPreviewNeedSubtitleUpdate e ActionShowHideSubs.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>VideoPreviewNeedSubtitleUpdate: a cópia temporária das legendas está atrasada.</summary>
    private bool _videoPreviewNeedSubtitleUpdate;

    /// <summary>DisableVideoUpdatePreview: ligado durante o arraste de uma borda na onda.</summary>
    private bool _disableVideoUpdatePreview;

    /// <summary>ShowingSubs (MenuShowHideSubtitles.Checked).</summary>
    [ObservableProperty]
    private bool _showingSubs = true;

    private VideoSubtitleStyle CurrentVideoSubtitleStyle => new(
        Settings.SubVideoFont,
        Settings.TransparencySubVideoFont,
        Settings.OutlineSubVideoFont,
        Settings.OutlineSubVideoColorFont,
        Settings.TransparencyOutlineSubVideoFont,
        Settings.ShadowSubVideoFont,
        Settings.ShadowSubVideoColorFont,
        Settings.TransparencyShadowSubVideoFont);

    /// <summary>
    /// No preview atual o estilo vai embutido no ASS temporário; assim evitamos que o mpv force outro
    /// tamanho/estilo por cima e ficamos mais próximos do renderer do Enhanced.
    /// </summary>
    private void ApplyVideoSubtitleStyle()
    {
        _ = _engine;
    }

    private SubtitleDocument CreateVideoPreviewDocument()
    {
        var preview = new SubtitleDocument
        {
            Header = _document.Header,
            Footer = _document.Footer,
            IsUtf8 = true,
        };

        preview.Styles.Add(CreateVideoPreviewStyle());
        foreach (Subtitle item in _document.Items)
        {
            var clone = item.Clone();
            clone.Style = "Default";
            preview.Items.Add(clone);
        }

        return preview;
    }

    private SsaStyle CreateVideoPreviewStyle()
    {
        var font = Settings.SubVideoFont;
        uint primary = ToAssAbgr(font.ToRgb() ?? 0xFFFFFF, Settings.TransparencySubVideoFont, sourceIsRgb: true);
        return new SsaStyle
        {
            Name = "Default",
            FontName = font.Name,
            FontSize = font.SizePt,
            PrimaryColor = primary,
            SecondaryColor = primary,
            OutlineColor = ToAssAbgr(Settings.OutlineSubVideoColorFont, Settings.TransparencyOutlineSubVideoFont, sourceIsRgb: false),
            BackColor = ToAssAbgr(Settings.ShadowSubVideoColorFont, Settings.TransparencyShadowSubVideoFont, sourceIsRgb: false),
            Bold = font.Bold ? -1 : 0,
            Italic = font.Italic ? -1 : 0,
            Underline = font.Underline ? -1 : 0,
            Strikeout = font.Strikeout ? -1 : 0,
            Outline = Settings.OutlineSubVideoFont,
            Shadow = Settings.ShadowSubVideoFont,
            Alignment = 2,
            MarginL = 15,
            MarginR = 15,
            MarginV = 15,
            Encoding = font.Charset,
        };
    }

    private static uint ToAssAbgr(int color, int transparency, bool sourceIsRgb)
    {
        uint alpha = (uint)(Math.Clamp(transparency, 0, 10) * 255 / 10);
        uint red = sourceIsRgb ? (uint)((color >> 16) & 0xFF) : (uint)(color & 0xFF);
        uint green = sourceIsRgb ? (uint)((color >> 8) & 0xFF) : (uint)((color >> 8) & 0xFF);
        uint blue = sourceIsRgb ? (uint)(color & 0xFF) : (uint)((color >> 16) & 0xFF);
        return (alpha << 24) | (blue << 16) | (green << 8) | red;
    }

    /// <summary>
    /// CurrentProject.IsDirty (TVSSProject.SetIsDirty): cada vez que passa a verdadeiro, mesmo já sendo,
    /// a legenda do vídeo é atualizada (CurrentProjectOnDirtySet).
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
                SaveCommand.NotifyCanExecuteChanged();
            }

            if (value)
            {
                UpdateSubtitleForPreview(false);
                _videoPreviewNeedSubtitleUpdate = true;
                CallJsOnSubtitleModification();
            }
        }
    }

    /// <summary>
    /// ActionShowHideSubsExecute: mostra ou esconde a legenda no vídeo; com o vídeo à mostra, atualiza a
    /// legenda (tocando ou pausado) ou mostra o quadro do início da legenda focada.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ShowHideSubs()
    {
        ShowingSubs = !ShowingSubs;
        if (_engine is { IsOpen: true } engine)
        {
            engine.SubtitlesVisible = ShowingSubs;
            ApplyVideoSubtitleStyle();
        }

        if (ShowingVideo)
        {
            if (_engine is { IsPlaying: true } || (IsPlaying && !_playTimer.IsEnabled))
            {
                UpdateSubtitleForPreview(true);
            }
            else
            {
                ShowStartFrame();
            }
        }
    }
}
