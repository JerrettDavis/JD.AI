using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;

namespace JD.AI.Channels.OpenClaw;

/// <summary>
/// JSON-RPC-over-WebSocket client for communicating with an OpenClaw gateway.
/// Implements the OpenClaw v3 protocol: challenge-response device auth with
/// Ed25519 signatures, typed request/response/event frames, and automatic reconnection.
/// </summary>
public sealed class OpenClawRpcClient : IAsyncDisposable
{
    private const int ProtocolVersion = 3;
    private const string ClientId = "cli";
    private const string ClientMode = "cli";
    private const string DefaultRole = "operator";

    private static readonly string[] DefaultScopes =
        ["operator.admin", "operator.read", "operator.write", "operator.approvals", "operator.pairing"];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly OpenClawConfig _config;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RpcResponse>> _pending = new();

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private int _rpcId;
    private bool _disposed;

    /// <summary>Raised when an event frame is received from the gateway.</summary>
#pragma warning disable CA1003 // Event callback pattern requires Action<T> for synchronous invocation
    public event Action<OpenClawEvent>? EventReceived;
#pragma warning restore CA1003

    /// <summary>Whether the client is connected and authenticated.</summary>
    public bool IsConnected { get; private set; }

    public OpenClawRpcClient(OpenClawConfig config, ILogger<OpenClawRpcClient> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Connects to the OpenClaw gateway, performs challenge-response device authentication,
    /// and starts the receive loop.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _ws = new ClientWebSocket();
        var uri = new Uri(_config.WebSocketUrl);
        await _ws.ConnectAsync(uri, ct);
        _logger.LogDebug("WebSocket connected to {Url}", uri);

        // Wait for challenge
        var challengeFrame = await ReceiveFrameAsync(ct);
        if (challengeFrame.Type is not "event" || !string.Equals(challengeFrame.Event, "connect.challenge", StringComparison.Ordinal))
            throw new InvalidOperationException($"Expected connect.challenge, got: {challengeFrame.Type}/{challengeFrame.Event}");

        var nonce = challengeFrame.Payload?.GetProperty("nonce").GetString()
            ?? throw new InvalidOperationException("Missing nonce in challenge");

        // Build & send connect request with device auth
        var connectResponse = await SendConnectAsync(nonce, ct);
        if (!connectResponse.Ok)
        {
            var error = connectResponse.Error?.GetProperty("message").GetString() ?? "unknown";
            throw new InvalidOperationException($"OpenClaw auth failed: {error}");
        }

        IsConnected = true;
        _logger.LogInformation("Authenticated to OpenClaw gateway at {Url}", uri);

        // Start receive loop
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _receiveTask = ReceiveLoopAsync(_receiveCts.Token);
    }

    /// <summary>Sends an RPC request and waits for the response.</summary>
    public async Task<RpcResponse> RequestAsync(string method, object? parameters = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_ws?.State != WebSocketState.Open)
            throw new InvalidOperationException("Not connected");

        var id = "r" + Interlocked.Increment(ref _rpcId);
        var tcs = new TaskCompletionSource<RpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        try
        {
            var frame = new
            {
                type = "req",
                id,
                method,
                @params = parameters ?? new { },
            };

            var json = JsonSerializer.Serialize(frame, JsonOpts);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await using var reg = timeout.Token.Register(() => tcs.TrySetCanceled());

            return await tcs.Task;
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    /// <summary>Gracefully disconnects from the gateway.</summary>
    public async Task DisconnectAsync()
    {
        if (_receiveCts is not null)
        {
            await _receiveCts.CancelAsync();
            if (_receiveTask is not null)
            {
                try { await _receiveTask; }
                catch (OperationCanceledException) { }
            }

            _receiveCts.Dispose();
            _receiveCts = null;
        }

        if (_ws is { State: WebSocketState.Open })
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "disconnect", CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error closing WebSocket");
            }
        }

