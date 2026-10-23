using System.Globalization;
using VisualSubSync.Core.Projects;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Plugins;

/// <summary>TJSPluginInfo (PreferencesFormUnit.pas): o que as preferências guardam de um plugin de erro.</summary>
public sealed class PluginInfo
{
    public bool Enabled { get; set; } = true;

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    /// <summary>Cor já na ordem do Windows (JSColorToTColor).</summary>
    public int Color { get; init; }

    public List<JsPluginParam> Params { get; init; } = [];

    public JsPluginParam? GetParam(string name) => Params.Find(p => p.Name == name);

    /// <summary>TJSPluginInfo.Assign: a cópia local que a janela de preferências edita.</summary>
    public PluginInfo Clone() => new()
    {
        Enabled = Enabled,
        Name = Name,
        Description = Description,
        Message = Message,
        Color = Color,
        Params = [.. Params.Select(p => p.Clone())],
    };
}

/// <summary>
/// ListJSPlugin do TConfigObject: UpdatePluginList (um item por plugin de erro, ativado e com os
/// valores do script), a leitura e a gravação no VisualSubSync.ini (uma seção com o nome do plugin,
/// "Enabled" e um valor por parâmetro) e ApplyParam.
/// </summary>
public sealed class PluginSettings
{
    public List<PluginInfo> Plugins { get; } = [];

    /// <summary>
    /// SaveConfig, parte Error plugin: as cópias editadas voltam para a lista. O original copia por
    /// posição de uma lista ordenada para outra, o que só reordena a lista; aqui a cópia é por nome.
    /// </summary>
    public void Assign(IEnumerable<PluginInfo> edited)
    {
        ArgumentNullException.ThrowIfNull(edited);
        foreach (var e in edited)
        {
            int i = Plugins.FindIndex(p => p.Name == e.Name);
            if (i >= 0)
            {
                Plugins[i] = e.Clone();
            }
        }
    }

    /// <summary>UpdatePluginList.</summary>
    public static PluginSettings Build(string pluginPath, PluginContext context, Action<string>? onError)
    {
        var settings = new PluginSettings();
        foreach (var plugin in PluginEnumerator.Enumerate(pluginPath, context, onError))
        {
            settings.Plugins.Add(new PluginInfo
            {
                Name = plugin.Name,
                Description = plugin.Description,
                Color = ErrorPlugin.ToTColor(plugin.Color),
                Message = plugin.Message,
                Params = plugin.GetParams(),
            });
        }

        return settings;
    }

    /// <summary>GetJSPluginInfoByName.</summary>
    public PluginInfo? GetByName(string name) => Plugins.Find(p => p.Name == name);

    /// <summary>Um plugin sem item nas preferências conta como ativado.</summary>
    public bool IsEnabled(string name) => GetByName(name)?.Enabled ?? true;

    /// <summary>Assigned(JSPluginInfo) and JSPluginInfo.Enabled: a regra de CanFixError e de FixErrorInList.</summary>
    public bool IsListedAndEnabled(string name) => GetByName(name)?.Enabled == true;

    /// <summary>ApplyParam: passa ao script os valores das preferências.</summary>
    public void ApplyParams(ErrorPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        if (GetByName(plugin.Name) is { } info)
        {
            foreach (var p in info.Params)
            {
                plugin.SetParamValue(p.Name, p.ValueAsString);
            }
        }
    }

    public void Load(IniDocument ini)
    {
        ArgumentNullException.ThrowIfNull(ini);
        foreach (var info in Plugins)
        {
            info.Enabled = ini.GetBool(info.Name, "Enabled", info.Enabled);
            foreach (var p in info.Params)
            {
                switch (p.ParamType)
                {
                    case JsPluginParamType.Boolean:
                        p.BooleanValue = ini.GetBool(info.Name, p.Name, p.BooleanValue);
                        break;
                    case JsPluginParamType.Integer:
                        p.IntegerValue = ini.GetInt(info.Name, p.Name, p.IntegerValue);
                        break;
                    case JsPluginParamType.Double:
                        // ReadFloat: StrToFloat na cultura atual; um valor inválido mantém o padrão.
                        if (ini.Get(info.Name, p.Name) is { } text && double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double d))
                        {
                            p.DoubleValue = d;
                        }

                        break;
                    case JsPluginParamType.String:
                        if (ini.Get(info.Name, p.Name) is { } s)
                        {
                            p.StringValue = RecentFiles.Utf8Decode(s);
                        }

                        break;
                }
            }
        }
    }

    public void Save(IniDocument ini)
    {
        ArgumentNullException.ThrowIfNull(ini);
        foreach (var info in Plugins)
        {
            ini.SetBool(info.Name, "Enabled", info.Enabled);
            foreach (var p in info.Params)
            {
                switch (p.ParamType)
                {
                    case JsPluginParamType.Boolean:
                        ini.SetBool(info.Name, p.Name, p.BooleanValue);
                        break;
                    case JsPluginParamType.Integer:
                        ini.SetInt(info.Name, p.Name, p.IntegerValue);
                        break;
                    case JsPluginParamType.Double:
                        ini.Set(info.Name, p.Name, p.DoubleValue.ToString("G15", CultureInfo.CurrentCulture));
                        break;
                    case JsPluginParamType.String:
                        ini.Set(info.Name, p.Name, RecentFiles.Utf8Encode(p.StringValue));
                        break;
                }
            }
        }
    }
}
