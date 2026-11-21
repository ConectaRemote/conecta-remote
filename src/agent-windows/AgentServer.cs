using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ConectaRemoteAgent;

/// <summary>
/// Servidor local do ConectaRemote Portable.
/// Escuta apenas em 127.0.0.1, valida a origem do navegador, exige pareamento por código
/// de uso único e executa somente mouse e teclado de uma sessão autorizada.
/// </summary>
internal sealed class AgentServer : IDisposable
{
    private const string WebSocketPath = "/conecta";

    private readonly AgentConfig _config;
    private readonly string _deviceName;
    private readonly string _version;
    private readonly InputExecutor _input = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _stateGate = new();

    private TcpListener? _listener;
    private WebSocket? _socket;
    private bool _paired;
    private bool _controlling;
    private string _sessionId = "";

    public AgentServer(AgentConfig config, string deviceName, string version)
    {
        _config = config;
        _deviceName = deviceName;
        _version = version;
    }

    /// <summary>(título, detalhe) para a bandeja do sistema e a janela.</summary>
    public event Action<string, string>? StatusChanged;

    public bool IsControlling
    {
        get
        {
            lock (_stateGate) return _controlling;
        }
    }

    public bool IsPaired
    {
        get
        {
            lock (_stateGate) return _paired;
        }
    }

    public string DeviceName => _deviceName;

