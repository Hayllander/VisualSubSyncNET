using System.Text;
using VisualSubSync.Core.Subtitles.Formats;
using VisualSubSync.Core.Text;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.Core.Subtitles;

/// <summary>
/// VSSClipboardUnit.pas: o formato "CF_VisualSubSync" (compatível com o programa original) e o texto
/// SRT que vai junto; para colar, o formato próprio e, na falta dele, texto SRT ou SSA.
/// </summary>
public static class SubtitleClipboard
{
    /// <summary>RegisterClipboardFormat('CF_VisualSubSync').</summary>
    public const string FormatName = "CF_VisualSubSync";

    /// <summary>CopyPasteStreamVersion.</summary>
    private const int StreamVersion = 1;

    /// <summary>
    /// CopyVTVToClipboard (parte binária): versão, quantidade e, para cada legenda, início, fim e os
    /// textos (Integer + UTF-16) na ordem de TSubtitleRange.SaveToStream.
    /// </summary>
    public static byte[] Serialize(IReadOnlyList<Subtitle> subtitles)
    {
        ArgumentNullException.ThrowIfNull(subtitles);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true))
        {
            writer.Write(StreamVersion);
            writer.Write(subtitles.Count);
            foreach (var sub in subtitles)
            {
                writer.Write(sub.StartMs);
                writer.Write(sub.StopMs);
                foreach (string value in Fields(sub))
                {
                    writer.Write(value.Length);
                    writer.Write(Encoding.Unicode.GetBytes(value));
                }
            }
        }

        return stream.ToArray();
    }

    /// <summary>PasteClipboardVSS: lista vazia quando a versão não confere ou os dados estão cortados.</summary>
    public static List<Subtitle> Deserialize(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var result = new List<Subtitle>();
        try
        {
            using var reader = new BinaryReader(new MemoryStream(data), Encoding.Unicode);
            if (reader.ReadInt32() != StreamVersion)
            {
                return result;
            }

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                result.Add(new Subtitle
                {
                    StartMs = reader.ReadInt32(),
                    StopMs = reader.ReadInt32(),
                    Text = ReadString(reader),
                    Effect = ReadString(reader),
                    Layer = ReadString(reader),
                    Marked = ReadString(reader),
                    Actor = ReadString(reader),
                    MarginR = ReadString(reader),
                    MarginL = ReadString(reader),
                    MarginV = ReadString(reader),
                    Style = ReadString(reader),
                });
            }
        }
        catch (EndOfStreamException)
        {
            // GlobalSize pode arredondar o bloco; o que já foi lido vale.
        }

        return result;
    }

    /// <summary>
    /// CopyVTVToClipboard (parte texto): Sub2SrtString de cada uma (número na lista, tempos com vírgula,
    /// texto), separadas por uma linha em branco.
    /// </summary>
    public static string ToText(IEnumerable<(int Number, Subtitle Subtitle)> subtitles)
    {
        ArgumentNullException.ThrowIfNull(subtitles);
        var text = new StringBuilder();
        foreach (var (number, sub) in subtitles)
        {
            text.Append(number.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n")
                .Append(TimeFormat.ToSrt(sub.StartMs)).Append(" --> ").Append(TimeFormat.ToSrt(sub.StopMs)).Append("\r\n")
                .Append(sub.Text).Append("\r\n\r\n");
        }

        return DelphiText.Trim(text.ToString());
    }

    /// <summary>PasteClipboardText: tenta SRT e, sem nada, SSA.</summary>
    public static List<Subtitle> ParseText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var srt = SrtFormat.Parse(text, sort: false);
        if (srt.Items.Count > 0)
        {
            return [.. srt.Items];
        }

        var ssa = SsaFormat.Parse(text, clipboard: true).Document.Items;
        foreach (var sub in ssa)
        {
            // TryPasteAsSSA só troca Marked e Layer quando o campo existe.
            if (sub.Marked.Length == 0)
            {
                sub.Marked = "Marked=0";
            }

            if (sub.Layer.Length == 0)
            {
                sub.Layer = "0";
            }

            Karaoke.UpdateSubTimesFromText(sub);
        }

        return [.. ssa];
    }

    /// <summary>ActionPasteAtCursorExecute: desloca todas para que a primeira comece no cursor.</summary>
    public static void MoveToCursor(IReadOnlyList<Subtitle> subtitles, int cursorMs)
    {
        ArgumentNullException.ThrowIfNull(subtitles);
        if (subtitles.Count == 0)
        {
            return;
        }

        int delay = subtitles[0].StartMs - cursorMs;
        foreach (var sub in subtitles)
        {
            sub.StartMs -= delay;
            sub.StopMs -= delay;
        }
    }

    private static string[] Fields(Subtitle s) =>
        [s.Text, s.Effect, s.Layer, s.Marked, s.Actor, s.MarginR, s.MarginL, s.MarginV, s.Style];

    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 0 || length > 1 << 24)
        {
            throw new EndOfStreamException();
        }

        byte[] bytes = reader.ReadBytes(length * 2);
        if (bytes.Length < length * 2)
        {
            throw new EndOfStreamException();
        }

        return Encoding.Unicode.GetString(bytes);
    }
}
