using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using VisualSubSync.Core.Audio;

namespace VisualSubSync.Core.Web;

/// <summary>O que o servidor lê do programa: a lista de legendas e o WAV do projeto naquele instante.</summary>
public sealed record WebSnapshot(PageContext Page, string WavFile);

/// <summary>A janela principal vista pelo servidor (g_GlobalContext e SuggestionForm.AddSuggestion).</summary>
public interface IWebServerHost
{
    /// <summary>Cópia do estado atual, tirada na thread da interface (o legado usa g_WebRWSynchro).</summary>
    WebSnapshot Snapshot();

    /// <summary>SuggestionForm.AddSuggestion(nil, Start, Stop, Text).</summary>
    void AddSuggestion(int startMs, int stopMs, string text);
}

/// <summary>
/// THTTPServer e THTTPRequestThreadProcessor (ServerUnit.pas): servidor HTTP/1.0 mínimo que serve a
/// pasta web, processa as páginas .shtml, gera WAVs virtuais em /wav/inicio-fim.wav e recebe sugestões
/// por POST. Uma conexão por pedido, fechada depois da resposta.
/// </summary>
public sealed class HttpServer : IDisposable
{
    private readonly string _rootDir;
    private readonly IWebServerHost _host;
    private TcpListener? _listener;
    private Task? _acceptLoop;
    private readonly object _clientsGate = new();
    private readonly HashSet<TcpClient> _clients = [];
    private const int MaximumConnections = 16;

    /// <summary>Bound idle reads and writes, including incomplete browser requests.</summary>
    public int RequestTimeoutMilliseconds { get; init; } = 10000;

