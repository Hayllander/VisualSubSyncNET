using FFmpeg.AutoGen;

namespace VisualSubSync.Media.FFmpeg;

/// <summary>Localiza as bibliotecas nativas do FFmpeg uma única vez.</summary>
internal static class FFmpegLoader
{
    private static readonly Lazy<bool> Loaded = new(() =>
    {
        string[] candidates = OperatingSystem.IsWindows()
            ? [AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native")]
            : ["/usr/lib/x86_64-linux-gnu", "/usr/lib"];
        string probe = OperatingSystem.IsWindows() ? "avcodec-61.dll" : "libavcodec.so.60";
        ffmpeg.RootPath = candidates.FirstOrDefault(d => File.Exists(Path.Combine(d, probe))) ?? candidates[0];
        ffmpeg.av_log_set_level(ffmpeg.AV_LOG_ERROR);
        return true;
    });

    public static void EnsureLoaded() => _ = Loaded.Value;

    public static unsafe string ErrorText(int error)
    {
        const int size = 256;
        byte* buffer = stackalloc byte[size];
        ffmpeg.av_strerror(error, buffer, size);
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi((nint)buffer) ?? error.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static int Check(int result, string what)
    {
        if (result < 0)
        {
            throw new IOException($"FFmpeg: {what} falhou ({ErrorText(result)}).");
        }

        return result;
    }
}
