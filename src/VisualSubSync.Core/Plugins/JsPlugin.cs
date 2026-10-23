using System.Globalization;
using System.Text;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Plugins;

/// <summary>
/// TBaseJavascriptPlugin (JavaScriptPluginUnit.pas): um motor JavaScript por script, com as funções
/// globais ScriptLog e LoadScript (inclusão relativa ao arquivo em carga, com curingas) e os objetos
/// VSSCore e SceneChange. Os erros vão para <see cref="PluginError"/> no formato
/// "mensagem in arquivo at line n".
/// </summary>
public class JsPlugin
{
    private readonly Stack<string> _loadingStack = new();

    protected JsPlugin(PluginContext context)
    {
        Context = context;
        // GetPropInfo do jsbridge não diferencia maiúsculas: sub.start vale sub.Start.
        Engine = new Engine(options => options.LimitRecursion(1000)
            .SetTypeResolver(new TypeResolver { MemberNameComparer = StringComparer.OrdinalIgnoreCase }));
        Engine.SetValue("ScriptLog", new Action<JsValue>(v => PluginError?.Invoke(TypeConverter.ToString(v))));
        Engine.SetValue("LoadScript", new Action<JsValue>(v => InternalLoadScripts(TypeConverter.ToString(v))));
        Engine.SetValue("VSSCore", new VssCoreJs(context));
        Engine.SetValue("SceneChange", new SceneChangeJs(context));
    }

    protected PluginContext Context { get; }

    protected Engine Engine { get; }

    public string FileName { get; private set; } = string.Empty;

    public string LastErrorMessage { get; private set; } = string.Empty;

    /// <summary>A última chamada terminou com erro (o original para a verificação desse plugin).</summary>
    public bool FatalError { get; private set; }

    /// <summary>A última chamada terminou num erro de script.</summary>
    protected bool LastCallFailed { get; private set; }

    /// <summary>OnJSPluginError: LogForm.LogMsg no original (ScriptLog também vem por aqui).</summary>
    public event Action<string>? PluginError;

    /// <summary>VSSPlugin, depois da carga.</summary>
    protected ObjectInstance? PluginObject { get; private set; }

    public virtual bool LoadScript(string fileName)
    {
        FileName = fileName;
        if (!InternalLoadScript(fileName))
        {
            return false;
        }

        PluginObject = Engine.GetValue("VSSPlugin") as ObjectInstance;
        return PluginObject is not null;
    }

    /// <summary>CallJSFunction: chama uma função global sem parâmetros.</summary>
    public void CallFunction(string name)
    {
        if (Engine.GetValue(name) is { } fn && fn.IsCallable())
        {
            Call(fn, JsValue.Undefined);
        }
    }

    /// <summary>Eval.</summary>
    public void Eval(string code) => Guard(() => Engine.Evaluate(code));

    protected JsValue Function(string name) =>
        PluginObject?.Get(name) is { } v && v.IsCallable() ? v : JsValue.Undefined;

    /// <summary>Chama a função com VSSPlugin como this; um erro vai para o log e devolve undefined.</summary>
    protected JsValue Call(JsValue function, JsValue thisValue, params JsValue[] arguments)
    {
        // FatalError fica ligado depois de um erro, como FFatalError no OnJsError do original;
        // LastCallFailed diz só se esta chamada falhou.
        JsValue result = JsValue.Undefined;
        bool fatal = FatalError;
        FatalError = false;
        Guard(() => result = Engine.Call(function, thisValue, arguments));
        LastCallFailed = FatalError;
        FatalError |= fatal;
        return result;
    }

    protected JsValue CallPlugin(string name, params JsValue[] arguments) =>
        Function(name) is { } fn && !fn.IsUndefined() ? Call(fn, PluginObject!, arguments) : JsValue.Undefined;

    protected JsValue Wrap(Subtitle? sub) => sub is null ? JsValue.Null : JsValue.FromObject(Engine, new SubtitleJs(Context, sub));

    /// <summary>GetProperty(..., WideString): null fica vazio e undefined vira "undefined", como no jsbridge.</summary>
    protected static string StringProperty(ObjectInstance obj, string name)
    {
        var v = obj.Get(name);
        return v.IsNull() ? string.Empty : TypeConverter.ToString(v);
    }