    public HttpServer(string rootDir, int port, IWebServerHost host)
    {
        ArgumentNullException.ThrowIfNull(rootDir);
        _rootDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDir)) + Path.DirectorySeparatorChar;
        Port = port;
        _host = host;
    }

    public int Port { get; private set; }

    /// <summary>ConfigObject.EnableCompression.</summary>
    public bool EnableCompression { get; set; }

    /// <summary>
    /// Start: escuta em todas as interfaces. O legado apenas encerra a thread quando o bind falha; aqui
    /// a falha é devolvida para poder ir para o log.
    /// </summary>
    public bool Start(out string? error)
    {
        error = null;
        if (_listener is not null)
        {
            return true;
        }

        var listener = new TcpListener(IPAddress.Any, Port);
        try
        {
            listener.Start(4);
        }
        catch (SocketException ex)
        {
            error = ex.Message;
            return false;
        }

        _listener = listener;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(() => AcceptLoop(listener));
        return true;
    }

    public void Stop()
    {
        if (_listener is { } listener)
        {
            _listener = null;
            listener.Stop();
            lock (_clientsGate)
            {
                foreach (var client in _clients)
                {
                    client.Dispose();
                }
            }
            try
            {
                _acceptLoop?.Wait();
            }
            catch (AggregateException)
            {
                // O accept interrompido pelo Stop termina com erro; é o fim normal.
            }

            _acceptLoop = null;
        }
    }

    public void Dispose() => Stop();

    private async Task AcceptLoop(TcpListener listener)
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            lock (_clientsGate)
            {
                if (_listener != listener || _clients.Count >= MaximumConnections)
                {
                    client.Dispose();
                    continue;
                }

                client.ReceiveTimeout = RequestTimeoutMilliseconds;
                client.SendTimeout = RequestTimeoutMilliseconds;
                _clients.Add(client);
            }

            _ = Task.Run(() =>
            {
                using (client)
                {
                    try
                    {
                        using var processor = new RequestProcessor(_rootDir, EnableCompression, _host, client, RequestTimeoutMilliseconds);
                        processor.Execute();
                    }
                    catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidOperationException or TaskCanceledException)
                    {
                        System.Diagnostics.Trace.TraceWarning($"HTTP: {ex.Message}");
                    }
                    finally
                    {
                        lock (_clientsGate)
                        {
                            _clients.Remove(client);
                        }
                    }
                }
            });
        }
    }

    /// <summary>ExtToMIMEType.</summary>
    public static string ExtToMimeType(string ext) => ext switch
    {
        ".html" or ".htm" or ".shtml" => "text/html",
        ".css" => "text/css",
        ".xml" => "text/xml",
        ".txt" => "text/plain",
        ".gif" => "image/gif",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".wav" => "audio/x-wav",
        ".mp3" => "audio/mpeg",
        _ => "text/html",
    };

    /// <summary>ParseTimeHexa: "inicio-fim" em hexadecimal, como o campo time-index.</summary>
    public static bool ParseTimeHexa(string timeStr, out int start, out int stop)
    {
        start = 0;
        stop = 0;
        int i = timeStr.IndexOf('-', StringComparison.Ordinal);
        if (i < 0)
        {
            return false;
        }

        start = HexOrMinusOne(timeStr[..i]);
        stop = HexOrMinusOne(timeStr[(i + 1)..]);
        // A precedência é a do legado: (Start <> -1) and (Stop <> -1) or (Start < Stop).
        return (start != -1 && stop != -1) || start < stop;
    }

    private static int HexOrMinusOne(string s) =>
        int.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int v) ? v : -1;

    /// <summary>URLDecode (MiscToolsUnit.pas): '+' vira espaço e %XX vira o byte; escapes inválidos somem.</summary>
    public static string UrlDecode(string src)
    {
        src = src.Replace('+', ' ');
        var sb = new StringBuilder(src.Length);
        for (int i = 0; i < src.Length; i++)
        {
            if (src[i] != '%')
            {
                sb.Append(src[i]);
                continue;
            }

            string esc = src.Substring(i + 1, Math.Min(2, src.Length - i - 1));
            i += 2;
            int code = HexOrMinusOne(esc);
            if (code is > 0 and < 256)
            {
                sb.Append((char)code);
            }
        }

        return sb.ToString();
    }

    /// <summary>TStringList.DelimitedText: separa pelo delimitador e por espaços e controles.</summary>
    internal static List<string> SplitDelimited(string text, char delimiter)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        foreach (char c in text)
        {
            if (c == delimiter || c <= ' ')
            {
                if (sb.Length > 0)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
        {
            result.Add(sb.ToString());
        }

        return result;
    }

    private static (string Name, string Value) SplitNameValue(string s)
    {
        int p = s.IndexOf('=', StringComparison.Ordinal);
        return p < 0 ? (string.Empty, string.Empty) : (s[..p], s[(p + 1)..]);
    }

    /// <summary>GetDateString: "Thu, 01 Jan 2004 17:11:47 GMT".</summary>
    public static string DateString(DateTime utc) =>
        utc.ToString("ddd, dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture) + " GMT";

    /// <summary>THTTPRequestThreadProcessor: um pedido e a sua resposta.</summary>
    private sealed class RequestProcessor(string rootDir, bool enableCompression, IWebServerHost host, TcpClient client, int timeoutMilliseconds) : IDisposable
    {
        private const int MaximumLineBytes = 8192;
        private const int MaximumHeaderBytes = 32768;
        private const int MaximumBodyBytes = 1048576;
        private readonly System.Diagnostics.Stopwatch _requestClock = System.Diagnostics.Stopwatch.StartNew();
        private int _headerBytes;
        private static readonly string[] DirectoryIndexList = ["index.shtml", "index.html", "index.htm"];
        private readonly Stream _stream = client.GetStream();
        private readonly List<string> _request = [];
        private readonly Dictionary<string, string> _requestHeader = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _answerHeader = [];
        private readonly MemoryStream _answer = new();
        private Dictionary<string, string> _envVars = new(StringComparer.OrdinalIgnoreCase);
        private string _fileToSend = string.Empty;
        private bool _canUseGzip;
        private bool _useGzip;
        private WebSnapshot? _snapshot;

        private WebSnapshot Snapshot => _snapshot ??= host.Snapshot();

        public void Dispose() => _answer.Dispose();

        public void Execute()
        {
            try
            {
                ReceiveRequest();
                ProcessRequestHeaderFields();
                ProcessRequest();
            }
            catch (HttpRequestException ex)
            {
                _answer.SetLength(0);
                _answerHeader.Clear();
                _fileToSend = string.Empty;
                int code = (int)(ex.StatusCode ?? HttpStatusCode.BadRequest);
                _answerHeader.Add($"HTTP/1.0 {code} {ex.Message}");
                _answerHeader.Add("Content-Type: text/html");
                CreateErrorPage(code, ex.Message);
                _answerHeader.Add(string.Empty);
                FlushSocket();
            }

            SendAnswer();
        }

        /// <summary>ReadLine: até o CR, que tem de vir seguido de mais um byte (o LF).</summary>
        private string ReadLine()
        {
            var sb = new StringBuilder();
            int c;
            while ((c = ReadRequestByte()) >= 0)
            {
                if (++_headerBytes > MaximumHeaderBytes || sb.Length >= MaximumLineBytes)
                {
                    throw new HttpRequestException("Request Header Fields Too Large", null, HttpStatusCode.RequestHeaderFieldsTooLarge);
                }

                if (c == '\r')
                {
                    if (ReadRequestByte() != '\n')
                    {
                        throw new HttpRequestException("Bad Request", null, HttpStatusCode.BadRequest);
                    }

                    _headerBytes++;
                    return sb.ToString();
                }

                sb.Append((char)c);
            }

            throw new IOException("Incomplete HTTP request.");
        }

        private int ReadRequestByte()
        {
            CheckDeadline();
            return _stream.ReadByte();
        }

        private void CheckDeadline()
        {
            if (_requestClock.ElapsedMilliseconds >= timeoutMilliseconds)
            {
                throw new IOException("HTTP request timed out.");
            }
        }

        private void ReceiveRequest()
        {
            string line;
            while ((line = ReadLine()).Length > 0)
            {
                _request.Add(line.Trim());
            }
        }

        private void ProcessRequestHeaderFields()
        {
            for (int i = 1; i < _request.Count; i++)
            {
                int p = _request[i].IndexOf(':', StringComparison.Ordinal);
                if (p > 0)
                {
                    string key = _request[i][..p].Trim();
                    string value = _request[i][(p + 1)..].Trim();
                    _requestHeader.TryAdd(key.ToLowerInvariant(), value);
                    SetEnv("HEADER[" + key + "]", value);
                }
            }
        }

        /// <summary>TStrings.Values[...] := valor; o valor vazio apaga a entrada.</summary>
        private void SetEnv(string name, string value)
        {
            if (value.Length == 0)
            {
                _envVars.Remove(name);
            }
            else
            {
                _envVars[name] = value;
            }
        }

        private string Header(string name) => _requestHeader.TryGetValue(name, out string? v) ? v : string.Empty;

        /// <summary>FlushSocket: descarta o que o navegador ainda mandou, para o fechamento não virar RST.</summary>
        private void FlushSocket()
        {
            var buffer = new byte[4096];
            int remaining = MaximumHeaderBytes;
            while (remaining > 0 && client.Available > 0)
            {
                CheckDeadline();
                int read = _stream.Read(buffer, 0, Math.Min(remaining, Math.Min(buffer.Length, client.Available)));
                if (read == 0) break;
                remaining -= read;
            }
        }

        private void ProcessRequest()
        {
            var tokens = _request.Count > 0 ? SplitDelimited(_request[0], (char)20) : [];
            if (tokens.Count >= 3 && tokens[0] == "GET")
            {
                FlushSocket();
                ProcessGet(tokens[1]);
            }
            else if (tokens.Count >= 3 && tokens[0] == "POST")
            {
                ProcessPost();
                FlushSocket();
                ProcessGet(tokens[1]);
            }
            else
            {
                _answerHeader.Add("HTTP/1.0 501 Not Implemented");
                _answerHeader.Add(string.Empty);
                CreateErrorPage(501, "Not Implemented");
            }
        }

        private void ProcessGet(string path)
        {
            if (path.StartsWith("/wav/", StringComparison.Ordinal) && path.EndsWith(".wav", StringComparison.Ordinal) && ProcessVirtualWav(path))
            {
                _answerHeader.Add("HTTP/1.0 200 OK");
                _answerHeader.Add("Content-Type: " + ExtToMimeType(".wav"));
                _answerHeader.Add(string.Empty);
                return;
            }

            _canUseGzip = enableCompression && Header("accept-encoding").Contains("deflate", StringComparison.Ordinal);
            string fileName = path.Replace('/', Path.DirectorySeparatorChar);
            int i = fileName.IndexOf('#', StringComparison.Ordinal);
            if (i >= 0)
            {
                fileName = fileName[..i];
            }

            i = fileName.IndexOf('?', StringComparison.Ordinal);
            if (i >= 0)
            {
                // DelimitedText troca a lista inteira pelos parâmetros da URL (?k1=v1&k2=v2).
                _envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string param in SplitDelimited(fileName[(i + 1)..], '&'))
                {
                    var (name, value) = SplitNameValue(param);
                    _envVars.TryAdd(name, value);
                }

                fileName = fileName[..i];
            }

            string root = Path.TrimEndingDirectorySeparator(rootDir);
            try
            {
                fileName = Path.GetFullPath(rootDir + fileName);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                fileName = string.Empty;
            }

            if (!fileName.StartsWith(root, StringComparison.Ordinal))
            {
                _answerHeader.Add("HTTP/1.0 403 Forbidden");
                CreateErrorPage(403, "Forbidden : RootDir = " + rootDir + "<br>Access path = " + path + "<br>Filename = " + fileName);
                _answerHeader.Add(string.Empty);
                return;
            }

            if (Directory.Exists(fileName))
            {
                string? index = DirectoryIndexList
                    .Select(n => Path.Combine(fileName, n))
                    .FirstOrDefault(File.Exists);
                if (index is null)
                {
                    _answerHeader.Add("HTTP/1.0 501 Not Implemented");
                    CreateErrorPage(501, "Directory listing not implemented.");
                    _answerHeader.Add(string.Empty);
                    return;
                }

                fileName = index;
            }

            if (File.Exists(fileName))
            {
                _answerHeader.Add("HTTP/1.0 200 OK");
                string ext = Path.GetExtension(fileName).ToLowerInvariant();
                _answerHeader.Add("Content-Type: " + ExtToMimeType(ext));
                if (ext == ".shtml")
                {
                    ProcessDynamicPage(fileName);
                }
                else
                {
                    _fileToSend = fileName;
                }
            }
            else
            {
                _answerHeader.Add("HTTP/1.0 404 Not Found");
                CreateErrorPage(404, "Not Found : " + fileName);
            }

            _answerHeader.Add(string.Empty);
        }

        private void ProcessDynamicPage(string fileName)
        {
            byte[] page = PageProcessor.ProcessPage(File.ReadAllBytes(fileName), Snapshot.Page, _envVars);
            if (_canUseGzip)
            {
                // TCompressionStream: formato zlib, que é o que "deflate" significa no HTTP.
                _useGzip = true;
                using var z = new ZLibStream(_answer, CompressionLevel.Optimal, leaveOpen: true);
                z.Write(page);
            }
            else
            {
                _answer.Write(page);
            }
        }

        private bool ProcessVirtualWav(string virtualPath)
        {
            string s = virtualPath.Length > 9 ? virtualPath.Substring(5, virtualPath.Length - 9) : string.Empty;
            if (!ParseTimeHexa(s, out int start, out int stop))
            {
                return false;
            }

            using var wav = WavFile.Open(Snapshot.WavFile);
            if (wav is null)
            {
                return false;
            }

            try
            {
                wav.ExtractToStream(start, stop, _answer);
                return true;
            }
            catch (EndOfStreamException)
            {
                _answer.SetLength(0);
                return false;
            }
        }

        private void ProcessPost()
        {
            var postVars = new List<string>();
            if (Header("content-type") == "application/x-www-form-urlencoded")
            {
                if (!int.TryParse(Header("content-length"), NumberStyles.None, CultureInfo.InvariantCulture, out int length) || length < 0)
                {
                    throw new HttpRequestException("Bad Request", null, HttpStatusCode.BadRequest);
                }

                if (length > MaximumBodyBytes)
                {
                    throw new HttpRequestException("Payload Too Large", null, HttpStatusCode.RequestEntityTooLarge);
                }

                if (length > 0)
                {
                    var content = new byte[length];
                    int read = 0;
                    while (read < length)
                    {
                        CheckDeadline();
                        int received = _stream.Read(content, read, length - read);
                        if (received == 0)
                        {
                            throw new HttpRequestException("Bad Request", null, HttpStatusCode.BadRequest);
                        }

                        read += received;
                    }
                    postVars = SplitDelimited(Encoding.Latin1.GetString(content, 0, read), '&');
                }
            }

            var decoded = new List<(string Name, string Value)>();
            foreach (string entry in postVars)
            {
                var (name, value) = SplitNameValue(entry);
                SetEnv("POST[" + name + "]", value);
                decoded.Add(SplitNameValue(UrlDecode(entry)));
            }

            string Post(string name) => decoded.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;

            if (Post("vss-command") == "SUGGESTION")
            {
                ParseTimeHexa(Post("time-index"), out int start, out int stop);
                // UTF8Decode do texto, que chega como bytes.
                host.AddSuggestion(start, stop, Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(Post("text"))));
                SetEnv("post-result", "Suggestion sent successfully.");
            }
            else
            {
                SetEnv("post-result", "Unknown vss-command.");
            }
        }

        private void CreateErrorPage(int errorCode, string msg)
        {
            void Line(string s) => _answer.Write(Encoding.Latin1.GetBytes(s + "\r\n"));
            Line("<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">");
            Line("<html><head>");
            Line(string.Create(CultureInfo.InvariantCulture, $"<title>{errorCode} {msg}</title>"));
            Line("</head><body>");
            Line(string.Create(CultureInfo.InvariantCulture, $"<h2>{errorCode} ERROR : {msg}</h2>"));
            Line("</body></html>");
        }

        private void SendAnswer()
        {
            long size = _fileToSend.Length > 0 ? new FileInfo(_fileToSend).Length : _answer.Length;
            if (_useGzip)
            {
                _answerHeader.Insert(1, "Content-encoding: deflate");
                _answerHeader.Insert(2, "Vary: Accept-Encoding");
            }

            _answerHeader.Insert(1, "Content-Length: " + size.ToString(CultureInfo.InvariantCulture));
            _answerHeader.Insert(1, "Server: VisualSubSync/" + Snapshot.Page.Version);
            _answerHeader.Insert(1, "Date: " + DateString(DateTime.UtcNow));
            var text = new StringBuilder();
            foreach (string line in _answerHeader)
            {
                text.Append(line).Append("\r\n");
            }

            _stream.Write(Encoding.Latin1.GetBytes(text.ToString()));
            if (_fileToSend.Length > 0)
            {
                using var file = new FileStream(_fileToSend, FileMode.Open, FileAccess.Read, FileShare.Read);
                file.CopyTo(_stream);
            }
            else
            {
                _answer.Position = 0;
                _answer.CopyTo(_stream);
            }

            _stream.Flush();
        }
    }
}
