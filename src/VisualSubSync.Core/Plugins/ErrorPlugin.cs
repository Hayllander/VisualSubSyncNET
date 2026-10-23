using System.Globalization;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Plugins;

/// <summary>TJSPluginParamType (jsptBoolean, jsptInteger, jsptDouble, jsptWideString).</summary>
#pragma warning disable CA1720 // Os nomes dos tipos são os do original.
public enum JsPluginParamType
{
    Unknown,
    Boolean,
    Integer,
    Double,
    String,
}
#pragma warning restore CA1720

/// <summary>TJSPluginParam: um parâmetro "Param..." do VSSPlugin.</summary>
public sealed class JsPluginParam
{
    public required string Name { get; init; }

    public JsPluginParamType ParamType { get; init; }

    public bool BooleanValue { get; set; }

    public int IntegerValue { get; set; }

    public double DoubleValue { get; set; }

    public string StringValue { get; set; } = string.Empty;

    public string Unit { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public JsPluginParam Clone() => (JsPluginParam)MemberwiseClone();

    /// <summary>GetParamValueAsWideString: BoolToStr (-1/0), IntToStr e FloatToStr na cultura atual.</summary>
    public string ValueAsString => ParamType switch
    {
        JsPluginParamType.Boolean => BooleanValue ? "-1" : "0",
        JsPluginParamType.Integer => IntegerValue.ToString(CultureInfo.InvariantCulture),
        JsPluginParamType.Double => DoubleValue.ToString("G15", CultureInfo.CurrentCulture),
        JsPluginParamType.String => StringValue,
        _ => string.Empty,
    };

    /// <summary>SetParamValueAsWideString; um valor inválido levanta FormatException, como StrToInt.</summary>
    public void SetValueFromString(string value)
    {
        switch (ParamType)
        {
            case JsPluginParamType.Boolean:
                BooleanValue = ParseBool(value);
                break;
            case JsPluginParamType.Integer:
                IntegerValue = int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                break;
            case JsPluginParamType.Double:
                DoubleValue = double.Parse(value, NumberStyles.Float, CultureInfo.CurrentCulture);
                break;
            case JsPluginParamType.String:
                StringValue = value;
                break;
        }
    }

    /// <summary>StrToBool: "True"/"False" ou um número (diferente de zero é verdadeiro).</summary>
    internal static bool ParseBool(string value)
    {
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return double.Parse(value, NumberStyles.Float, CultureInfo.CurrentCulture) != 0;
    }
}

/// <summary>
/// TJavaScriptPlugin: plugin de verificação de erros. O VSSPlugin precisa ter HasError; FixError é
/// opcional. Name, Description, Color e Message são lidos na carga.
/// </summary>
public sealed class ErrorPlugin : JsPlugin
{
    public ErrorPlugin(PluginContext context)
        : base(context)
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>Cor RGB do JavaScript (0xRRGGBB); <see cref="ToTColor"/> converte para a ordem do Windows.</summary>
    public int Color { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public bool CanFixError => !Function("FixError").IsUndefined();

    public override bool LoadScript(string fileName)
    {
        if (!base.LoadScript(fileName) || PluginObject is not { } plugin)
        {
            return false;
        }

        Name = StringProperty(plugin, "Name");
        Description = StringProperty(plugin, "Description");
        Color = IntProperty(plugin, "Color");
        Message = StringProperty(plugin, "Message");
        // É preciso pelo menos HasError.
        return !Function("HasError").IsUndefined();
    }

    /// <summary>JSColorToTColor: 0xRRGGBB para 0xBBGGRR.</summary>
    public static int ToTColor(int rgb) => (rgb & 0x00FF00) | ((rgb & 0x0000FF) << 16) | ((rgb & 0xFF0000) >> 16);

    /// <summary>HasError: a descrição do erro, ou vazio. Um erro de script devolve vazio e marca FatalError.</summary>
    public string HasError(Subtitle current, Subtitle? previous, Subtitle? next)
    {
        var fn = Function("HasError");
        if (fn.IsUndefined())
        {
            return string.Empty;
        }

        var result = Call(fn, PluginObject!, Wrap(current), Wrap(previous), Wrap(next));
        return LastCallFailed ? string.Empty : TypeConverter.ToString(result);
    }

    public void FixError(Subtitle current, Subtitle? previous, Subtitle? next)
    {
        var fn = Function("FixError");
        if (!fn.IsUndefined())
        {
            Call(fn, PluginObject!, Wrap(current), Wrap(previous), Wrap(next));
        }
    }

    /// <summary>FillParamList: as propriedades "Param..." cujo Value é número, texto ou booleano.</summary>
    public List<JsPluginParam> GetParams()
    {
        var list = new List<JsPluginParam>();
        if (PluginObject is not { } plugin)
        {
            return list;
        }

        foreach (var key in plugin.GetOwnPropertyKeys(Jint.Runtime.Types.String))
        {
            string name = key.ToString();
            if (!name.StartsWith("Param", StringComparison.Ordinal) || plugin.Get(name) is not ObjectInstance param)
            {
                continue;
            }

            string description = StringProperty(param, "Description");
            var value = param.Get("Value");
            JsPluginParam? p = value.Type switch
            {
                Jint.Runtime.Types.Number when IsInteger(value) => new JsPluginParam { Name = name, ParamType = JsPluginParamType.Integer, IntegerValue = (int)value.AsNumber(), Unit = StringProperty(param, "Unit"), Description = description == "undefined" ? string.Empty : description },
                Jint.Runtime.Types.Number => new JsPluginParam { Name = name, ParamType = JsPluginParamType.Double, DoubleValue = value.AsNumber(), Unit = StringProperty(param, "Unit"), Description = description == "undefined" ? string.Empty : description },
                Jint.Runtime.Types.String => new JsPluginParam { Name = name, ParamType = JsPluginParamType.String, StringValue = value.AsString(), Unit = StringProperty(param, "Unit"), Description = description == "undefined" ? string.Empty : description },
                Jint.Runtime.Types.Boolean => new JsPluginParam { Name = name, ParamType = JsPluginParamType.Boolean, BooleanValue = value.AsBoolean(), Unit = StringProperty(param, "Unit"), Description = description == "undefined" ? string.Empty : description },
                _ => null,
            };
            if (p is not null)
            {
                list.Add(p);
            }
        }

        return list;
    }

    /// <summary>JSVAL_IS_INT: um número inteiro que cabe nos 31 bits do jsval.</summary>
    private static bool IsInteger(JsValue value)
    {
        double d = value.AsNumber();
        return d == Math.Floor(d) && Math.Abs(d) < (1 << 30) && !(d == 0 && double.IsNegative(d));
    }

    /// <summary>SetParamValue: converte o texto para o tipo atual de Value e o grava no plugin.</summary>
    public void SetParamValue(string name, string value)
    {
        if (PluginObject?.Get(name) is not ObjectInstance param || !param.HasProperty("Value"))
        {
            return;
        }

        var current = param.Get("Value");
        JsValue? newValue = current.Type switch
        {
            Jint.Runtime.Types.Number when IsInteger(current) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : null,
            Jint.Runtime.Types.Number => double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out double d) ? d : null,
            Jint.Runtime.Types.String => value,
            Jint.Runtime.Types.Boolean => TryParseBool(value),
            _ => null,
        };
        if (newValue is not null)
        {
            param.Set("Value", newValue);
        }
    }

    private static JsValue? TryParseBool(string value)
    {
        try
        {
            return JsPluginParam.ParseBool(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

/// <summary>
/// TJavaScriptPluginEnumerator: carrega cada *.js da pasta de plugins (em ordem de nome); os que não
/// carregam ou não têm HasError são pulados.
/// </summary>
public static class PluginEnumerator
{
    public static IEnumerable<ErrorPlugin> Enumerate(string pluginPath, PluginContext context, Action<string>? onError)
    {
        if (!Directory.Exists(pluginPath))
        {
            yield break;
        }

        foreach (string file in Directory.GetFiles(pluginPath, "*.js").Order(StringComparer.OrdinalIgnoreCase))
        {
            if (Load(file, context, onError) is { } plugin)
            {
                yield return plugin;
            }
        }
    }

    /// <summary>GetPluginByFilename.</summary>
    public static ErrorPlugin? Load(string fileName, PluginContext context, Action<string>? onError)
    {
        var plugin = new ErrorPlugin(context);
        if (onError is not null)
        {
            plugin.PluginError += onError;
        }

        return plugin.LoadScript(fileName) ? plugin : null;
    }
}
