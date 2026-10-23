using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace VisualSubSync.App.Services;

/// <summary>
/// ShellRegisterExtension, ShellUnRegisterExtension, ShellIsExtensionRegistered e
/// IsRegistryClassesRootKeyWritable (MiscToolsUnit.pas), no HKEY_CLASSES_ROOT como no original.
/// </summary>
internal static partial class ShellAssociation
{
    private const string AppName = "VisualSubSync";

    /// <summary>RegOpenKeyEx(HKEY_CLASSES_ROOT, nil, KEY_WRITE).</summary>
    public static bool IsClassesRootWritable()
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(string.Empty, writable: true);
            return key is not null;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool IsRegistered(string extension, string docType, string executable)
    {
        try
        {
            using var ext = Registry.ClassesRoot.OpenSubKey("." + extension);
            if (ext?.GetValue(string.Empty) as string != AppName + "." + docType)
            {
                return false;
            }

            using var command = Registry.ClassesRoot.OpenSubKey(AppName + "." + docType + @"\shell\open\command");
            return command?.GetValue(string.Empty) as string == executable + " \"%1\"";
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return false;
        }
    }

    /// <summary>Guarda o tipo anterior em "VisualSubSync.sav", aponta a extensão para o programa e define o ícone.</summary>
    public static void Register(string extension, string docType, string executable, int iconIndex)
    {
        try
        {
            string progId = AppName + "." + docType;
            using (var ext = Registry.ClassesRoot.CreateSubKey("." + extension))
            {
                if (ext.GetValue(string.Empty) is string previous && previous.Length > 0 && previous != progId)
                {
                    ext.SetValue(AppName + ".sav", previous);
                }

                ext.SetValue(string.Empty, progId);
            }

            using (var command = Registry.ClassesRoot.CreateSubKey(progId + @"\shell\open\command"))
            {
                command.SetValue(string.Empty, executable + " \"%1\"");
            }

            if (iconIndex != -1)
            {
                using var icon = Registry.ClassesRoot.CreateSubKey(progId + @"\DefaultIcon");
                icon.SetValue(string.Empty, executable + "," + iconIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            System.Diagnostics.Trace.TraceWarning($"Register .{extension}: {ex.Message}");
        }

        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED, SHCNF_IDLIST
    }

    /// <summary>Restaura o tipo guardado (ou apaga a extensão) e apaga a chave do programa.</summary>
    public static void Unregister(string extension, string docType)
    {
        try
        {
            using (var ext = Registry.ClassesRoot.OpenSubKey("." + extension, writable: true))
            {
                if (ext is not null)
                {
                    if (ext.GetValue(AppName + ".sav") is string saved && saved.Length > 0)
                    {
                        ext.SetValue(string.Empty, saved);
                        ext.DeleteValue(AppName + ".sav", throwOnMissingValue: false);
                    }
                    else
                    {
                        ext.Close();
                        Registry.ClassesRoot.DeleteSubKeyTree("." + extension, throwOnMissingSubKey: false);
                    }
                }
            }

            Registry.ClassesRoot.DeleteSubKeyTree(AppName + "." + docType, throwOnMissingSubKey: false);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or System.IO.IOException or ArgumentException)
        {
            System.Diagnostics.Trace.TraceWarning($"Unregister .{extension}: {ex.Message}");
        }
    }

    [LibraryImport("shell32.dll")]
    private static partial void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
