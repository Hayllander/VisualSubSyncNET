namespace VisualSubSync.Core.Text;

/// <summary>
/// Caminhos relativos com a semântica do legado (WideExtractRelativePath / WideResolveRelativePath),
/// sempre em estilo Windows, independentemente do sistema onde os testes rodam.
/// </summary>
public static class WindowsPath
{
    /// <summary>Como WideIsAbsolutePath: "C:..." ou "\\servidor...".</summary>
    public static bool IsAbsolute(string path) =>
        (path.Length >= 2 && path[1] == ':') || path.StartsWith(@"\\", StringComparison.Ordinal);

    /// <summary>Caminho de <paramref name="target"/> relativo à pasta do arquivo <paramref name="baseFile"/>.
    /// Em unidades diferentes devolve o caminho absoluto, como ExtractRelativePath.</summary>
    public static string MakeRelative(string baseFile, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return string.Empty;
        }

        var baseParts = Split(baseFile)[..^1];
        var targetParts = Split(target);
        if (baseParts.Length == 0 || targetParts.Length == 0 ||
            !string.Equals(baseParts[0], targetParts[0], StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        int common = 0;
        while (common < baseParts.Length && common < targetParts.Length - 1 &&
               string.Equals(baseParts[common], targetParts[common], StringComparison.OrdinalIgnoreCase))
        {
            common++;
        }

        var parts = Enumerable.Repeat("..", baseParts.Length - common).Concat(targetParts[common..]);
        return string.Join('\\', parts);
    }

    /// <summary>Resolve <paramref name="target"/> a partir da pasta de <paramref name="baseFile"/>, tratando ".." e ".".</summary>
    public static string Resolve(string baseFile, string target)
    {
        if (IsAbsolute(target))
        {
            return target;
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            return string.Empty;
        }

        var stack = new List<string>(Split(baseFile)[..^1]);
        foreach (string part in Split(target))
        {
            if (part == "..")
            {
                if (stack.Count > 1)
                {
                    stack.RemoveAt(stack.Count - 1);
                }
            }
            else if (part != ".")
            {
                stack.Add(part);
            }
        }

        string joined = string.Join('\\', stack);
        return baseFile.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\" + joined : joined;
    }

    private static string[] Split(string path) =>
        path.Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
}