    public bool Start()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, _config.Port);
            _listener.Start();
        }
        catch
        {
            return false;
        }

        _ = Task.Run(AcceptLoopAsync);
        return true;
    }

    /// <summary>Interrupção imediata pedida pelo usuário assistido (atalho ou bandeja).</summary>
    public void EmergencyStop(string reason)
    {
        _input.ReleaseAll();

        bool wasControlling;
        lock (_stateGate)
        {
            wasControlling = _controlling;
            _controlling = false;
        }

        var socket = _socket;
        if (socket is { State: WebSocketState.Open })
        {
            _ = SendAsync(socket, new { type = "emergency_stop", reason });
        }

        if (wasControlling)
        {
            StatusChanged?.Invoke("Controle interrompido", "Nenhuma entrada está sendo executada neste computador.");
        }
    }

    // ---------- Servidor local ----------

    private async Task AcceptLoopAsync()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync().ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            client.NoDelay = true;
            var stream = client.GetStream();

            string? origin;
            try
            {
                origin = await HandshakeAsync(stream).ConfigureAwait(false);
            }
            catch
            {
                return;
            }
            if (origin == null) return;

            using var socket = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions
            {
                IsServer = true,
                KeepAliveInterval = TimeSpan.FromSeconds(20),
            });

            _socket = socket;
            var buffer = new byte[16 * 1024];
            var message = new MemoryStream();

            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None)
                        .ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close) break;

                    message.Write(buffer, 0, result.Count);
                    if (!result.EndOfMessage) continue;

                    var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                    message.SetLength(0);
                    await HandleMessageAsync(socket, text).ConfigureAwait(false);
                }
            }
            catch
            {
                // conexão encerrada pelo navegador
            }
            finally
            {
                _input.ReleaseAll();
                lock (_stateGate)
                {
                    _controlling = false;
                    _paired = false;
                    _sessionId = "";
                }
                _socket = null;
                StatusChanged?.Invoke("Agente pronto", "Aguardando pareamento do navegador.");
            }
        }
    }

    private async Task<string?> HandshakeAsync(NetworkStream stream)
    {
        var raw = new StringBuilder();
        var one = new byte[1];
        while (raw.Length < 8192)
        {
            var read = await stream.ReadAsync(new ArraySegment<byte>(one), CancellationToken.None).ConfigureAwait(false);
            if (read == 0) return null;
            raw.Append((char)one[0]);
            if (raw.Length >= 4 && raw.ToString(raw.Length - 4, 4) == "\r\n\r\n") break;
        }

        var lines = raw.ToString().Split("\r\n");
        if (lines.Length == 0) return null;

        var requestLine = lines[0].Split(' ');
        if (requestLine.Length < 2 || !requestLine[1].StartsWith(WebSocketPath, StringComparison.OrdinalIgnoreCase))
        {
            await WriteHttpErrorAsync(stream, "404 Not Found").ConfigureAwait(false);
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var index = line.IndexOf(':');
            if (index <= 0) continue;
            headers[line[..index].Trim()] = line[(index + 1)..].Trim();
        }

        var origin = headers.TryGetValue("Origin", out var value) ? value : null;
        if (!IsAllowedOrigin(origin))
        {
            await WriteHttpErrorAsync(stream, "403 Forbidden").ConfigureAwait(false);
            return null;
        }

        if (!headers.TryGetValue("Sec-WebSocket-Key", out var key))
        {
            await WriteHttpErrorAsync(stream, "400 Bad Request").ConfigureAwait(false);
            return null;
        }

        var accept = Convert.ToBase64String(
            SHA1.HashData(Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

        var response = "HTTP/1.1 101 Switching Protocols\r\n" +
                       "Upgrade: websocket\r\n" +
                       "Connection: Upgrade\r\n" +
                       $"Sec-WebSocket-Accept: {accept}\r\n\r\n";

        var bytes = Encoding.ASCII.GetBytes(response);
        await stream.WriteAsync(new ArraySegment<byte>(bytes), CancellationToken.None).ConfigureAwait(false);
        await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        return origin!;
    }

    private static async Task WriteHttpErrorAsync(NetworkStream stream, string status)
    {
        var body = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");
        await stream.WriteAsync(new ArraySegment<byte>(body), CancellationToken.None).ConfigureAwait(false);
    }

    private bool IsAllowedOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return false;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;

        foreach (var allowed in _config.AllowedOrigins)
        {
            if (string.Equals(allowed.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (_config.AllowBase44Subdomains && uri.Host.EndsWith(".base44.app", StringComparison.OrdinalIgnoreCase))
            return true;

        if (_config.AllowLocalOrigins && (uri.Host == "localhost" || uri.Host == "127.0.0.1"))
            return true;

        return false;
    }

    // ---------- Protocolo ----------

    private async Task HandleMessageAsync(WebSocket socket, string text)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch
        {
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeProperty)) return;
            var type = typeProperty.GetString();

            switch (type)
            {
                case "hello":
                {
                    var code = root.TryGetProperty("pairingCode", out var codeProperty)
                        ? (codeProperty.GetString() ?? "")
                        : "";
                    var sessionId = root.TryGetProperty("sessionId", out var sessionProperty)
                        ? (sessionProperty.GetString() ?? "")
                        : "";

                    if (!await VerifyPairingAsync(code).ConfigureAwait(false))
                    {
                        await SendAsync(socket, new
                        {
                            type = "error",
                            code = "pairing_rejected",
                            message = "Código de pareamento inválido ou expirado. Gere um novo na sala de assistência.",
                        }).ConfigureAwait(false);
                        return;
                    }

                    lock (_stateGate)
                    {
                        _paired = true;
                        _sessionId = sessionId;
                        _controlling = false;
                    }

                    StatusChanged?.Invoke("Agente pareado", $"Conectado ao navegador de {_deviceName}.");

                    var (width, height) = InputExecutor.ScreenSize();
                    await SendAsync(socket, new
                    {
                        type = "hello_ok",
                        device = new { name = _deviceName, version = _version, platform = "windows" },
                        screens = new[] { new { width, height, primary = true } },
                    }).ConfigureAwait(false);
                    return;
                }

                case "pointer":
                {
                    if (!TryReadPointer(root, out var action, out var x, out var y, out var button,
                            out var deltaX, out var deltaY))
                        return;

                    switch (action)
                    {
                        case "move":
                            _input.MovePointer(x, y);
                            MarkControlling(socket);
                            break;
                        case "down":
                        case "up":
                            _input.MovePointer(x, y);
                            _input.PointerButton(action, button);
                            MarkControlling(socket);
                            break;
                        case "wheel":
                            _input.Wheel(deltaX, deltaY);
                            MarkControlling(socket);
                            break;
                    }
                    return;
                }

                case "key":
                {
                    if (!AuthorizedForSession(root)) return;
                    if (!root.TryGetProperty("action", out var actionProperty)) return;

                    var action = actionProperty.GetString() ?? "";
                    var code = root.TryGetProperty("code", out var codeProperty)
                        ? (codeProperty.GetString() ?? "")
                        : "";

                    _input.Key(action, code);
                    return;
                }

                case "release_all":
                case "revoke":
                {
                    _input.ReleaseAll();
                    lock (_stateGate) _controlling = false;
                    if (type == "revoke") StatusChanged?.Invoke("Agente pronto", "Controle encerrado pelo navegador.");
                    return;
                }

                case "ping":
                {
                    await SendAsync(socket, new { type = "pong", controlling = IsControlling }).ConfigureAwait(false);
                    return;
                }
            }
        }
    }

    private bool AuthorizedForSession(JsonElement root)
    {
        bool paired;
        bool controlling;
        string sessionId;
        lock (_stateGate)
        {
            paired = _paired;
            controlling = _controlling;
            sessionId = _sessionId;
        }

        if (!paired || !controlling) return false;
        if (string.IsNullOrEmpty(sessionId)) return true;
        if (!root.TryGetProperty("sessionId", out var property)) return false;
        return string.Equals(property.GetString(), sessionId, StringComparison.Ordinal);
    }

    private bool TryReadPointer(JsonElement root, out string action, out double x, out double y,
        out string button, out double deltaX, out double deltaY)
    {
        action = "";
        x = 0;
        y = 0;
        button = "left";
        deltaX = 0;
        deltaY = 0;

        bool paired;
        string sessionId;
        lock (_stateGate)
        {
            paired = _paired;
            sessionId = _sessionId;
        }

        if (!paired) return false;
        if (!string.IsNullOrEmpty(sessionId))
        {
            if (!root.TryGetProperty("sessionId", out var property)) return false;
            if (!string.Equals(property.GetString(), sessionId, StringComparison.Ordinal)) return false;
        }

        if (!root.TryGetProperty("action", out var actionProperty)) return false;

        action = actionProperty.GetString() ?? "";
        if (root.TryGetProperty("x", out var xProperty) && xProperty.ValueKind == JsonValueKind.Number)
            x = xProperty.GetDouble();
        if (root.TryGetProperty("y", out var yProperty) && yProperty.ValueKind == JsonValueKind.Number)
            y = yProperty.GetDouble();
        if (root.TryGetProperty("button", out var buttonProperty))
            button = buttonProperty.GetString() ?? "left";
        if (root.TryGetProperty("deltaX", out var dxProperty) && dxProperty.ValueKind == JsonValueKind.Number)
            deltaX = dxProperty.GetDouble();
        if (root.TryGetProperty("deltaY", out var dyProperty) && dyProperty.ValueKind == JsonValueKind.Number)
            deltaY = dyProperty.GetDouble();

        return true;
    }

    private void MarkControlling(WebSocket socket)
    {
        bool already;
        lock (_stateGate)
        {
            already = _controlling;
            _controlling = true;
        }

        if (already) return;

        StatusChanged?.Invoke("Controle remoto ativo", $"{_deviceName} está recebendo mouse e teclado.");
        _ = SendAsync(socket, new { type = "input_ack", controlling = true });
    }

    private async Task<bool> VerifyPairingAsync(string code)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^\\d{6}$")) return false;
        if (string.IsNullOrWhiteSpace(_config.AppBaseUrl)) return false;

        var payload = JsonSerializer.Serialize(new
        {
            code,
            device = new { name = _deviceName, platform = "windows", version = _version },
        });

        try
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _http
                .PostAsync($"{_config.AppBaseUrl.TrimEnd('/')}/api/agent/verify", content)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task SendAsync(WebSocket socket, object payload)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (socket.State != WebSocketState.Open) return;
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // conexão encerrada
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        _input.ReleaseAll();
        try
        {
            _socket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "encerrado", CancellationToken.None)
                .Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignora
        }
        _listener?.Stop();
        _http.Dispose();
        _sendLock.Dispose();
    }
}