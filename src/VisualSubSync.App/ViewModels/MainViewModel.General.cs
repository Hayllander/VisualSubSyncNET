using System.IO;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Plugins;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.App.ViewModels;

/// <summary>Uma ação [js] registrada pelo plugin geral (TJavascriptAction).</summary>
/// <param name="Name">Nome do objeto JavaScript (JSObjectName), também o nome da ação nos atalhos.</param>
/// <param name="Caption">Texto do menu JS Tools (a ação tem "[js] " na frente).</param>
public sealed record JsActionInfo(string Name, string Caption);

/// <summary>Uma coluna extra da lista declarada pelo plugin geral.</summary>
/// <param name="Index">Índice da coluna na lista do original (LAST_CORE_COL_INDEX + 1 + i).</param>
public sealed record ExtraColumnInfo(int Index, string Title, int MinWidth, bool BackgroundColorized, bool CustomText);

/// <summary>
/// O plugin geral (GeneralJSPlugin, InitGeneralJSPlugin): avisos de mudança e de seleção, duplo clique
/// nas bordas da legenda, colunas extras, SetStatusBarText e as ações do menu JS Tools.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>INDEX_COL_INDEX..VO_COL_INDEX: as colunas do núcleo no original.</summary>
    public const int CoreColumnsCount = 6;

    private readonly List<JsActionInfo> _jsActions = [];
    private GeneralPlugin? _generalPlugin;
    private bool _generalPluginLoaded;

    /// <summary>StatusBarPrimaryText (SetStatusBarText) e StatusBarSecondaryText (ShowStatusBarMessage).</summary>
    private string _statusBarPrimaryText = string.Empty;
    private string _statusBarSecondaryText = string.Empty;

    /// <summary>MenuItemJSTools: na ordem em que o script as registrou.</summary>
    public IReadOnlyList<JsActionInfo> JsActions => _jsActions;

    /// <summary>InitVTVExtraColumns.</summary>
    public IReadOnlyList<ExtraColumnInfo> ExtraColumns { get; private set; } = [];

    /// <summary>
    /// InitGeneralJSPlugin: chamado antes de a janela montar os atalhos, como no .dpr (antes de
    /// LoadSettings), para que as ações [js] entrem na tabela de atalhos.
    /// </summary>
    public void InitGeneralPlugin()
    {
        var context = PluginContext;
        string file = Path.Combine(PluginPath, "general", "general_plugin.js");
        _generalPlugin = new GeneralPlugin(context) { CoreColumnsCount = CoreColumnsCount };
        if (!File.Exists(file))
        {
            _dialogs.LogMessage("Can't find " + file, silent: true);
            return;
        }

        _generalPlugin.PluginError += LogPluginError;
        if (_generalPlugin.LoadScript(file))
        {
            _generalPluginLoaded = true;
            _generalPlugin.StatusBarText += SetStatusBarPrimaryText;
            var plugin = _generalPlugin;
            ExtraColumns = [.. Enumerable.Range(context.LastCoreColumn + 1, plugin.ExtraColumnsCount).Select(i =>
                new ExtraColumnInfo(i, plugin.GetColumnTitle(i), plugin.GetColumnSize(i), plugin.IsColumnBgColorized(i), plugin.HasColumnCustomText(i)))];
        }
        else
        {
            _dialogs.LogMessage("Can't load " + file, silent: true);
        }
    }

    /// <summary>TMainForm.RegisterJavascriptAction (VSSCore.RegisterJavascriptAction).</summary>
    private void RegisterJavascriptAction(string name, string caption, string defaultShortcut)
    {
        // Um segundo registro com o mesmo nome daria erro de nome duplicado na VCL; aqui é ignorado.
        if (_jsActions.Exists(a => a.Name == name))
        {
            return;
        }

        _jsActions.Add(new JsActionInfo(name, caption));
        Settings.AddJavascriptHotkey(name, caption, defaultShortcut);
    }

    /// <summary>OnJavascriptAction: executa <c>nome.onExecute()</c>; as mudanças podem ser desfeitas.</summary>
    [RelayCommand]
    private void ExecuteJsAction(string name)
    {
        if (_generalPlugin is { } plugin)
        {
            RunGeneralPluginChanges(() => plugin.ExecuteAction(name));
        }
    }

    /// <summary>WAVDisplayer1RangeStartDblClick / RangeStopDblClick.</summary>
    public void OnWaveformRangeEdgeDoubleClick(int index, bool isStart)
    {
        if (!_generalPluginLoaded || _generalPlugin is not { } plugin || index < 0 || index >= _document.Items.Count)
        {
            return;
        }

        var (current, previous, next) = Neighbors(index);
        RunGeneralPluginChanges(() =>
        {
            if (isStart)
            {
                plugin.NotifyRangeStartDblClick(current, previous, next);
            }
            else
            {
                plugin.NotifyRangeStopDblClick(current, previous, next);
            }
        });
    }

    /// <summary>
    /// O trecho comum das ações e dos duplos cliques: com mudanças, a tarefa vai para a pilha e
    /// UpdateAfterJSChange atualiza a tela, mantendo a seleção do editor.
    /// </summary>
    private void RunGeneralPluginChanges(Action run)
    {
        var selection = _editorSelection;
        string? textBefore = SelectedSubtitle?.Text;
        RunPluginChanges(run);
        if (SelectedSubtitle is { } item && item.Text != textBefore)
        {
            EditorSelectionRequested?.Invoke(this, selection);
        }
    }

    /// <summary>CallJSOnSubtitleModification: a legenda selecionada na onda, ou a focada na lista.</summary>
    private void CallJsOnSubtitleModification()
    {
        if (!_generalPluginLoaded || _generalPlugin is not { } plugin)
        {
            return;
        }

        int index = Waveform?.SelectedRangeIndex ?? -1;
        if (index < 0 && SelectedSubtitle is { } item)
        {
            index = Subtitles.IndexOf(item);
        }

        if (index >= 0 && index < _document.Items.Count)
        {
            var (current, previous, next) = Neighbors(index);
            plugin.NotifySubtitleModification(current, previous, next);
        }
    }

    /// <summary>CallJSOnSelectedSubtitle: a legenda focada na lista.</summary>
    private void CallJsOnSelectedSubtitle(SubtitleItemViewModel item)
    {
        if (_generalPluginLoaded && _generalPlugin is { } plugin && Subtitles.IndexOf(item) is int index and >= 0)
        {
            var (current, previous, next) = Neighbors(index);
            plugin.NotifySelectionModification(current, previous, next);
        }
    }

    /// <summary>GetCurrentPreviousNextSubtitles.</summary>
    private (Subtitle Current, Subtitle? Previous, Subtitle? Next) Neighbors(int index) =>
        (_document.Items[index], index > 0 ? _document.Items[index - 1] : null, index < _document.Items.Count - 1 ? _document.Items[index + 1] : null);

    /// <summary>vtvSubsListGetText de uma coluna extra (vazia quando o plugin não dá texto).</summary>
    public string ExtraColumnText(SubtitleItemViewModel item, int column)
    {
        ArgumentNullException.ThrowIfNull(item);
        int index = item.Index - 1;
        if (!_generalPluginLoaded || _generalPlugin is not { } plugin || !plugin.HasColumnCustomText(column) || index < 0 || index >= _document.Items.Count)
        {
            return string.Empty;
        }

        var (current, previous, next) = Neighbors(index);
        return plugin.GetColumnText(column, current, previous, next);
    }

    /// <summary>vtvSubsListBeforeCellPaint: a cor de fundo (TColor) de uma coluna extra, ou null.</summary>
    public int? ExtraColumnColor(SubtitleItemViewModel item, int column)
    {
        ArgumentNullException.ThrowIfNull(item);
        int index = item.Index - 1;
        if (!_generalPluginLoaded || _generalPlugin is not { } plugin || !plugin.IsColumnBgColorized(column) || index < 0 || index >= _document.Items.Count)
        {
            return null;
        }

        var (current, previous, next) = Neighbors(index);
        return plugin.GetColumnBgColor(column, current, previous, next);
    }

    /// <summary>SetStatusBarPrimaryText.</summary>
    private void SetStatusBarPrimaryText(string text)
    {
        _statusBarPrimaryText = text;
        UpdateStatusBar();
    }

    /// <summary>UpdateStatusBar: "mensagem (texto do plugin)", ou só um dos dois.</summary>
    private void UpdateStatusBar() =>
        StatusText = _statusBarPrimaryText.Length > 0
            ? _statusBarSecondaryText.Length > 0 ? _statusBarSecondaryText + " (" + _statusBarPrimaryText + ")" : _statusBarPrimaryText
            : _statusBarSecondaryText;
}
