using Jint;
using Jint.Native;
using Jint.Runtime;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Plugins;

/// <summary>
/// TSimpleJavascriptWrapper (JavaScriptPluginUnit.pas): o plugin geral
/// (<c>jsplugin\general\general_plugin.js</c>). Recebe os avisos de mudança e de seleção, os duplos
/// cliques nas bordas da legenda, descreve as colunas extras da lista e executa as ações [js].
/// </summary>
public sealed class GeneralPlugin : JsPlugin
{
    private string[] _columnsTitle = [];
    private int[] _columnsSize = [];
    private bool[] _columnsBgColorized = [];
    private bool[] _columnsCustomText = [];

    public GeneralPlugin(PluginContext context)
        : base(context) =>
        Engine.SetValue("SetStatusBarText", new Action<JsValue>(v => StatusBarText?.Invoke(TypeConverter.ToString(v))));

    /// <summary>CoreColumnsCount: COLUMN_COUNT da lista (#, Start, Stop, Style, Text e VO).</summary>
    public int CoreColumnsCount { get; init; } = 6;

    public int ExtraColumnsCount { get; private set; }

    /// <summary>OnJSPluginSetStatusBarText: SetStatusBarText(texto) do script.</summary>
    public event Action<string>? StatusBarText;

    /// <summary>Carrega o script; só vale se o VSSPlugin tiver OnSubtitleModification ou OnSelectedSubtitle.</summary>
    public override bool LoadScript(string fileName)
    {
        if (!base.LoadScript(fileName))
        {
            return false;
        }

        if (!Function("GetExtraColumnsCount").IsUndefined())
        {
            ExtraColumnsCount = JsConvert.ToInt32(TypeConverter.ToNumber(CallPlugin("GetExtraColumnsCount")));
        }

        if (ExtraColumnsCount > 0)
        {
            int count = CoreColumnsCount + ExtraColumnsCount;
            _columnsTitle = Query("GetColumnTitle", count, TypeConverter.ToString) ?? [];
            _columnsSize = Query("GetColumnSize", count, v => JsConvert.ToInt32(TypeConverter.ToNumber(v))) ?? [];
            _columnsBgColorized = Query("IsColumnBGColorized", count, TypeConverter.ToBoolean) ?? [];
            _columnsCustomText = Query("HasColumnCustomText", count, TypeConverter.ToBoolean) ?? [];
        }

        return !Function("OnSubtitleModification").IsUndefined() || !Function("OnSelectedSubtitle").IsUndefined();
    }

    /// <summary>Chama a função para cada índice de coluna; null quando o script não a tem.</summary>
    private T[]? Query<T>(string name, int count, Func<JsValue, T> convert)
    {
        if (Function(name).IsUndefined())
        {
            return null;
        }

        var result = new T[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = convert(CallPlugin(name, i));
        }

        return result;
    }

    public void NotifySubtitleModification(Subtitle current, Subtitle? previous, Subtitle? next) =>
        CallPlugin("OnSubtitleModification", Wrap(current), Wrap(previous), Wrap(next));

    public void NotifySelectionModification(Subtitle current, Subtitle? previous, Subtitle? next) =>
        CallPlugin("OnSelectedSubtitle", Wrap(current), Wrap(previous), Wrap(next));

    public void NotifyRangeStartDblClick(Subtitle current, Subtitle? previous, Subtitle? next) =>
        CallPlugin("OnRangeStartDblClick", Wrap(current), Wrap(previous), Wrap(next));

    public void NotifyRangeStopDblClick(Subtitle current, Subtitle? previous, Subtitle? next) =>
        CallPlugin("OnRangeStopDblClick", Wrap(current), Wrap(previous), Wrap(next));

    public string GetColumnTitle(int index) => index >= 0 && index < _columnsTitle.Length ? _columnsTitle[index] : string.Empty;

    public int GetColumnSize(int index) => index >= 0 && index < _columnsSize.Length ? _columnsSize[index] : 50;

    public bool IsColumnBgColorized(int index) => index >= 0 && index < _columnsBgColorized.Length && _columnsBgColorized[index];

    public bool HasColumnCustomText(int index) => index >= 0 && index < _columnsCustomText.Length && _columnsCustomText[index];

    /// <summary>GetColumnBgColor: a cor já na ordem do Windows (JSColorToTColor).</summary>
    public int GetColumnBgColor(int index, Subtitle current, Subtitle? previous, Subtitle? next)
    {
        var value = CallPlugin("GetColumnBGColor", index, Wrap(current), Wrap(previous), Wrap(next));
        return ErrorPlugin.ToTColor(value.IsUndefined() || value.IsNull() ? 0 : JsConvert.ToInt32(TypeConverter.ToNumber(value)));
    }

    public string GetColumnText(int index, Subtitle current, Subtitle? previous, Subtitle? next)
    {
        var value = CallPlugin("GetColumnText", index, Wrap(current), Wrap(previous), Wrap(next));
        return value.IsUndefined() || value.IsNull() ? string.Empty : TypeConverter.ToString(value);
    }

    /// <summary>OnJavascriptAction: <c>Eval(nome + '.onExecute();')</c>.</summary>
    public void ExecuteAction(string objectName) => Eval(objectName + ".onExecute();");
}