    protected static int IntProperty(ObjectInstance obj, string name)
    {
        var v = obj.Get(name);
        return v.IsNull() || v.IsUndefined() ? 0 : JsConvert.ToInt32(TypeConverter.ToNumber(v));
    }

    private bool InternalLoadScripts(string fileName)
    {
        if (!fileName.Contains('*', StringComparison.Ordinal))
        {
            return InternalLoadScript(fileName);
        }

        fileName = Resolve(fileName);
        string folder = Path.GetDirectoryName(fileName) ?? string.Empty;
        if (Directory.Exists(folder))
        {
            foreach (string file in Directory.GetFiles(folder, Path.GetFileName(fileName)).Order(StringComparer.OrdinalIgnoreCase))
            {
                InternalLoadScript(file);
            }
        }

        return true;
    }

    private string Resolve(string fileName) =>
        Path.IsPathRooted(fileName)
            ? fileName
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(CurrentLoadingFile) ?? string.Empty, fileName));

    private string CurrentLoadingFile => _loadingStack.Count > 0 ? _loadingStack.Peek() : FileName;

    private bool InternalLoadScript(string fileName)
    {
        fileName = Resolve(fileName);
        if (!File.Exists(fileName))
        {
            PluginError?.Invoke("Can't find " + fileName);
            return false;
        }

        string code = ReadScript(fileName);
        _loadingStack.Push(fileName);
        try
        {
            FatalError = false;
            return Guard(() => Engine.Execute(code, fileName));
        }
        finally
        {
            _loadingStack.Pop();
        }
    }

    /// <summary>TJSScript.LoadRaw: UTF-8, UTF-16 ou UTF-32 com BOM; sem BOM, ANSI.</summary>
    internal static string ReadScript(string fileName)
    {
        byte[] bytes = File.ReadAllBytes(fileName);
        ReadOnlySpan<byte> b = bytes;
        if (b.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            return Encoding.UTF8.GetString(b[3..]);
        }

        if (b.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE, 0x00, 0x00]))
        {
            return Encoding.UTF32.GetString(b[4..]);
        }

        if (b.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
        {
            return Encoding.Unicode.GetString(b[2..]);
        }

        if (b.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            return Encoding.BigEndianUnicode.GetString(b[2..]);
        }

        return TextFileReader.Ansi.GetString(bytes);
    }

    /// <summary>OnJsError: registra o erro e marca FatalError.</summary>
    private bool Guard(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (JavaScriptException ex)
        {
            // "TypeError: mensagem", sem o caminho que o Jint junta à mensagem; nos erros de sintaxe,
            // a linha do código vem junto, como o uclinebuf do SpiderMonkey.
            string message = ex.Message;
            bool syntax = false;
            if (ex.Error is ObjectInstance error)
            {
                string name = TypeConverter.ToString(error.Get("name"));
                syntax = name == "SyntaxError";
                message = name + ": " + TypeConverter.ToString(error.Get("message"));
            }

            string? file = ex.Location.SourceFile;
            int line = ex.Location.Start.Line;
            Report(message, file, line, syntax ? SourceLine(string.IsNullOrEmpty(file) ? CurrentLoadingFile : file, line) : null);
        }
        catch (Acornima.ParseErrorException ex)
        {
            // Erro de sintaxe: sempre no arquivo que está sendo carregado.
            Report(ex.Error.Description, null, ex.Error.LineNumber, SourceLine(CurrentLoadingFile, ex.Error.LineNumber));
        }
        catch (JintException ex)
        {
            Report(ex.Message, null, 0, null);
        }
        catch (Exception ex) when (ex is InvalidCastException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or OverflowException)
        {
            Report(ex.Message, null, 0, null);
        }

        return false;
    }

    private static string? SourceLine(string? file, int line)
    {
        try
        {
            return file is not null && File.Exists(file) && line > 0
                ? ReadScript(file).Split('\n').ElementAtOrDefault(line - 1)?.TrimEnd('\r')
                : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void Report(string message, string? file, int line, string? lineBuffer)
    {
        FatalError = true;
        string current = string.IsNullOrEmpty(file) ? CurrentLoadingFile : file;
        LastErrorMessage = string.Create(CultureInfo.InvariantCulture, $"{message} in {Path.GetFileName(current)} at line {line}");
        if (!string.IsNullOrEmpty(lineBuffer))
        {
            LastErrorMessage += "\r\n-> " + lineBuffer;
        }

        PluginError?.Invoke(LastErrorMessage);
    }
}
