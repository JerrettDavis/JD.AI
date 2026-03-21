using JD.AI.Daemon.Services;

namespace JD.AI.Tests.Daemon.Services;

public sealed class BridgeCommandServiceTests
{
    [Theory]
    [InlineData(null)]
    public async Task ExecuteAsync_DefaultAction_TreatsNullAsStatus(string? action)
    {
        var sut = new TestBridgeCommandService();

        var code = await sut.ExecuteAsync(action);

        Assert.Equal(0, code);
        Assert.Equal(["read", "write"], sut.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_Status_ReadsAndWritesState()
    {
        var sut = new TestBridgeCommandService();

        var code = await sut.ExecuteAsync("status");

        Assert.Equal(0, code);
        Assert.Equal(["read", "write"], sut.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_Enable_ExecutesExpectedSequence()
    {
        var sut = new TestBridgeCommandService();

        var code = await sut.ExecuteAsync("enable");

        Assert.Equal(0, code);
        Assert.Equal(["set:True", "tasks:True", "restart", "write"], sut.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_Disable_ExecutesExpectedSequence()
    {
        var sut = new TestBridgeCommandService();

        var code = await sut.ExecuteAsync("disable");

        Assert.Equal(0, code);
        Assert.Equal(["runtime-clean", "set:False", "tasks:False", "stop-task", "restart", "write"], sut.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_Passthrough_ExecutesExpectedSequence()
    {
        var sut = new TestBridgeCommandService();

        var code = await sut.ExecuteAsync("passthrough");

        Assert.Equal(0, code);
        Assert.Equal(["passthrough", "tasks:True", "restart", "write"], sut.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAction_ReturnsUsageError()
    {
        var sut = new TestBridgeCommandService();

        var code = await sut.ExecuteAsync("bogus");

        Assert.Equal(1, code);
        Assert.Empty(sut.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_Exception_ReturnsError()
    {
        var sut = new TestBridgeCommandService
        {
            ThrowOnSetEnabled = true
        };

        var code = await sut.ExecuteAsync("enable");

        Assert.Equal(1, code);
        Assert.Equal(["set:True"], sut.Calls);
    }

    [Fact]
    public void BuildManagedSessionFilters_IncludesRegisteredAgentsAndChannels()
    {
        var config = new JD.AI.Gateway.Config.OpenClawGatewayConfig
        {
            RegisterAgents =
            [
                new JD.AI.Gateway.Config.OpenClawAgentRegistration
                {
                    Id = "jdai-default",
                    Bindings =
                    [
                        new JD.AI.Gateway.Config.OpenClawBindingConfig { Channel = "signal" }
                    ]
                }
            ],
            Channels = new Dictionary<string, JD.AI.Gateway.Config.OpenClawChannelConfig>(StringComparer.Ordinal)
            {
                ["discord"] = new JD.AI.Gateway.Config.OpenClawChannelConfig()
            }
        };

        var (prefixes, contains) = BridgeCommandService.BuildManagedSessionFilters(config);

        Assert.Contains("agent:jdai-", prefixes);
        Assert.Contains("agent:jdai-default:", prefixes);
        Assert.Contains("g-agent-", contains);
        Assert.Contains("jdai-default", contains);
        Assert.Contains("signal:g-agent-", contains);
        Assert.Contains("discord:g-agent-", contains);
    }

    [Fact]
    public async Task RestartInstalledServiceAsync_WhenRunning_StopsThenStarts()
    {
        var manager = new CountingServiceManager
        {
            StatusToReturn = new ServiceStatus(ServiceState.Running, null, null, null),
            StopToReturn = new ServiceResult(true, "stopped"),
            StartToReturn = new ServiceResult(true, "started")
        };
        var sut = new DefaultBehaviorBridgeCommandService(() => manager);

        await sut.InvokeRestartInstalledServiceAsync();

        Assert.Equal(1, manager.GetStatusCalls);
        Assert.Equal(1, manager.StopCalls);
        Assert.Equal(1, manager.StartCalls);
    }

    [Fact]
    public async Task RestartInstalledServiceAsync_WhenNotRunning_DoesNotStartOrStop()
    {
        var manager = new CountingServiceManager
        {
            StatusToReturn = new ServiceStatus(ServiceState.Stopped, null, null, null)
        };
        var sut = new DefaultBehaviorBridgeCommandService(() => manager);

        await sut.InvokeRestartInstalledServiceAsync();

        Assert.Equal(1, manager.GetStatusCalls);
        Assert.Equal(0, manager.StopCalls);
        Assert.Equal(0, manager.StartCalls);
    }

    [Fact]
    public void WriteStatus_WritesEffectiveModeAndOverrides()
    {
        var sut = new DefaultBehaviorBridgeCommandService(() => new CountingServiceManager());
        var state = new OpenClawBridgeState(
            Enabled: true,
            AutoConnect: true,
            DefaultMode: "Passthrough",
            OverrideActive: true,
            OverrideChannels: ["signal"]);

        using var writer = new StringWriter();
        var prevOut = Console.Out;
        try
        {
            Console.SetOut(writer);
            sut.InvokeWriteStatus(state, "appsettings.json");
        }
        finally
        {
            Console.SetOut(prevOut);
        }

        var text = writer.ToString();
        Assert.Contains("Config:          appsettings.json", text, StringComparison.Ordinal);
        Assert.Contains("Effective mode:  Override active", text, StringComparison.Ordinal);
        Assert.Contains("Override chans:  signal", text, StringComparison.Ordinal);
    }

    private sealed class TestBridgeCommandService : BridgeCommandService
    {
        public bool ThrowOnSetEnabled { get; set; }
        public List<string> Calls { get; } = [];

        public TestBridgeCommandService()
            : base("test-appsettings.json", () => new FakeServiceManager())
        {
        }

        protected override OpenClawBridgeState ReadState(string appSettingsPath)
        {
            Calls.Add("read");
            return NewState(enabled: false);
        }

        protected override OpenClawBridgeState SetEnabled(string appSettingsPath, bool enabled)
        {
            Calls.Add($"set:{enabled}");
            if (ThrowOnSetEnabled)
                throw new InvalidOperationException("boom");

            return NewState(enabled);
        }

        protected override OpenClawBridgeState SetPassthrough(string appSettingsPath)
        {
            Calls.Add("passthrough");
            return NewState(enabled: true, defaultMode: "Passthrough");
        }

        protected override void WriteStatus(OpenClawBridgeState state, string appSettingsPath)
            => Calls.Add("write");

        protected override Task RestartInstalledServiceAsync()
        {
            Calls.Add("restart");
            return Task.CompletedTask;
        }

        protected override Task DisableBridgeRuntimeAsync(string appSettingsPath)
        {
            Calls.Add("runtime-clean");
            return Task.CompletedTask;
        }

        protected override Task SetOpenClawGatewayTasksEnabledAsync(bool enabled)
        {
            Calls.Add($"tasks:{enabled}");
            return Task.CompletedTask;
        }

        protected override Task StopOpenClawGatewayTaskAsync()
        {
            Calls.Add("stop-task");
            return Task.CompletedTask;
        }

        private static OpenClawBridgeState NewState(bool enabled, string defaultMode = "Passthrough")
            => new(enabled, enabled, defaultMode, false, []);
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

    private sealed class CountingServiceManager : IServiceManager
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

    private sealed class DefaultBehaviorBridgeCommandService : BridgeCommandService
    {
        public DefaultBehaviorBridgeCommandService(Func<IServiceManager> managerFactory)
            : base("appsettings.json", managerFactory)
        {
        }

        public Task InvokeRestartInstalledServiceAsync() => base.RestartInstalledServiceAsync();
        public void InvokeWriteStatus(OpenClawBridgeState state, string path) => base.WriteStatus(state, path);
    }
}
