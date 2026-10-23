using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Web;

namespace VisualSubSync.Core.Tests;

public sealed class HttpServerTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("vss-web-").FullName;
    private readonly string _root;
    private readonly FakeHost _host = new();
    private readonly HttpServer _server;

    public HttpServerTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(_folder, "web")).FullName;
        File.WriteAllText(Path.Combine(_root, "index.shtml"), "$|project-filename|$:<!--$TMPL_SUBTITLES_LIST$-->$|index|$=$|raw-text8|$;<!--$TMPL_SUBTITLES_LIST_END$-->");
        File.WriteAllText(Path.Combine(_root, "details.shtml"), "<!--$TMPL_SELECTED_SUB$-->$|time-index|$<!--$TMPL_SELECTED_SUB_END$-->");
        File.WriteAllText(Path.Combine(_root, "send_suggestion.shtml"), "$|post-result|$ $|POST[index]|$");
        File.WriteAllText(Path.Combine(_root, "style.css"), "body {}");
        File.WriteAllText(Path.Combine(_folder, "secret.txt"), "secret");
        _host.Wav = Path.Combine(_folder, "a.wav");
        WriteWav(_host.Wav);
        _server = new HttpServer(_root, 0, _host);
        Assert.True(_server.Start(out _));
    }

    public void Dispose()
    {
        _server.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private sealed class FakeHost : IWebServerHost
    {
        public string Wav { get; set; } = string.Empty;

        public List<(int, int, string)> Suggestions { get; } = [];

        public WebSnapshot Snapshot() => new(
            new PageContext("9.9", "proj.vssprj", [new Subtitle { StartMs = 1000, StopMs = 2000, Text = "Olá" }, new Subtitle { StartMs = 3000, StopMs = 4000, Text = "B" }], 2000),
            Wav);

        public void AddSuggestion(int startMs, int stopMs, string text)
        {
            lock (Suggestions)
            {
                Suggestions.Add((startMs, stopMs, text));
            }
        }
    }

    private static void WriteWav(string path)
    {
        using var w = new BinaryWriter(File.Create(path));
        w.Write("RIFF"u8);
        w.Write(36 + 2000);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(1000);
        w.Write(2000);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(2000);
        w.Write(new byte[2000]);
    }

    private async Task<(string[] Header, byte[] Body)> Send(string request)
    {
        using var client = new TcpClient();
        var ct = TestContext.Current.CancellationToken;
        await client.ConnectAsync("127.0.0.1", _server.Port, ct);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.Latin1.GetBytes(request), ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        byte[] all = ms.ToArray();
        int end = all.AsSpan().IndexOf("\r\n\r\n"u8);
        string[] header = Encoding.Latin1.GetString(all, 0, end).Split("\r\n");
        return (header, all[(end + 4)..]);
    }

    [Fact]
    public async Task Get_root_processes_the_index_page()
    {
        var (header, body) = await Send("GET / HTTP/1.0\r\nHost: x\r\n\r\n");
        Assert.Equal("HTTP/1.0 200 OK", header[0]);
        Assert.StartsWith("Date: ", header[1], StringComparison.Ordinal);
        Assert.EndsWith(" GMT", header[1], StringComparison.Ordinal);
        Assert.Equal("Server: VisualSubSync/9.9", header[2]);
        Assert.Equal("Content-Length: " + body.Length, header[3]);
        Assert.Equal("Content-Type: text/html", header[4]);
        Assert.Equal("proj.vssprj:1=Olá;2=B;", Encoding.UTF8.GetString(body));
    }

    [Fact]
    public async Task Get_with_parameters_selects_the_subtitle()
    {
        var (_, body) = await Send("GET /details.shtml?idx=2 HTTP/1.1\r\n\r\n");
        Assert.Equal("BB8-FA0", Encoding.ASCII.GetString(body));
    }

    [Fact]
    public async Task Static_missing_and_forbidden_files()
    {
        var (header, body) = await Send("GET /style.css HTTP/1.0\r\n\r\n");
        Assert.Contains("Content-Type: text/css", header);
        Assert.Equal("body {}", Encoding.ASCII.GetString(body));

        (header, body) = await Send("GET /missing.txt HTTP/1.0\r\n\r\n");
        Assert.Equal("HTTP/1.0 404 Not Found", header[0]);
        Assert.Contains("<h2>404 ERROR : Not Found : ", Encoding.ASCII.GetString(body), StringComparison.Ordinal);

        (header, _) = await Send("GET /../secret.txt HTTP/1.0\r\n\r\n");
        Assert.Equal("HTTP/1.0 403 Forbidden", header[0]);

        (header, _) = await Send("PUT / HTTP/1.0\r\n\r\n");
        Assert.Equal("HTTP/1.0 501 Not Implemented", header[0]);
    }

    [Fact]
    public async Task Post_adds_a_suggestion_and_answers_with_the_page()
    {
        string content = "vss-command=SUGGESTION&time-index=3E8-7D0&index=1&text=Ol%C3%A1+mundo%0D%0Ade+novo";
        var (header, body) = await Send($"POST /send_suggestion.shtml HTTP/1.0\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: {content.Length}\r\n\r\n{content}");
        Assert.Equal("HTTP/1.0 200 OK", header[0]);
        Assert.Equal("Suggestion sent successfully. 1", Encoding.ASCII.GetString(body));
        Assert.Equal((1000, 2000, "Olá mundo\r\nde novo"), Assert.Single(_host.Suggestions));
    }

    [Fact]
    public async Task Virtual_wav_extracts_the_range()
    {
        var (header, body) = await Send("GET /wav/0-64.wav HTTP/1.0\r\n\r\n");
        Assert.Contains("Content-Type: audio/x-wav", header);
        Assert.Equal(46 + 200, body.Length);
    }

    [Fact]
    public async Task Pages_are_deflated_when_enabled_and_accepted()
    {
        _server.EnableCompression = true;
        var (header, body) = await Send("GET /index.shtml HTTP/1.0\r\nAccept-Encoding: gzip, deflate\r\n\r\n");
        Assert.Equal("Content-encoding: deflate", header[4]);
        Assert.Equal("Vary: Accept-Encoding", header[5]);
        using var z = new ZLibStream(new MemoryStream(body), CompressionMode.Decompress);
        using var reader = new StreamReader(z, Encoding.UTF8);
        Assert.Equal("proj.vssprj:1=Olá;2=B;", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Helpers_follow_the_legacy()
    {
        Assert.Equal("a bÃ©", HttpServer.UrlDecode("a+b%C3%A9"));
        Assert.Equal("ab", HttpServer.UrlDecode("a%zzb"));
        Assert.True(HttpServer.ParseTimeHexa("3e8-7D0", out int start, out int stop));
        Assert.Equal((1000, 2000), (start, stop));
        Assert.False(HttpServer.ParseTimeHexa("12", out _, out _));
        Assert.Equal("Thu, 01 Jan 2004 17:11:47 GMT", HttpServer.DateString(new DateTime(2004, 1, 1, 17, 11, 47, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task Oversized_headers_and_bodies_are_rejected_before_allocation()
    {
        var (header, _) = await Send("GET / HTTP/1.0\r\nX: " + new string('a', 9000) + "\r\n\r\n");
        Assert.Equal("HTTP/1.0 431 Request Header Fields Too Large", header[0]);
        (header, _) = await Send("POST /send_suggestion.shtml HTTP/1.0\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: 2147483647\r\n\r\n");
        Assert.Equal("HTTP/1.0 413 Payload Too Large", header[0]);
        Assert.Empty(_host.Suggestions);
    }

    [Fact]
    public async Task Negative_content_length_is_rejected()
    {
        var (header, _) = await Send("POST /send_suggestion.shtml HTTP/1.0\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: -1\r\n\r\n");
        Assert.Equal("HTTP/1.0 400 Bad Request", header[0]);
    }

    [Fact]
    public async Task Idle_connections_expire_and_stop_closes_incomplete_requests()
    {
        using var server = new HttpServer(_root, 0, _host) { RequestTimeoutMilliseconds = 150 };
        Assert.True(server.Start(out _));
        using var idle = new TcpClient();
        await idle.ConnectAsync("127.0.0.1", server.Port, TestContext.Current.CancellationToken);
        byte[] buffer = new byte[1];
        int read = await idle.GetStream().ReadAsync(buffer, TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(0, read);

        using var incomplete = new TcpClient();
        await incomplete.ConnectAsync("127.0.0.1", _server.Port, TestContext.Current.CancellationToken);
        await incomplete.GetStream().WriteAsync("GET / HTTP/1.0\r\nX: incomplete"u8.ToArray(), TestContext.Current.CancellationToken);
        _server.Stop();
        try
        {
            read = await incomplete.GetStream().ReadAsync(buffer, TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(0, read);
        }
        catch (IOException)
        {
            // Closing with unread input may reset the connection instead of sending FIN.
        }
    }
}
