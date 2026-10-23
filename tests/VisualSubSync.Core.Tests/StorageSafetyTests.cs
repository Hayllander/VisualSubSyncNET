using VisualSubSync.Core.Settings;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Tests;

public sealed class StorageSafetyTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("vss-storage-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Writable_portable_settings_stay_beside_executable_without_changing_content()
    {
        string exe = Path.Combine(_folder, "VisualSubSync.exe");
        string ini = Path.ChangeExtension(exe, ".ini");
        File.WriteAllText(ini, "[Other]\r\nFuture=keep\r\n");
        Assert.Equal(ini, VssSettings.ResolvePath(exe));
        Assert.Equal("[Other]\r\nFuture=keep\r\n", File.ReadAllText(ini));
        Assert.Single(Directory.GetFiles(_folder));
    }

    [Fact]
    public void Locked_ini_falls_back_even_without_readonly_attribute()
    {
        string exe = Path.Combine(_folder, "VisualSubSync.exe");
        string ini = Path.ChangeExtension(exe, ".ini");
        File.WriteAllText(ini, "keep");
        using var file = new FileStream(ini, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(Path.Combine(_folder, "user", "VisualSubSync.ini"), VssSettings.ResolvePath(exe, Path.Combine(_folder, "user")));
    }

    [Fact]
    public void Protected_local_ini_is_imported_before_first_per_user_save()
    {
        string exe = Path.Combine(_folder, "VisualSubSync.exe");
        string ini = Path.ChangeExtension(exe, ".ini");
        string user = Directory.CreateDirectory(Path.Combine(_folder, "user")).FullName;
        File.WriteAllText(ini, "[Unknown]\r\nFuture=keep\r\n[General]\r\nVolume=42\r\n");
        File.SetAttributes(ini, FileAttributes.ReadOnly);
        try
        {
            var settings = VssSettings.LoadForExecutable(exe, user);
            Assert.Equal(Path.Combine(user, "VisualSubSync.ini"), settings.FileName);
            Assert.Equal(42, settings.Volume);
            settings.Save();
            Assert.Contains("Future=keep", File.ReadAllText(settings.FileName), StringComparison.Ordinal);
            Assert.Contains("Volume=42", File.ReadAllText(ini), StringComparison.Ordinal);
        }
        finally
        {
            File.SetAttributes(ini, FileAttributes.Normal);
        }
    }

    [Fact]
    public void Backup_uses_user_storage_when_portable_backup_path_is_unavailable()
    {
        File.WriteAllText(Path.Combine(_folder, "Backup"), "block directory creation");
        string user = Path.Combine(_folder, "user");
        string backup = StoragePaths.ResolveBackupDirectory(_folder, user);
        Assert.Equal(Path.Combine(user, "VisualSubSync", "Backup"), backup);
        Assert.True(StoragePaths.CanWriteDirectory(backup));
    }

    [Fact]
    public void Atomic_write_preserves_previous_file_when_replacement_is_locked()
    {
        string path = Path.Combine(_folder, "subtitle.srt");
        File.WriteAllText(path, "original");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() => AtomicFile.WriteAllBytes(path, "changed"u8.ToArray()));
        }

        Assert.Equal("original", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(_folder, ".vss-*.tmp"));
        AtomicFile.WriteAllBytes(path, "changed"u8.ToArray());
        Assert.Equal("changed", File.ReadAllText(path));
    }
}
