using VisualSubSync.Core.Scenes;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Plugins;

/// <summary>
/// TVSSCoreEngineIntf (SubStructUnit.pas): o que os plugins leem da janela principal. A ordem das
/// legendas é a da lista (vtvSubsList), que é a mesma do documento.
/// </summary>
public interface IVssCoreEngine
{
    IReadOnlyList<Subtitle> Subtitles { get; }

    /// <summary>As legendas selecionadas, na ordem da lista.</summary>
    IReadOnlyList<Subtitle> SelectedSubtitles { get; }

    void RegisterJavascriptAction(string name, string caption, string defaultShortcut);

    int GetTextSelectionStart();

    int GetTextSelectionLength();

    int GetAudioCursorPosition();

    /// <summary>MeasureStringWidth: largura em pixels do texto na fonte pedida (GetTextExtentPoint32W).</summary>
    int MeasureStringWidth(string fontName, int fontSize, bool bold, string text);
}

/// <summary>
/// O estado comum a todos os plugins: g_VSSCoreWrapper (constantes, preferências e a janela principal)
/// e g_SceneChangeWrapper (as mudanças de cena e os seus offsets).
/// </summary>
public sealed class PluginContext
{
    public IVssCoreEngine? Engine { get; set; }

    // Constantes de coluna (Set_*_COL_IDX).
    public int IndexColumn { get; set; }

    public int StartColumn { get; set; } = 1;

    public int StopColumn { get; set; } = 2;

    public int StyleColumn { get; set; } = 3;

    public int TextColumn { get; set; } = 4;

    public int VoColumn { get; set; } = 5;

    public int LastCoreColumn { get; set; } = 5;

    // ApplyMiscSettings.
    public int CpsTarget { get; set; } = 18;

    public int MinimumDuration { get; set; } = 1000;

    public int MaximumDuration { get; set; } = 6000;

    public int MinimumBlank { get; set; } = 120;

    public int VideoWidth { get; set; }

    public int VideoHeight { get; set; }

    /// <summary>SetSceneChangeList: tempos em ordem crescente.</summary>
    public IReadOnlyList<int> SceneChanges { get; set; } = [];

    public int SceneChangeStartOffset { get; set; } = 130;

    public int SceneChangeStopOffset { get; set; } = 130;

    public int SceneChangeFilterOffset { get; set; } = 250;

    public bool SceneChangeVisible { get; set; }

    /// <summary>OnSubtitleChangeStart/Stop/Text: chamados antes de o plugin mudar a legenda (para o desfazer).</summary>
    public event Action<Subtitle, int>? ChangingStart;

    public event Action<Subtitle, int>? ChangingStop;

    public event Action<Subtitle, string>? ChangingText;

    internal void OnChangingStart(Subtitle sub, int value) => ChangingStart?.Invoke(sub, value);

    internal void OnChangingStop(Subtitle sub, int value) => ChangingStop?.Invoke(sub, value);

    internal void OnChangingText(Subtitle sub, string value) => ChangingText?.Invoke(sub, value);

    internal int SceneIndex(int timeMs, bool backward) => SceneChangeList.BinarySearch(SceneChanges, timeMs, backward);
}
