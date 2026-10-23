using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.Core.Plugins;

/// <summary>Um erro encontrado (TErrorReportForm.AddError).</summary>
/// <param name="Color">Cor do plugin na ordem do Windows.</param>
public sealed record ErrorItem(Subtitle Subtitle, int Color, string PluginMessage, string ErrorMessage, string PluginFileName, string PluginName);

/// <summary>ActionCheckErrorsExecute e ActionFixErrorMainExecute.</summary>
public static class ErrorChecker
{
    /// <summary>
    /// Cada plugin ativado olha cada legenda (com a anterior e a próxima); um erro de script interrompe
    /// só aquele plugin.
    /// </summary>
    public static List<ErrorItem> Check(IReadOnlyList<Subtitle> subtitles, string pluginPath, PluginContext context, PluginSettings settings, Action<string>? onError)
    {
        ArgumentNullException.ThrowIfNull(subtitles);
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<ErrorItem>();
        foreach (var plugin in PluginEnumerator.Enumerate(pluginPath, context, onError))
        {
            if (!settings.IsEnabled(plugin.Name))
            {
                continue;
            }

            settings.ApplyParams(plugin);
            for (int i = 0; i < subtitles.Count; i++)
            {
                var current = subtitles[i];
                string result = plugin.HasError(current, i > 0 ? subtitles[i - 1] : null, i < subtitles.Count - 1 ? subtitles[i + 1] : null);
                if (result.Length > 0)
                {
                    errors.Add(new ErrorItem(current, ErrorPlugin.ToTColor(plugin.Color), plugin.Message, result, plugin.FileName, plugin.Name));
                }

                if (plugin.FatalError)
                {
                    break;
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Corrige as legendas pedidas com todos os plugins ativados: cada um primeiro confere HasError e só
    /// então chama FixError. As mudanças passam pelos eventos do contexto (para o desfazer).
    /// </summary>
    public static void FixSubtitles(IReadOnlyList<Subtitle> subtitles, IEnumerable<Subtitle> toFix, string pluginPath, PluginContext context, PluginSettings settings, Action<string>? onError)
    {
        ArgumentNullException.ThrowIfNull(subtitles);
        ArgumentNullException.ThrowIfNull(settings);
        var targets = toFix.ToList();
        foreach (var plugin in PluginEnumerator.Enumerate(pluginPath, context, onError))
        {
            if (!settings.IsEnabled(plugin.Name))
            {
                continue;
            }

            settings.ApplyParams(plugin);
            foreach (var current in targets)
            {
                int i = SubtitleJs.IndexOf(subtitles, current);
                var previous = i > 0 ? subtitles[i - 1] : null;
                var next = i >= 0 && i < subtitles.Count - 1 ? subtitles[i + 1] : null;
                if (plugin.HasError(current, previous, next).Length > 0)
                {
                    plugin.FixError(current, previous, next);
                }
            }
        }
    }

    /// <summary>
    /// FixErrorInList: um plugin por arquivo (carregado na primeira vez); só os que estão nas
    /// preferências e ativados corrigem, chamando FixError sem conferir HasError. Devolve quantos
    /// erros foram mandados corrigir.
    /// </summary>
    public static int FixErrors(IReadOnlyList<Subtitle> subtitles, IEnumerable<ErrorItem> errors, PluginContext context, PluginSettings settings, Action<string>? onError)
    {
        ArgumentNullException.ThrowIfNull(subtitles);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(settings);
        var plugins = new Dictionary<string, ErrorPlugin?>(StringComparer.Ordinal);
        int fixedCount = 0;
        foreach (var error in errors)
        {
            if (!plugins.TryGetValue(error.PluginFileName, out var plugin))
            {
                plugin = PluginEnumerator.Load(error.PluginFileName, context, onError);
                if (plugin is not null && settings.IsListedAndEnabled(plugin.Name))
                {
                    settings.ApplyParams(plugin);
                }
                else
                {
                    plugin = null;
                }

                plugins[error.PluginFileName] = plugin;
            }

            if (plugin is not null)
            {
                int i = SubtitleJs.IndexOf(subtitles, error.Subtitle);
                var previous = i > 0 ? subtitles[i - 1] : null;
                var next = i >= 0 && i < subtitles.Count - 1 ? subtitles[i + 1] : null;
                plugin.FixError(error.Subtitle, previous, next);
                fixedCount++;
            }
        }

        return fixedCount;
    }

    /// <summary>TMainForm.CanFixError: o plugin do arquivo está nas preferências, ativado, e tem FixError.</summary>
    public static bool CanFixError(string pluginFileName, PluginContext context, PluginSettings settings, Action<string>? onError)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return PluginEnumerator.Load(pluginFileName, context, onError) is { } plugin && settings.IsListedAndEnabled(plugin.Name) && plugin.CanFixError;
    }

    /// <summary>SubListPopupMenuPopup: algum plugin ativado nas preferências sabe corrigir (pmiFixError).</summary>
    public static bool AnyCanFixError(string pluginPath, PluginContext context, PluginSettings settings, Action<string>? onError)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return PluginEnumerator.Enumerate(pluginPath, context, onError).Any(p => settings.IsListedAndEnabled(p.Name) && p.CanFixError);
    }
}
