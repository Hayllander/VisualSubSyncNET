using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Timing;
using VisualSubSync.Core.Web;

namespace VisualSubSync.App.ViewModels;

/// <summary>Uma sugestão recebida pela rede (TSuggestionTreeData).</summary>
public sealed class SuggestionItem(int startMs, int stopMs, string text)
{
    public int StartMs { get; } = startMs;

    public int StopMs { get; } = stopMs;

    public string Text { get; } = text;

    /// <summary>AddTime (GetTickCount).</summary>
    public long AddTime { get; } = Environment.TickCount64;

    /// <summary>Primeira linha, em negrito.</summary>
    public string Header => "From " + TimeFormat.ToVss(StartMs) + " to " + TimeFormat.ToVss(StopMs);

    /// <summary>StringConvertCRLFToPipe.</summary>
    public string Line => ConvertCrLfToPipe(Text);

    /// <summary>StringConvertCRLFToPipe: cada #13 vira '|' e o caractere seguinte é descartado.</summary>
    public static string ConvertCrLfToPipe(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                sb.Append('|');
                i++;
            }
            else
            {
                sb.Append(text[i]);
            }
        }

        return sb.ToString();
    }

    /// <summary>As sugestões dos últimos 2 s não são apagadas (ainda podem estar sendo lidas).</summary>
    public bool IsRecent(long now) => now - AddTime <= 2000;
}

/// <summary>Web server (StartStopServer) e a janela Suggestions (SuggestionFormUnit.pas).</summary>
public sealed partial class MainViewModel : IWebServerHost
{
    private HttpServer? _server;

    /// <summary>MenuItemStartWebServer.Checked; o contrário marca Stop.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWebServerStopped))]
    private bool _isWebServerRunning;

    /// <summary>MenuItemStopWebServer.Checked (marcado na abertura, como no main.dfm).</summary>
    public bool IsWebServerStopped => !IsWebServerRunning;

    public ObservableCollection<SuggestionItem> Suggestions { get; } = [];

    /// <summary>StartStopServer(False).</summary>
    [RelayCommand]
    private void StartWebServer()
    {
        if (_server is null)
        {
            var server = new HttpServer(ServerRootDir, Settings.WebServerPort, this) { EnableCompression = Settings.WebServerEnableCompression };
            if (server.Start(out string? error))
            {
                _server = server;
            }
            else
            {
                _dialogs.LogMessage(string.Create(CultureInfo.InvariantCulture, $"Web server: port {Settings.WebServerPort}: {error}"), silent: true);
            }
        }

        IsWebServerRunning = _server is not null;
    }

    /// <summary>StartStopServer(True).</summary>
    [RelayCommand]
    private void StopWebServer()
    {
        _server?.Dispose();
        _server = null;
        IsWebServerRunning = false;
    }

    /// <summary>ActionShowSuggestionsExecute: mostra ou esconde a janela.</summary>
    [RelayCommand]
    private void ShowSuggestions() => _dialogs.ToggleSuggestions(this);

    /// <summary>Lido pelas threads do servidor: copia o estado na thread da interface.</summary>
    WebSnapshot IWebServerHost.Snapshot() => dispatcher.Invoke(() =>
    {
        int averageBytes = Peaks is { } p ? (int)(p.SamplesPerSecond * p.Channels * (p.BitsPerSample / 8)) : 0;
        var page = new PageContext(ApplicationVersion, Project?.FileName ?? string.Empty, [.. _document.Items.Select(static s => s.Clone())], averageBytes);
        return new WebSnapshot(page, Project?.WavFile ?? string.Empty);
    });

    /// <summary>AddSuggestion: entra no fim da lista e avisa na barra de status.</summary>
    void IWebServerHost.AddSuggestion(int startMs, int stopMs, string text) => dispatcher.BeginInvoke(() =>
    {
        Suggestions.Add(new SuggestionItem(startMs, stopMs, text));
        ShowStatusBarMessage(string.Create(CultureInfo.InvariantCulture, $"We just received a new suggestion from the network. (Total : {Suggestions.Count})"));
    });

    /// <summary>pmiClearAllClick: apaga todas, menos as dos últimos 2 s.</summary>
    public void ClearAllSuggestions()
    {
        long now = Environment.TickCount64;
        for (int i = Suggestions.Count - 1; i >= 0; i--)
        {
            if (!Suggestions[i].IsRecent(now))
            {
                Suggestions.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// pmiClearSelectedClick: apaga as selecionadas com mais de 2 s e devolve a que deve ficar
    /// selecionada (a primeira, a partir da primeira selecionada, que fica; senão a anterior a ela).
    /// </summary>
    public SuggestionItem? ClearSelectedSuggestions(IReadOnlyCollection<SuggestionItem> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        long now = Environment.TickCount64;
        int first = Suggestions.ToList().FindIndex(selected.Contains);
        if (first < 0)
        {
            return null;
        }

        SuggestionItem? next = Suggestions.Skip(first).FirstOrDefault(s => !selected.Contains(s) || s.IsRecent(now));
        next ??= first > 0 ? Suggestions[first - 1] : null;
        foreach (var item in selected.Where(s => !s.IsRecent(now)).ToList())
        {
            Suggestions.Remove(item);
        }

        return next;
    }

    /// <summary>SelectNodeFromTimestamps: seleciona a legenda com exatamente esses tempos.</summary>
    public void SelectSubtitleFromTimestamps(int startMs, int stopMs)
    {
        int index = _document.Items.FindIndex(s => s.StartMs == startMs && s.StopMs == stopMs);
        if (index >= 0)
        {
            SelectNodeAtIndex(index);
        }
    }
}
