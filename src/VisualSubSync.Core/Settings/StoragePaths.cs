namespace VisualSubSync.Core.Settings;

/// <summary>Keep portable storage when writable; installed applications use per-user storage.</summary>
public static class StoragePaths
{
    public static string ResolveDataFile(string portableFile, string relativeUserFile, string? userDataDirectory = null)
    {
        if (CanWriteFile(portableFile)) return portableFile;
        string fallback = Path.Combine(userDataDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualSubSync", relativeUserFile);
        Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
        return fallback;
    }
    public static bool CanWriteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var file = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);
            }

            return CanWriteDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool CanWriteDirectory(string directory)
    {
        try
        {
            string probe = Path.Combine(directory, $".vss-write-{Guid.NewGuid():N}.tmp");
            using var file = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static string ResolveBackupDirectory(string executableDirectory, string? userDataDirectory = null)
    {
        string portable = Path.Combine(executableDirectory, "Backup");
        try
        {
            Directory.CreateDirectory(portable);
            if (CanWriteDirectory(portable))
            {
                return portable;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Program Files or a read-only portable directory: use the user's profile.
        }

        string fallback = Path.Combine(userDataDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualSubSync", "Backup");
        Directory.CreateDirectory(fallback);
        return fallback;
    }
}
