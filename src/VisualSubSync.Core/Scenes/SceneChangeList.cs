namespace VisualSubSync.Core.Scenes;

/// <summary>
/// As operações do TSceneChangeWrapper (JavaScriptPluginUnit.pas) sobre a lista ordenada de mudanças
/// de cena: BinarySearch (MiscToolsUnit), Contains, Delete e Insert.
/// </summary>
public static class SceneChangeList
{
    /// <summary>BinarySearch: o índice do valor; sem ele, o primeiro maior (ou, para trás, o último menor).</summary>
    public static int BinarySearch(IReadOnlyList<int> values, int value, bool backward = false)
    {
        ArgumentNullException.ThrowIfNull(values);
        int min = 0, max = values.Count - 1;
        while (min <= max)
        {
            int mid = (min + max) / 2;
            if (values[mid] < value)
            {
                min = mid + 1;
            }
            else if (values[mid] > value)
            {
                max = mid - 1;
            }
            else
            {
                return mid;
            }
        }

        return backward ? max : min;
    }

    /// <summary>Contains: há mudança de cena em [start, stop].</summary>
    public static bool Contains(IReadOnlyList<int> values, int start, int stop)
    {
        int i = BinarySearch(values, start);
        return i < values.Count && values[i] <= stop;
    }

    /// <summary>Delete: tira as mudanças de cena em [start, stop] e devolve as tiradas.</summary>
    public static int[] Delete(List<int> values, int startMs, int stopMs)
    {
        ArgumentNullException.ThrowIfNull(values);
        int startIdx = BinarySearch(values, startMs);
        int stopIdx = BinarySearch(values, stopMs, backward: true);
        int count = stopIdx - startIdx + 1;
        if (stopIdx == -1 || count <= 0)
        {
            return [];
        }

        int[] deleted = [.. values.GetRange(startIdx, count)];
        values.RemoveRange(startIdx, count);
        return deleted;
    }

    /// <summary>Insert: junta as mudanças de cena em ordem; um tempo que já existe não se repete.</summary>
    public static void Insert(List<int> values, IEnumerable<int> source)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(source);
        foreach (int time in source)
        {
            int i = BinarySearch(values, time);
            if (i >= values.Count || values[i] != time)
            {
                values.Insert(i, time);
            }
        }
    }
}