        _ws?.Dispose();
        _ws = null;
        IsConnected = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await DisconnectAsync();
    }

    private async Task<RpcResponse> SendConnectAsync(string nonce, CancellationToken ct)
    {
        var signedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Build the device auth payload: "v2|deviceId|clientId|clientMode|role|scopes|signedAtMs|token|nonce"
        // The token in the payload is the gateway shared token (not the device operator token)
        var scopesCsv = string.Join(",", DefaultScopes);
        var gatewayToken = !string.IsNullOrEmpty(_config.GatewayToken) ? _config.GatewayToken : _config.DeviceToken;
        var payloadStr = $"v2|{_config.DeviceId}|{ClientId}|{ClientMode}|{DefaultRole}|{scopesCsv}|{signedAtMs}|{gatewayToken}|{nonce}";

        // Sign with Ed25519 using NSec
        var signature = SignEd25519(_config.PrivateKeyPem, payloadStr);
        var publicKeyRawB64 = GetPublicKeyRawBase64Url(_config.PublicKeyPem);

        var connectId = "r" + Interlocked.Increment(ref _rpcId);

        var frame = new
        {
            type = "req",
            id = connectId,
            method = "connect",
            @params = new
            {
                minProtocol = ProtocolVersion,
                maxProtocol = ProtocolVersion,
                client = new
                {
                    id = ClientId,
                    displayName = "JD.AI Bridge",
                    version = "1.0.0",
                    platform = OperatingSystem.IsWindows() ? "win32" : "linux",
                    mode = ClientMode,
                    instanceId = $"jdai-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                },
                caps = Array.Empty<string>(),
                auth = new { token = gatewayToken },
                role = DefaultRole,
                scopes = DefaultScopes,
                device = new
                {
                    id = _config.DeviceId,
                    publicKey = publicKeyRawB64,
                    signature,
                    signedAt = signedAtMs,
                    nonce,
                },
            },
        };

        var json = JsonSerializer.Serialize(frame, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws!.SendAsync(bytes, WebSocketMessageType.Text, true, ct);

        // Read the connect response directly (receive loop isn't running yet)
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var responseFrame = await ReceiveFrameAsync(timeout.Token);

        return new RpcResponse
        {
            Ok = responseFrame.Ok ?? false,
            Payload = responseFrame.Payload,
            Error = responseFrame.Error,
        };
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
        {
            try
            {
                var frame = await ReceiveFrameAsync(ct);

                if (string.Equals(frame.Type, "res", StringComparison.Ordinal))
                {
                    if (frame.Id is not null && _pending.TryGetValue(frame.Id, out var tcs))
                    {
                        tcs.TrySetResult(new RpcResponse
                        {
                            Ok = frame.Ok ?? false,
                            Payload = frame.Payload,
                            Error = frame.Error,
                        });
                    }
                }
                else if (string.Equals(frame.Type, "event", StringComparison.Ordinal))
                {
                    EventReceived?.Invoke(new OpenClawEvent
                    {
                        EventName = frame.Event ?? "unknown",
                        Payload = frame.Payload,
                    });
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.LogWarning("OpenClaw WebSocket closed prematurely");
                IsConnected = false;
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OpenClaw receive loop");
                await Task.Delay(1000, ct);
            }
        }

        IsConnected = false;
    }

    private async Task<RawFrame> ReceiveFrameAsync(CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;

        do
        {
            result = await _ws!.ReceiveAsync(buffer, ct);
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        if (result.MessageType == WebSocketMessageType.Close)
            throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "Server closed connection");

        var json = Encoding.UTF8.GetString(ms.ToArray());
        return JsonSerializer.Deserialize<RawFrame>(json, JsonOpts) ?? throw new InvalidOperationException("Null frame");
    }

    /// <summary>
    /// Signs a payload string with the Ed25519 private key from PEM.
    /// The PEM contains a PKCS#8-encoded Ed25519 key; the last 32 bytes are the raw seed.
    /// </summary>
    private static string SignEd25519(string privateKeyPem, string payload)
    {
        var seed = ExtractEd25519Seed(privateKeyPem);
        var algo = SignatureAlgorithm.Ed25519;
        using var key = Key.Import(algo, seed, KeyBlobFormat.RawPrivateKey);
        var sig = algo.Sign(key, Encoding.UTF8.GetBytes(payload));
        return Base64UrlEncode(sig);
    }

    /// <summary>Extracts the raw 32-byte Ed25519 public key from a PEM-encoded SPKI.</summary>
    private static string GetPublicKeyRawBase64Url(string publicKeyPem)
    {
        var pem = publicKeyPem.Replace("-----BEGIN PUBLIC KEY-----", "")
            .Replace("-----END PUBLIC KEY-----", "")
            .Replace("\n", "").Replace("\r", "").Trim();
        var spki = Convert.FromBase64String(pem);
        // Ed25519 SPKI is 44 bytes; raw key is last 32 bytes
        var raw = spki.AsSpan(spki.Length - 32).ToArray();
        return Base64UrlEncode(raw);
    }

    /// <summary>Extracts the 32-byte Ed25519 seed from a PEM-encoded PKCS#8 private key.</summary>
    private static byte[] ExtractEd25519Seed(string privateKeyPem)
    {
        var pem = privateKeyPem.Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("\n", "").Replace("\r", "").Trim();
        var pkcs8 = Convert.FromBase64String(pem);
        // PKCS#8 Ed25519: 48 bytes, structure ends with 32-byte seed
        return pkcs8.AsSpan(pkcs8.Length - 32).ToArray();
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed class RawFrame
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("event")]
        public string? Event { get; set; }

        [JsonPropertyName("ok")]
        public bool? Ok { get; set; }

        [JsonPropertyName("payload")]
        public JsonElement? Payload { get; set; }

        [JsonPropertyName("error")]
        public JsonElement? Error { get; set; }

        [JsonPropertyName("method")]
        public string? Method { get; set; }
    }
}

/// <summary>Response from an OpenClaw RPC call.</summary>
public sealed class RpcResponse
{
    public bool Ok { get; init; }

#pragma warning disable CA1721 // Payload property is the canonical name; GetPayload<T> is a typed accessor
    public JsonElement? Payload { get; init; }
#pragma warning restore CA1721

    public JsonElement? Error { get; init; }

    public T? GetPayload<T>() =>
        Payload.HasValue ? JsonSerializer.Deserialize<T>(Payload.Value.GetRawText()) : default;
}

/// <summary>An event received from the OpenClaw gateway.</summary>
public sealed class OpenClawEvent
{
    public required string EventName { get; init; }
    public JsonElement? Payload { get; init; }
}
