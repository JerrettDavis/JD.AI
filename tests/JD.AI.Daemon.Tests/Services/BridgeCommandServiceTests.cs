namespace JD.AI.Daemon.Tests.Services;

public sealed class BridgeCommandServiceTests
{
    [Fact]
    public async Task DisableBridgeRuntimeDirectAsync_RemovesConfiguredNonPrefixedAgentAndSessions()
    {
        var tempDir = Directory.CreateTempSubdirectory("jdai-daemon-openclaw-");
        await using var server = new FakeOpenClawRpcServer(
            configJson: """
                        {
                          "agents": {
                            "list": [
                              { "id": "custom-jdai", "name": "JD.AI Custom" },
                              { "id": "native-assistant", "name": "Native" }
                            ]
                          },
                          "bindings": [
                            { "agentId": "custom-jdai", "match": { "channel": "signal" } },
                            { "agentId": "native-assistant", "match": { "channel": "discord" } }
                          ]
                        }
                        """,
            sessions:
            [
                "agent:custom-jdai:main",
                "agent:jdai-default:main",
                "agent:native-assistant:main",
            ]);

        try
        {
            WriteTestOpenClawIdentity(tempDir.FullName);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["Gateway:OpenClaw:WebSocketUrl"] = server.WebSocketUrl,
                    ["Gateway:OpenClaw:StateDir"] = tempDir.FullName,
                    ["Gateway:OpenClaw:RegisterAgents:0:Id"] = "custom-jdai",
                    ["Gateway:OpenClaw:RegisterAgents:0:Bindings:0:Channel"] = "signal",
                })
                .Build();

            var sut = new DirectCleanupBridgeCommandService();
            await sut.InvokeDisableBridgeRuntimeDirectAsync(config);

