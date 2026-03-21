namespace JD.AI.Daemon.Tests.Services;

public sealed class BridgeCommandServiceTests
{
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
}
