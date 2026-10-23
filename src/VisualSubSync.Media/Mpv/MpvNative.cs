using System.Reflection;
using System.Runtime.InteropServices;

namespace VisualSubSync.Media.Mpv;

/// <summary>Subconjunto da API cliente do libmpv (client.h, API 2.x).</summary>
internal static partial class MpvNative
{
    private const string Lib = "mpv";

    static MpvNative() => NativeLibrary.SetDllImportResolver(typeof(MpvNative).Assembly, Resolve);

    public enum Format
    {
        None = 0,
        String = 1,
        Flag = 3,
        Int64 = 4,
        Double = 5,
    }

    public enum EventId
    {
        None = 0,
        Shutdown = 1,
        LogMessage = 2,
        CommandReply = 5,
        StartFile = 6,
        EndFile = 7,
        FileLoaded = 8,
        Seek = 20,
        PlaybackRestart = 21,
        PropertyChange = 22,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Event
    {
        public EventId EventIdValue;
        public int Error;
        public ulong ReplyUserData;
        public nint Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EventProperty
    {
        public nint Name;
        public Format Format;
        public nint Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EventEndFile
    {
        public int Reason;
        public int Error;
    }

    [LibraryImport(Lib, EntryPoint = "mpv_create")]
    public static partial nint Create();

    [LibraryImport(Lib, EntryPoint = "mpv_initialize")]
    public static partial int Initialize(nint ctx);

    [LibraryImport(Lib, EntryPoint = "mpv_terminate_destroy")]
    public static partial void TerminateDestroy(nint ctx);

    [LibraryImport(Lib, EntryPoint = "mpv_set_option_string", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int SetOptionString(nint ctx, string name, string value);

    [LibraryImport(Lib, EntryPoint = "mpv_set_property_string", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int SetPropertyString(nint ctx, string name, string value);

    [LibraryImport(Lib, EntryPoint = "mpv_set_property", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int SetProperty(nint ctx, string name, Format format, ref double data);

    [LibraryImport(Lib, EntryPoint = "mpv_get_property", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int GetProperty(nint ctx, string name, Format format, out double data);

    [LibraryImport(Lib, EntryPoint = "mpv_get_property", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int GetInt64Property(nint ctx, string name, Format format, out long data);

    [LibraryImport(Lib, EntryPoint = "mpv_observe_property", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int ObserveProperty(nint ctx, ulong replyUserData, string name, Format format);

    [LibraryImport(Lib, EntryPoint = "mpv_command")]
    private static partial int CommandRaw(nint ctx, nint args);

    [LibraryImport(Lib, EntryPoint = "mpv_wait_event")]
    public static partial nint WaitEvent(nint ctx, double timeout);

    [LibraryImport(Lib, EntryPoint = "mpv_wakeup")]
    public static partial void Wakeup(nint ctx);

    [LibraryImport(Lib, EntryPoint = "mpv_error_string")]
    private static partial nint ErrorStringRaw(int error);

    public static string ErrorString(int error) => Marshal.PtrToStringUTF8(ErrorStringRaw(error)) ?? error.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>mpv_command com argumentos UTF-8 terminados por NULL.</summary>
    public static int Command(nint ctx, params string[] args)
    {
        var pointers = new nint[args.Length + 1];
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                pointers[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
            }

            unsafe
            {
                fixed (nint* p = pointers)
                {
                    return CommandRaw(ctx, (nint)p);
                }
            }
        }
        finally
        {
            foreach (nint p in pointers)
            {
                if (p != 0)
                {
                    Marshal.FreeCoTaskMem(p);
                }
            }
        }
    }

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? path)
    {
        if (name != Lib)
        {
            return 0;
        }

        string[] candidates = OperatingSystem.IsWindows()
            ? ["libmpv-2.dll", "mpv-2.dll", Path.Combine(AppContext.BaseDirectory, "libmpv-2.dll")]
            : ["libmpv.so.2", "libmpv.so"];
        foreach (string candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, assembly, path, out nint handle))
            {
                return handle;
            }
        }

        throw new DllNotFoundException(
            "libmpv não encontrada. No Windows, rode poc\\obter-libmpv.ps1 para baixar libmpv-2.dll.");
    }
}
