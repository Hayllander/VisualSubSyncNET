namespace VisualSubSync.Core.Text;

/// <summary>Finish writing beside the target before replacing the existing user file.</summary>
public static class AtomicFile
{
    public static void WriteAllBytes(string path, byte[] bytes)
    {
        string target = Path.GetFullPath(path);
        string temporary = Path.Combine(Path.GetDirectoryName(target)!, $".vss-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }

            if (File.Exists(target))
            {
                File.Replace(temporary, target, null);
            }
            else
            {
                File.Move(temporary, target);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
