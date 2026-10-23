using System.Text;

namespace VisualSubSync.Core.Text;

/// <summary>
/// Gravação como nos SaveSubtitlesAs* do legado: UTF-8 com BOM (UTF8Encode) ou a página ANSI
/// do sistema (WC2MB), em que caracteres sem representação viram '?'.
/// </summary>
public static class TextFileWriter
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static byte[] Encode(string text, bool utf8)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!utf8)
        {
            return TextFileReader.Ansi.GetBytes(text);
        }

        byte[] body = Encoding.UTF8.GetBytes(text);
        byte[] result = new byte[Utf8Bom.Length + body.Length];
        Utf8Bom.CopyTo(result, 0);
        body.CopyTo(result, Utf8Bom.Length);
        return result;
    }

    public static void Write(string path, string text, bool utf8) => AtomicFile.WriteAllBytes(path, Encode(text, utf8));

    /// <summary>
    /// DetectCharsetDataLoss: verdadeiro quando o texto perderia caracteres em ANSI,
    /// caso em que o legado força UTF-8 ao salvar.
    /// </summary>
    public static bool WouldLoseDataInAnsi(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var ansi = (Encoding)TextFileReader.Ansi.Clone();
        ansi.EncoderFallback = EncoderFallback.ExceptionFallback;
        try
        {
            _ = ansi.GetByteCount(text);
            return false;
        }
        catch (EncoderFallbackException)
        {
            return true;
        }
    }
}
