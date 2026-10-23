using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;

namespace VisualSubSync.App.ViewModels;

/// <summary>
/// MenuItemCheckUpdatesClick e TimerChkUpdatesTimer (main.pas do Enhanced): consulta as releases do
/// repositório no GitHub, compara a versão com a do programa e oferece o download da mais nova.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>
    /// O repositório que publica os instaladores (no Enhanced, red5goahead/VisualSubSync-Enhanced).
    /// </summary>
    private const string ReleasesApiUrl = "https://api.github.com/repos/Hayllander/VisualSubSyncNET/releases";

    /// <summary>TimerChkUpdates.Interval (main.dfm): meio segundo depois de a janela abrir.</summary>
    private const int UpdateCheckDelayMs = 500;

    private static readonly HttpClient UpdateClient = CreateUpdateClient();

    /// <summary>MenuItemCheckUpdatesClick.</summary>
    [RelayCommand]
    private Task CheckUpdates() => CheckUpdatesAsync(silent: false);

    /// <summary>TimerChkUpdatesTimer: a verificação do início, que só avisa quando há versão nova.</summary>
    public async Task CheckUpdatesOnStartup()
    {
        if (!Settings.CheckUpdates)
        {
            return;
        }

        await Task.Delay(UpdateCheckDelayMs);
        await CheckUpdatesAsync(silent: true);
    }

    /// <summary>
    /// CheckUpdates do Enhanced: vale a primeira release com um instalador anexado; se a versão dela for
    /// maior que a do programa, pergunta se deve baixar e abre o link. Sem internet, nada é mostrado.
    /// </summary>
    private async Task CheckUpdatesAsync(bool silent)
    {
        ReleaseInfo? release;
        try
        {
            release = await FetchLatestReleaseAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            Trace.TraceWarning($"Update check failed: {ex.Message}");
            return;
        }

        if (release is not null && string.Compare(FormatVersionString(release.Version), FormatVersionString(ApplicationVersion), StringComparison.Ordinal) > 0)
        {
            string message =
                $"A new version of {ApplicationName} ({release.Version}) is available on the Github repository.{Environment.NewLine}" +
                $"Do you want to download it?{Environment.NewLine}{Environment.NewLine}" +
                $"Release notes ({release.PublishedAt}){Environment.NewLine}{Environment.NewLine}{release.Notes}{Environment.NewLine}";
            if (_dialogs.ConfirmInformation(ApplicationName, message))
            {
                OpenUrl(release.DownloadUrl);
            }
        }
        else if (!silent)
        {
            _dialogs.ShowInformation("No new updates are available.");
        }
    }

    /// <summary>A primeira release da lista (a mais nova) que tenha pelo menos um arquivo anexado.</summary>
    private static async Task<ReleaseInfo?> FetchLatestReleaseAsync()
    {
        using var response = await UpdateClient.GetAsync(new Uri(ReleasesApiUrl));
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement release in document.RootElement.EnumerateArray())
        {
            string? download = FirstAssetUrl(release);
            if (download is null)
            {
                continue;
            }

            return new ReleaseInfo(
                Text(release, "tag_name"),
                Text(release, "body"),
                FormatPublishedAt(Text(release, "published_at")),
                download);
        }

        return null;
    }

    /// <summary>O browser_download_url do primeiro arquivo anexado (o break do Enhanced no primeiro).</summary>
    private static string? FirstAssetUrl(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            if (Text(asset, "browser_download_url") is { Length: > 0 } url)
            {
                return url;
            }
        }

        return null;
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>published_at (aaaa-mm-ddThh:mm:ssZ) vira "dd-mm-aaaa hh:mm", como os Copy do Enhanced.</summary>
    private static string FormatPublishedAt(string publishedAt) =>
        DateTimeOffset.TryParse(publishedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset value)
            ? value.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)
            : publishedAt;

    /// <summary>
    /// FormatVersionString do Enhanced: cada componente numérica com dois dígitos, para comparar como
    /// texto. Diferença deliberada: as tags daqui têm o prefixo "v" e o sufixo de pré-lançamento
    /// ("2.0.0-alpha.3"), que o original não tinha; o sufixo entra no fim da chave e a versão final ganha
    /// "~", que ordena depois de qualquer sufixo, para o pré-lançamento não passar por versão nova.
    /// </summary>
    private static string FormatVersionString(string version)
    {
        string text = version.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        int dash = text.IndexOf('-', StringComparison.Ordinal);
        string core = dash < 0 ? text : text[..dash];
        string suffix = dash < 0 ? "~" : text[(dash + 1)..];

        var builder = new StringBuilder();
        foreach (string part in core.Split('.'))
        {
            builder.Append(part.PadRight(2, '0')).Append('.');
        }

        return builder.Append(suffix).ToString();
    }

    private static HttpClient CreateUpdateClient()
    {
        // A API do GitHub recusa requisições sem User-Agent.
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15), };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(ApplicationName);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>ShellExecute(Handle, 'open', ...): sem mensagem quando o link não abre, como no original.</summary>
    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Trace.TraceWarning($"Could not open {url}: {ex.Message}");
        }
    }

    /// <summary>Os campos da release que a mensagem usa.</summary>
    private sealed record ReleaseInfo(string Version, string Notes, string PublishedAt, string DownloadUrl);
}
