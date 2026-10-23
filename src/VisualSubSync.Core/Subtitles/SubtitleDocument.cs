namespace VisualSubSync.Core.Subtitles;

/// <summary>
/// Conteúdo de um arquivo de legendas: as legendas e o que o legado guarda em volta delas
/// (SubtitleFileHeader/Footer, a lista de estilos e CurrentProject.IsUTF8).
/// </summary>
public sealed class SubtitleDocument
{
    public List<Subtitle> Items { get; } = [];

    /// <summary>Estilos SSA/ASS. Vazio em SRT; ao salvar em SSA/ASS sem estilos, usa-se o "Default".</summary>
    public List<SsaStyle> Styles { get; } = [];

    /// <summary>Linhas chave:valor de [Script Info], separadas por CRLF (vazio = cabeçalho padrão).</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>Linhas fora das seções conhecidas ([Fonts], [Graphics], ...), preservadas ao salvar.</summary>
    public string Footer { get; set; } = string.Empty;

    /// <summary>Arquivo em Unicode (com BOM). Falso = ANSI. Decide a codificação ao salvar.</summary>
    public bool IsUtf8 { get; set; }

    /// <summary>
    /// O SRT foi corrigido na leitura (fim igual ao início da seguinte); o legado marca o projeto
    /// como modificado nesse caso.
    /// </summary>
    public bool AutoCorrected { get; set; }

    /// <summary>Ordem do RangeList.FullSort: início, depois fim. Estável para empates.</summary>
    public void Sort()
    {
        var sorted = Items.OrderBy(static s => s.StartMs).ThenBy(static s => s.StopMs).ToList();
        Items.Clear();
        Items.AddRange(sorted);
    }

    /// <summary>Garante ao menos um estilo, como TStyleForm.AddDefaultStyle.</summary>
    public void EnsureDefaultStyle()
    {
        if (Styles.Count == 0)
        {
            Styles.Add(new SsaStyle { Name = "Default" });
        }
    }
}
