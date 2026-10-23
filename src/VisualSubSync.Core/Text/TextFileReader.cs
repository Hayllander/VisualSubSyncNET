using System.Globalization;
using System.Text;

namespace VisualSubSync.Core.Text;

/// <summary>
/// Leitura de texto com a mesma prioridade do legado: BOM (UTF-8/UTF-16) e, sem BOM, a página de
/// código ANSI do sistema (GetACP), usada pelo Delphi 7. Diferença deliberada: um arquivo sem BOM
/// que contenha UTF-8 válido com caracteres não ASCII é lido como UTF-8 (o legado mostraria lixo).
/// Arquivos só com ASCII continuam sendo tratados como ANSI, para regravar idênticos.
/// </summary>
public static partial class TextFileReader
{
    static TextFileReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static (string Text, Encoding Encoding) ReadAllText(string path) => ReadAllText(path, detectUtf8: true);

    /// <summary>
    /// <paramref name="detectUtf8"/> falso: sem BOM o arquivo é sempre ANSI, como o GetPrivateProfileString
    /// que o TIniFile usa (o VisualSubSync.ini guarda UTF-8 dentro de ANSI nas entradas Recent).
    /// </summary>
    public static (string Text, Encoding Encoding) ReadAllText(string path, bool detectUtf8)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var encoding = Detect(bytes, out int preamble, detectUtf8);
        return (encoding.GetString(bytes, preamble, bytes.Length - preamble), encoding);
    }

    public static Encoding Detect(ReadOnlySpan<byte> bytes, out int preambleLength) => Detect(bytes, out preambleLength, detectUtf8: true);

    private static Encoding Detect(ReadOnlySpan<byte> bytes, out int preambleLength, bool detectUtf8)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            preambleLength = 3;
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
        {
            preambleLength = 2;
            return new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            preambleLength = 2;
            return new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
        }

        preambleLength = 0;
        return detectUtf8 && !System.Text.Ascii.IsValid(bytes) && IsValidUtf8(bytes)
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            : Ansi;
    }

    /// <summary>Equivalente a LastFileCharSet &lt;&gt; csAnsi: o legado chama isso de "IsUTF8".</summary>
    public static bool IsUnicode(Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        return encoding is UTF8Encoding or UnicodeEncoding or UTF32Encoding;
    }

    /// <summary>
    /// Página de código ANSI do sistema (GetACP, "linguagem para programas não Unicode"), a mesma do
    /// Delphi 7; fora do Windows, a da cultura atual.
    /// </summary>
    public static Encoding Ansi => Encoding.GetEncoding(OperatingSystem.IsWindows()
        ? (int)NativeMethods.GetACP()
        : CultureInfo.CurrentCulture.TextInfo.ANSICodePage);

    private static bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            _ = new UTF8Encoding(false, throwOnInvalidBytes: true).GetCharCount(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static partial class NativeMethods
    {
        [System.Runtime.InteropServices.LibraryImport("kernel32.dll")]
        internal static partial uint GetACP();
    }
}