            var configAfter = server.CurrentConfig;
            var agentIds = configAfter["agents"]?["list"]?.AsArray()
                .Select(node => node?["id"]?.GetValue<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToArray() ?? [];
            var bindingAgentIds = configAfter["bindings"]?.AsArray()
                .Select(node => node?["agentId"]?.GetValue<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToArray() ?? [];

            Assert.DoesNotContain("custom-jdai", agentIds, StringComparer.Ordinal);
            Assert.DoesNotContain("custom-jdai", bindingAgentIds, StringComparer.Ordinal);
            Assert.Contains("native-assistant", agentIds, StringComparer.Ordinal);
            Assert.Contains("agent:custom-jdai:main", server.DeletedSessionKeys, StringComparer.Ordinal);
            Assert.Contains("agent:jdai-default:main", server.ResetSessionKeys, StringComparer.Ordinal);
            Assert.DoesNotContain("agent:native-assistant:main", server.DeletedSessionKeys, StringComparer.Ordinal);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NullActionFallsBackToStatus()
    {
        var sut = new SpyBridgeCommandService();

        var code = await sut.ExecuteAsync(null);

        Assert.Equal(0, code);
        Assert.Equal(["read", "write"], sut.Calls);
    }

    [Fact]
    public void WriteStatus_WhenEnabledButNoOverrides_UsesObserveOnlyMessage()
    {
        var sut = new StatusBridgeCommandService();
        var state = new OpenClawBridgeState(
            Enabled: true,
            AutoConnect: true,
            DefaultMode: "Passthrough",
            OverrideActive: false,
            OverrideChannels: []);

        var original = Console.Out;
        using var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            sut.InvokeWriteStatus(state, "appsettings.json");
        }
        finally
        {
            Console.SetOut(original);
        }

        var output = writer.ToString();
        Assert.Contains("Effective mode:  Passthrough/observe-only", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestartInstalledServiceAsync_WhenStopFails_DoesNotStartService()
    {
        var manager = new RecordingServiceManager
        {
            StatusToReturn = new ServiceStatus(ServiceState.Running, null, null, null),
            StopToReturn = new ServiceResult(false, "stop failed"),
        };
        var sut = new RestartBridgeCommandService(() => manager);

        await sut.InvokeRestartInstalledServiceAsync();

        Assert.Equal(1, manager.GetStatusCalls);
        Assert.Equal(1, manager.StopCalls);
        Assert.Equal(0, manager.StartCalls);
    }

    [Fact]
    public async Task RestartInstalledServiceAsync_WhenStartFails_StillAttemptsRestart()
    {
        var manager = new RecordingServiceManager
        {
            StatusToReturn = new ServiceStatus(ServiceState.Running, null, null, null),
            StopToReturn = new ServiceResult(true, "stopped"),
            StartToReturn = new ServiceResult(false, "start failed"),
        };
        var sut = new RestartBridgeCommandService(() => manager);

        var original = Console.Out;
        using var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            await sut.InvokeRestartInstalledServiceAsync();
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Equal(1, manager.GetStatusCalls);
        Assert.Equal(1, manager.StopCalls);
        Assert.Equal(1, manager.StartCalls);
        Assert.Contains("could not be restarted automatically", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisableBridgeRuntimeAsync_WhenHttpCleanupFails_FallsBackToDirectCleanup()
    {
        var tempDir = Directory.CreateTempSubdirectory("jdai-daemon-test-");
        var configPath = Path.Combine(tempDir.FullName, "appsettings.json");
        var server = new RuntimeCleanupServer(HttpStatusCode.InternalServerError);
        try
        {
            await File.WriteAllTextAsync(configPath, $$"""
                                                     {
                                                       "Gateway": {
                                                         "Server": { "Port": {{server.Port}} },
                                                         "OpenClaw": {
                                                           "StateDir": "{{tempDir.FullName.Replace("\\", "\\\\")}}"
                                                         }
                                                       }
                                                     }
                                                     """);

            var sut = new RuntimeCleanupBridgeCommandService();
            await sut.InvokeDisableBridgeRuntimeAsync(configPath);

            Assert.True(sut.DirectCleanupInvoked);
        }
        finally
        {
            await server.DisposeAsync();
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task DisableBridgeRuntimeAsync_WhenHttpCleanupSucceeds_SkipsDirectCleanup()
    {
        var tempDir = Directory.CreateTempSubdirectory("jdai-daemon-test-");
        var configPath = Path.Combine(tempDir.FullName, "appsettings.json");
        var server = new RuntimeCleanupServer(HttpStatusCode.OK);
        try
        {
            await File.WriteAllTextAsync(configPath, $$"""
                                                     {
                                                       "Gateway": {
                                                         "Server": { "Port": {{server.Port}} },
                                                         "OpenClaw": {
                                                           "StateDir": "{{tempDir.FullName.Replace("\\", "\\\\")}}"
                                                         }
                                                       }
                                                     }
                                                     """);

            var sut = new RuntimeCleanupBridgeCommandService();
            await sut.InvokeDisableBridgeRuntimeAsync(configPath);

            Assert.False(sut.DirectCleanupInvoked);
        }
        finally
        {
            await server.DisposeAsync();
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task DisableBridgeRuntimeDirectAsync_WhenIdentityIsIncomplete_WritesSkipNote()
    {
        var tempDir = Directory.CreateTempSubdirectory("jdai-daemon-test-");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["Gateway:OpenClaw:StateDir"] = tempDir.FullName,
                })
                .Build();

            var sut = new DirectCleanupBridgeCommandService();

            var original = Console.Out;
            using var writer = new StringWriter();
            try
            {
                Console.SetOut(writer);
                await sut.InvokeDisableBridgeRuntimeDirectAsync(config);
            }
            finally
            {
                Console.SetOut(original);
            }

            Assert.Contains("direct cleanup skipped", writer.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    private sealed class SpyBridgeCommandService : BridgeCommandService
    {
        public List<string> Calls { get; } = [];

        public SpyBridgeCommandService()
            : base("appsettings.json", () => new FakeServiceManager())
        {
        }

        protected override OpenClawBridgeState ReadState(string appSettingsPath)
        {
            Calls.Add("read");
            return new OpenClawBridgeState(true, true, "Passthrough", false, []);
        }

        protected override void WriteStatus(OpenClawBridgeState state, string appSettingsPath)
        {
            Calls.Add("write");
        }
    }

    private sealed class StatusBridgeCommandService : BridgeCommandService
    {
        public StatusBridgeCommandService()
            : base("appsettings.json", () => new FakeServiceManager())
        {
        }

        public void InvokeWriteStatus(OpenClawBridgeState state, string path) => base.WriteStatus(state, path);
    }

    private sealed class RestartBridgeCommandService : BridgeCommandService
    {
        public RestartBridgeCommandService(Func<IServiceManager> managerFactory)
            : base("appsettings.json", managerFactory)
        {
        }

        public Task InvokeRestartInstalledServiceAsync() => base.RestartInstalledServiceAsync();
    }

    private sealed class RuntimeCleanupBridgeCommandService : BridgeCommandService
    {
        public bool DirectCleanupInvoked { get; private set; }

        public RuntimeCleanupBridgeCommandService()
            : base("appsettings.json", () => new FakeServiceManager())
        {
        }

        public Task InvokeDisableBridgeRuntimeAsync(string path) => base.DisableBridgeRuntimeAsync(path);

        protected override Task DisableBridgeRuntimeDirectAsync(IConfiguration config)
        {
            DirectCleanupInvoked = true;
            return Task.CompletedTask;
        }
    }

    private sealed class DirectCleanupBridgeCommandService : BridgeCommandService
    {
        public DirectCleanupBridgeCommandService()
            : base("appsettings.json", () => new FakeServiceManager())
        {
        }

        public Task InvokeDisableBridgeRuntimeDirectAsync(IConfiguration config) => base.DisableBridgeRuntimeDirectAsync(config);
    }

    private sealed class FakeServiceManager : IServiceManager
    {
        public Task<ServiceResult> InstallAsync(CancellationToken ct = default) =>
            Task.FromResult(new ServiceResult(true, "ok"));

        public Task<ServiceResult> UninstallAsync(CancellationToken ct = default) =>
            Task.FromResult(new ServiceResult(true, "ok"));

        public Task<ServiceResult> StartAsync(CancellationToken ct = default) =>
            Task.FromResult(new ServiceResult(true, "ok"));

        public Task<ServiceResult> StopAsync(CancellationToken ct = default) =>
            Task.FromResult(new ServiceResult(true, "ok"));

        public Task<ServiceStatus> GetStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(new ServiceStatus(ServiceState.Stopped, null, null, null));

        public Task<ServiceResult> ShowLogsAsync(int lines = 50, CancellationToken ct = default) =>
            Task.FromResult(new ServiceResult(true, "ok"));
    }

    private sealed class RecordingServiceManager : IServiceManager
    {
        public int GetStatusCalls { get; private set; }
        public int StopCalls { get; private set; }
        public int StartCalls { get; private set; }

        public ServiceStatus StatusToReturn { get; set; } = new(ServiceState.Stopped, null, null, null);
        public ServiceResult StopToReturn { get; set; } = new(true, "ok");
        public ServiceResult StartToReturn { get; set; } = new(true, "ok");

        public Task<ServiceResult> InstallAsync(CancellationToken ct = default) =>
            Task.FromResult(new ServiceResult(true, "ok"));

        public Task<ServiceResult> UninstallAsync(CancellationToken ct = default) =>
            Task.FromResult(new ServiceResult(true, "ok"));

        public Task<ServiceResult> StartAsync(CancellationToken ct = default)
        {
            StartCalls++;
            return Task.FromResult(StartToReturn);
        }

        public Task<ServiceResult> StopAsync(CancellationToken ct = default)
        {
            StopCalls++;
            return Task.FromResult(StopToReturn);
        }

        public Task<ServiceStatus> GetStatusAsync(CancellationToken ct = default)
        {
            GetStatusCalls++;
            return Task.FromResult(StatusToReturn);
        }

        public Task<ServiceResult> ShowLogsAsync(int lines = 50, CancellationToken ct = default) =>
            Task.FromResult(new ServiceResult(true, "ok"));
    }

    private sealed class RuntimeCleanupServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serveTask;

        public RuntimeCleanupServer(HttpStatusCode statusCode)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serveTask = ServeAsync(statusCode);
        }

        public int Port { get; }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Dispose();
            try
            {
                await _serveTask;
            }
            catch
            {
                // The test only needs the branch to have been exercised.
            }
        }

        private async Task ServeAsync(HttpStatusCode statusCode)
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var buffer = new byte[1024];
            var request = new StringBuilder();

            while (request.Length < 64_000)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                if (read == 0)
                    break;

                request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                if (request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                    break;
            }

            var reason = statusCode == HttpStatusCode.OK ? "OK" : "Internal Server Error";
            var response = $"HTTP/1.1 {(int)statusCode} {reason}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(response);
            await stream.WriteAsync(bytes);
            await stream.FlushAsync();
        }
    }

    private static void WriteTestOpenClawIdentity(string stateDir)
    {
        var identityDir = Path.Combine(stateDir, "identity");
        Directory.CreateDirectory(identityDir);

        Span<byte> privateRaw = stackalloc byte[32];
        Span<byte> publicRaw = stackalloc byte[32];
        RandomNumberGenerator.Fill(privateRaw);
        RandomNumberGenerator.Fill(publicRaw);

        var privatePkcs8Like = new byte[48];
        privateRaw.CopyTo(privatePkcs8Like.AsSpan(privatePkcs8Like.Length - 32));
        var publicSpkiLike = new byte[44];
        publicRaw.CopyTo(publicSpkiLike.AsSpan(publicSpkiLike.Length - 32));

        var privatePem = ToPem("PRIVATE KEY", privatePkcs8Like);
        var publicPem = ToPem("PUBLIC KEY", publicSpkiLike);

        File.WriteAllText(Path.Combine(identityDir, "device.json"), $$"""
            {
              "deviceId": "test-device",
              "publicKeyPem": {{System.Text.Json.JsonSerializer.Serialize(publicPem)}},
              "privateKeyPem": {{System.Text.Json.JsonSerializer.Serialize(privatePem)}}
            }
            """);

        File.WriteAllText(Path.Combine(identityDir, "device-auth.json"), """
            {
              "tokens": {
                "operator": {
                  "token": "test-device-token"
                }
              }
            }
            """);
    }

    private static string ToPem(string label, byte[] data)
    {
        var b64 = Convert.ToBase64String(data);
        return $"-----BEGIN {label}-----\n{b64}\n-----END {label}-----";
    }

    private sealed class FakeOpenClawRpcServer : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;
        private string _hash = "h0";

        public FakeOpenClawRpcServer(string configJson, IReadOnlyList<string> sessions)
        {
            var port = GetEphemeralPort();
            BaseHttpUrl = $"http://127.0.0.1:{port}/";
            WebSocketUrl = $"ws://127.0.0.1:{port}/ws/";
            _listener.Prefixes.Add(BaseHttpUrl);
            _listener.Start();

            CurrentConfig = JsonNode.Parse(configJson)!.AsObject();
            Sessions = sessions.ToList();
            _serverTask = Task.Run(ServerLoopAsync);
        }

        public string BaseHttpUrl { get; }
        public string WebSocketUrl { get; }
        public JsonObject CurrentConfig { get; private set; }
        public List<string> Sessions { get; }
        public List<string> DeletedSessionKeys { get; } = [];
        public List<string> ResetSessionKeys { get; } = [];

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _listener.Stop();
            _listener.Close();
            try
            {
                await _serverTask.ConfigureAwait(false);
            }
            catch
            {
                // Best effort test server shutdown.
            }
            _cts.Dispose();
        }

        private async Task ServerLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext? ctx = null;
                try
                {
                    ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    break;
                }

                if (ctx is null)
                    continue;

                if (ctx.Request.IsWebSocketRequest && string.Equals(ctx.Request.Url?.AbsolutePath, "/ws/", StringComparison.Ordinal))
                {
                    var wsCtx = await ctx.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
                    await HandleWebSocketAsync(wsCtx.WebSocket).ConfigureAwait(false);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    ctx.Response.Close();
                }
            }
        }

        private async Task HandleWebSocketAsync(WebSocket socket)
        {
            await SendEventAsync(socket, "connect.challenge", new { nonce = "nonce-1" }).ConfigureAwait(false);

            while (socket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                var text = await ReceiveTextAsync(socket).ConfigureAwait(false);
                if (text is null)
                    break;

                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var type) || !string.Equals(type.GetString(), "req", StringComparison.Ordinal))
                    continue;

                var id = root.GetProperty("id").GetString() ?? "unknown";
                var method = root.GetProperty("method").GetString() ?? string.Empty;
                var @params = root.TryGetProperty("params", out var p) ? p : default;

                switch (method)
                {
                    case "connect":
                        await SendResponseAsync(socket, id, ok: true, payloadJson: "{}").ConfigureAwait(false);
                        break;
                    case "chat.history":
                        await SendResponseAsync(socket, id, ok: true, payloadJson: """{"items":[]}""").ConfigureAwait(false);
                        break;
                    case "config.get":
                        var configRaw = CurrentConfig.ToJsonString();
                        await SendResponseAsync(
                            socket,
                            id,
                            ok: true,
                            payloadJson: $$"""{"raw":{{System.Text.Json.JsonSerializer.Serialize(configRaw)}},"hash":{{System.Text.Json.JsonSerializer.Serialize(_hash)}}}"""
                        ).ConfigureAwait(false);
                        break;
                    case "config.set":
                        var raw = @params.GetProperty("raw").GetString() ?? "{}";
                        CurrentConfig = JsonNode.Parse(raw)!.AsObject();
                        _hash = "h" + Guid.NewGuid().ToString("N");
                        await SendResponseAsync(socket, id, ok: true, payloadJson: """{}""").ConfigureAwait(false);
                        break;
                    case "sessions.list":
                        var sessionsJson = System.Text.Json.JsonSerializer.Serialize(
                            Sessions.Select(key => new { key }));
                        await SendResponseAsync(socket, id, ok: true, payloadJson: $$"""{"sessions":{{sessionsJson}}}""").ConfigureAwait(false);
                        break;
                    case "sessions.delete":
                        var key = @params.GetProperty("key").GetString();
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            if (key.EndsWith(":main", StringComparison.OrdinalIgnoreCase))
                            {
                                await SendResponseAsync(
                                    socket,
                                    id,
                                    ok: false,
                                    payloadJson: null,
                                    errorJson: """{"message":"Cannot delete the main session."}"""
                                ).ConfigureAwait(false);
                                break;
                            }

                            DeletedSessionKeys.Add(key);
                            Sessions.RemoveAll(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
                        }
                        await SendResponseAsync(socket, id, ok: true, payloadJson: """{}""").ConfigureAwait(false);
                        break;
                    case "sessions.reset":
                        var resetKey = @params.TryGetProperty("key", out var keyParam)
                            ? keyParam.GetString()
                            : @params.TryGetProperty("sessionKey", out var sessionKeyParam)
                                ? sessionKeyParam.GetString()
                                : @params.TryGetProperty("id", out var idParam)
                                    ? idParam.GetString()
                                    : null;
                        if (!string.IsNullOrWhiteSpace(resetKey))
                            ResetSessionKeys.Add(resetKey);
                        await SendResponseAsync(socket, id, ok: true, payloadJson: """{}""").ConfigureAwait(false);
                        break;
                    default:
                        await SendResponseAsync(
                            socket,
                            id,
                            ok: false,
                            payloadJson: null,
                            errorJson: $$"""{"message":"unsupported method: {{method}}"}"""
                        ).ConfigureAwait(false);
                        break;
                }
            }

            if (socket.State == WebSocketState.Open)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None).ConfigureAwait(false);
            socket.Dispose();
        }

        private static async Task SendEventAsync(WebSocket socket, string eventName, object payload)
        {
            var json = $$"""{"type":"event","event":{{System.Text.Json.JsonSerializer.Serialize(eventName)}},"payload":{{System.Text.Json.JsonSerializer.Serialize(payload)}}}""";
            await SendTextAsync(socket, json).ConfigureAwait(false);
        }

        private static async Task SendResponseAsync(
            WebSocket socket,
            string id,
            bool ok,
            string? payloadJson = null,
            string? errorJson = null)
        {
            string json;
            if (ok)
                json = $$"""{"type":"res","id":{{System.Text.Json.JsonSerializer.Serialize(id)}},"ok":true,"payload":{{payloadJson ?? "{}"}}}""";
            else
                json = $$"""{"type":"res","id":{{System.Text.Json.JsonSerializer.Serialize(id)}},"ok":false,"error":{{errorJson ?? """{"message":"error"}"""}}}""";

            await SendTextAsync(socket, json).ConfigureAwait(false);
        }

        private static async Task SendTextAsync(WebSocket socket, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None).ConfigureAwait(false);
        }

        private static async Task<string?> ReceiveTextAsync(WebSocket socket)
        {
            var buffer = new byte[16 * 1024];
            using var ms = new MemoryStream();

            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    return null;

                await ms.WriteAsync(buffer.AsMemory(0, result.Count), CancellationToken.None).ConfigureAwait(false);
                if (result.EndOfMessage)
                    break;
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static int GetEphemeralPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
