using System.CommandLine;
using JD.AI.Channels.OpenClaw;
using JD.AI.Channels.OpenClaw.Routing;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using JD.AI.Core.Config;
using JD.AI.Core.Events;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Memory;
using JD.AI.Core.Plugins;
using JD.AI.Core.Providers;
using JD.AI.Core.Security;
using JD.AI.Core.Sessions;
using JD.AI.Daemon.Config;
using JD.AI.Daemon.Services;
using JD.AI.Gateway.Commands;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Endpoints;
using JD.AI.Gateway.Hubs;
using JD.AI.Gateway.Middleware;
using JD.AI.Gateway.Services;

var rootCommand = new RootCommand("JD.AI Gateway Daemon — run as a system service with auto-updates");

// ── run (default) ──────────────────────────────────────────────────
var runCommand = new Command("run", "Start the daemon (default when no subcommand is given)");
runCommand.SetAction(_ => RunDaemon(args));
rootCommand.Subcommands.Add(runCommand);

// Make "run" the default action when no subcommand is given
rootCommand.SetAction(_ => RunDaemon(args));

// ── install ────────────────────────────────────────────────────────
var installCommand = new Command("install", "Install as a Windows Service or systemd unit");
installCommand.SetAction(async _ =>
{
    var mgr = CreateServiceManager();
    var result = await mgr.InstallAsync();
    Console.WriteLine(result.Message);
    return result.Success ? 0 : 1;
});
rootCommand.Subcommands.Add(installCommand);

// ── uninstall ──────────────────────────────────────────────────────
var uninstallCommand = new Command("uninstall", "Remove the system service");
uninstallCommand.SetAction(async _ =>
{
    var mgr = CreateServiceManager();
    var result = await mgr.UninstallAsync();
    Console.WriteLine(result.Message);
    return result.Success ? 0 : 1;
});
rootCommand.Subcommands.Add(uninstallCommand);

// ── start ──────────────────────────────────────────────────────────
var startCommand = new Command("start", "Start the installed service");
startCommand.SetAction(async _ =>
{
    var mgr = CreateServiceManager();
    var result = await mgr.StartAsync();
    Console.WriteLine(result.Message);
    return result.Success ? 0 : 1;
});
rootCommand.Subcommands.Add(startCommand);

// ── stop ───────────────────────────────────────────────────────────
var stopCommand = new Command("stop", "Stop the running service");
stopCommand.SetAction(async _ =>
{
    var mgr = CreateServiceManager();
    var result = await mgr.StopAsync();
    Console.WriteLine(result.Message);
    return result.Success ? 0 : 1;
});
rootCommand.Subcommands.Add(stopCommand);

// ── status ─────────────────────────────────────────────────────────
var statusCommand = new Command("status", "Show service status, version, and uptime");
statusCommand.SetAction(async _ =>
{
    var mgr = CreateServiceManager();
    var status = await mgr.GetStatusAsync();
    Console.WriteLine($"State:   {status.State}");
    if (status.Version is not null) Console.WriteLine($"Version: {status.Version}");
    if (status.Uptime.HasValue) Console.WriteLine($"Uptime:  {status.Uptime.Value}");
    if (status.Details is not null) Console.WriteLine($"Details: {status.Details}");
});
rootCommand.Subcommands.Add(statusCommand);

// ── update ─────────────────────────────────────────────────────────
var updateCommand = new Command("update", "Check for and apply updates from NuGet");
var checkOnlyOption = new Option<bool>("--check-only") { Description = "Only check — don't apply the update" };
updateCommand.Options.Add(checkOnlyOption);
updateCommand.SetAction(async parseResult =>
{
    var checkOnly = parseResult.GetValue(checkOnlyOption);
    await RunUpdateCommandAsync(checkOnly);
});
rootCommand.Subcommands.Add(updateCommand);

// ── logs ───────────────────────────────────────────────────────────
var logsCommand = new Command("logs", "Show recent service logs");
var linesOption = new Option<int>("--lines", "-n") { Description = "Number of log lines to show", DefaultValueFactory = _ => 50 };
logsCommand.Options.Add(linesOption);
logsCommand.SetAction(async parseResult =>
{
    var lines = parseResult.GetValue(linesOption);
    var mgr = CreateServiceManager();
    var result = await mgr.ShowLogsAsync(lines);
    Console.WriteLine(result.Message);
});
rootCommand.Subcommands.Add(logsCommand);

// ── bridge ─────────────────────────────────────────────────────────
var bridgeCommand = new Command("bridge", "Manage OpenClaw bridge mode and enablement (status|enable|disable|passthrough)");
var bridgeActionArg = new Argument<string>("action")
{
    Description = "Action: status, enable, disable, passthrough",
};
bridgeActionArg.Arity = ArgumentArity.ZeroOrOne;
bridgeCommand.Arguments.Add(bridgeActionArg);
bridgeCommand.SetAction(async parseResult =>
{
    var action = parseResult.GetValue(bridgeActionArg);
    return await HandleBridgeCommandAsync(action);
});
rootCommand.Subcommands.Add(bridgeCommand);

return rootCommand.Parse(args).Invoke();

// ════════════════════════════════════════════════════════════════════
// Helper methods
// ════════════════════════════════════════════════════════════════════

static IServiceManager CreateServiceManager()
{
    if (OperatingSystem.IsWindows())
        return new WindowsServiceManager();
    if (OperatingSystem.IsLinux())
        return new SystemdServiceManager();

    throw new PlatformNotSupportedException(
        "Service management is supported on Windows and Linux only.");
}

static void RunDaemon(string[] args)
{
    // When running as a dotnet global tool, the working directory won't contain
    // appsettings.json or wwwroot. Set content root to the assembly's directory.
    var assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = assemblyDir,
        WebRootPath = Path.Combine(assemblyDir, "wwwroot"),
    });

    // Ensure Blazor WASM static assets resolve in all environments (not just Development)
    if (!builder.Environment.IsDevelopment())
        builder.WebHost.UseStaticWebAssets();

    // Platform-specific service hosting
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = DaemonServiceIdentity.HostedServiceDisplayName;
    });
    builder.Services.AddSystemd();

    // --- Data directory (resolve before other services reference it) ---
    var configuredDataDir = builder.Configuration["DataDir"];
    if (!string.IsNullOrWhiteSpace(configuredDataDir))
        DataDirectories.SetRoot(configuredDataDir);

    var logger = LoggerFactory.Create(lb => lb.AddConsole()).CreateLogger("Startup");
    logger.LogInformation("Data directory: {DataDir}", DataDirectories.Root);

    // Update configuration
    builder.Services.Configure<UpdateConfig>(
        builder.Configuration.GetSection("Updates"));
    builder.Services.AddHttpClient("NuGet");

    // --- Gateway configuration ---
    var gatewayConfig = builder.Configuration.GetSection("Gateway").Get<GatewayConfig>() ?? new GatewayConfig();
    builder.Services.AddSingleton(gatewayConfig);

    // --- Security services ---
    var authProvider = new ApiKeyAuthProvider();
    foreach (var entry in gatewayConfig.Auth.ApiKeys)
    {
        if (Enum.TryParse<GatewayRole>(entry.Role, ignoreCase: true, out var role))
            authProvider.RegisterKey(entry.Key, entry.Name, role);
    }

    builder.Services.AddSingleton<IAuthProvider>(authProvider);
    if (string.Equals(gatewayConfig.RateLimit.Provider, "Redis", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(gatewayConfig.RateLimit.RedisConnectionString))
    {
        builder.Services.AddSingleton<IRateLimiter>(sp =>
            new RedisRateLimiter(
                StackExchange.Redis.ConnectionMultiplexer.Connect(gatewayConfig.RateLimit.RedisConnectionString),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisRateLimiter>>(),
                gatewayConfig.RateLimit.MaxRequestsPerMinute));
    }
    else
    {
        builder.Services.AddSingleton<IRateLimiter>(
            new SlidingWindowRateLimiter(gatewayConfig.RateLimit.MaxRequestsPerMinute));
    }

    // --- Core services ---
    builder.Services.AddEventBus(new EventBusOptions
    {
        Provider = gatewayConfig.EventBus.Provider,
        RedisConnectionString = gatewayConfig.EventBus.RedisConnectionString,
    });
    builder.Services.AddSingleton<IChannelRegistry, ChannelRegistry>();
    builder.Services.AddSingleton<IProviderDetector, ClaudeCodeDetector>();
    builder.Services.AddSingleton<IProviderDetector, CopilotDetector>();
    builder.Services.AddSingleton<IProviderDetector, OllamaDetector>();
    builder.Services.AddSingleton<IProviderRegistry>(sp =>
        new ProviderRegistry(sp.GetServices<IProviderDetector>()));
    builder.Services.AddSingleton<SessionStore>(_ =>
        new SessionStore(DataDirectories.SessionsDb));
    builder.Services.AddSingleton<AgentPoolService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentPoolService>());

    // --- Plugin loader ---
    builder.Services.AddSingleton<PluginLoader>();
    builder.Services.AddSingleton<IPluginRuntime>(sp => sp.GetRequiredService<PluginLoader>());
    builder.Services.AddSingleton<PluginRegistryStore>();
    builder.Services.AddSingleton<IPluginInstaller>(sp =>
        new PluginInstaller(
            new HttpClient(),
            sp.GetRequiredService<ILogger<PluginInstaller>>()));
    builder.Services.AddSingleton<IPluginContextFactory, ServiceProviderPluginContextFactory>();
    builder.Services.AddSingleton<IPluginLifecycleManager, PluginLifecycleManager>();
    builder.Services.AddHostedService<PluginLifecycleHostedService>();
    builder.Services.AddSingleton<AgentRouter>();

    // --- Command system ---
    builder.Services.AddSingleton<ICommandRegistry>(sp =>
    {
        var registry = new CommandRegistry();
        registry.Register(new HelpCommand(registry));
        registry.Register(new UsageCommand(sp.GetRequiredService<AgentPoolService>()));
        registry.Register(new StatusCommand(
            sp.GetRequiredService<AgentPoolService>(),
            sp.GetRequiredService<IChannelRegistry>()));
        registry.Register(new ModelsCommand(
            sp.GetRequiredService<AgentPoolService>(),
            sp.GetRequiredService<GatewayConfig>()));
        registry.Register(new SwitchCommand(sp.GetRequiredService<AgentPoolService>()));
        registry.Register(new ClearCommand(sp.GetRequiredService<AgentPoolService>()));
        registry.Register(new AgentsCommand(
            sp.GetRequiredService<AgentPoolService>(),
            sp.GetRequiredService<AgentRouter>()));
        registry.Register(new RouteCommand(
            sp.GetRequiredService<AgentRouter>(),
            sp.GetRequiredService<AgentPoolService>()));
        registry.Register(new RoutesCommand(
            sp.GetRequiredService<AgentRouter>(),
            sp.GetRequiredService<AgentPoolService>()));
        registry.Register(new ProvidersCommand(
            sp.GetRequiredService<IProviderRegistry>()));
        registry.Register(new ProviderCommand(
            sp.GetRequiredService<AgentRouter>(),
            sp.GetRequiredService<AgentPoolService>(),
            sp.GetRequiredService<IProviderRegistry>()));
        registry.Register(new ConfigCommand(
            sp.GetRequiredService<AgentRouter>(),
            sp.GetRequiredService<AgentPoolService>(),
            sp.GetRequiredService<IChannelRegistry>(),
            sp.GetRequiredService<IProviderRegistry>()));
        return registry;
    });
    builder.Services.AddSingleton<IVectorStore>(_ =>
        new SqliteVectorStore(DataDirectories.VectorsDb));

    // --- Channel factory & orchestrator ---
    builder.Services.AddSingleton<ChannelFactory>();
    builder.Services.AddHostedService<GatewayOrchestrator>();

    // --- OpenClaw bridge (if enabled) ---
    if (gatewayConfig.OpenClaw.Enabled)
    {
        builder.Services.AddOpenClawBridge(config =>
        {
            config.WebSocketUrl = gatewayConfig.OpenClaw.WebSocketUrl;
        });

        builder.Services.AddOpenClawRouting(
            routing =>
            {
                if (Enum.TryParse<OpenClawRoutingMode>(gatewayConfig.OpenClaw.DefaultMode, true, out var defaultMode))
                    routing.DefaultMode = defaultMode;

                foreach (var (channelName, channelConfig) in gatewayConfig.OpenClaw.Channels)
                {
                    var route = new OpenClawChannelRouteConfig();

                    if (Enum.TryParse<OpenClawRoutingMode>(channelConfig.Mode, true, out var mode))
                        route.Mode = mode;

                    route.CommandPrefix = channelConfig.CommandPrefix;
                    route.TriggerPattern = channelConfig.TriggerPattern;

                    if (!string.IsNullOrEmpty(channelConfig.SystemPrompt))
                        route.SystemPrompt = channelConfig.SystemPrompt;

                    if (!string.IsNullOrEmpty(channelConfig.AgentId))
                        route.AgentProfile = channelConfig.AgentId;

                    routing.Channels[channelName] = route;
                }
            },
            messageProcessor: null);

        builder.Services.AddSingleton<Func<string, string, Task<string?>>>(sp =>
        {
            var pool = sp.GetRequiredService<AgentPoolService>();
            var gwConfig = sp.GetRequiredService<GatewayConfig>();

            var agentMapping = gwConfig.OpenClaw.RegisterAgents
                .Where(r => !string.IsNullOrEmpty(r.GatewayAgentId))
                .ToDictionary(r => r.Id, r => r.GatewayAgentId!, StringComparer.OrdinalIgnoreCase);

            return async (sessionKey, content) =>
            {
                var ocAgentId = ExtractAgentIdFromSessionKey(sessionKey);
                string? poolAgentId = null;

                if (ocAgentId is not null && agentMapping.TryGetValue(ocAgentId, out _))
                {
                    var agents = pool.ListAgents();
                    poolAgentId = agents.Count > 0 ? agents[0].Id : null;
                }

                var allAgents = pool.ListAgents();
                poolAgentId ??= allAgents.Count > 0 ? allAgents[0].Id : null;

                if (poolAgentId is null) return null;
                return await pool.SendMessageAsync(poolAgentId, content, CancellationToken.None);
            };

            static string? ExtractAgentIdFromSessionKey(string sessionKey)
            {
                if (!sessionKey.StartsWith("agent:", StringComparison.Ordinal))
                    return null;
                var parts = sessionKey.Split(':', 3);
                return parts.Length >= 2 ? parts[1] : null;
            }
        });

        builder.Services.AddSingleton<OpenClawAgentRegistrar>();
    }

    // --- SignalR ---
    builder.Services.AddSignalR();

    // --- OpenAPI ---
    builder.Services.AddOpenApi();

    // --- Health checks ---
    builder.Services.AddHealthChecks()
        .AddCheck<GatewayHealthCheck>("gateway");

    // --- CORS (allow TUI, web clients, and SignalR WebSockets) ---
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

    // --- Update services ---
    builder.Services.AddSingleton<UpdateChecker>();
    builder.Services.AddHostedService<UpdateService>();

    var app = builder.Build();

    // --- Initialize stores ---
    app.Services.GetRequiredService<SessionStore>().InitializeAsync().GetAwaiter().GetResult();

    // --- Middleware pipeline ---
    // API version rewrite must precede routing so /api/* → /api/v1/* happens first
    app.UseMiddleware<ApiVersionRewriteMiddleware>();
    app.UseRouting();
    app.UseCors();

    if (gatewayConfig.Auth.Enabled)
        app.UseMiddleware<ApiKeyAuthMiddleware>();

    if (gatewayConfig.RateLimit.Enabled)
        app.UseMiddleware<RateLimitMiddleware>();

    // --- Blazor WASM Dashboard (static files at root) ---
    app.MapStaticAssets();

    // --- OpenAPI (available in all environments, protected by auth middleware) ---
    app.MapOpenApi();

    // --- Health ---
    app.MapHealthChecks(GatewayRuntimeDefaults.HealthPath);
    app.MapGet(GatewayRuntimeDefaults.HealthStartupPath, () => Results.Ok(new { Status = "Started" }));
    app.MapGet(GatewayRuntimeDefaults.ReadyPath, () => Results.Ok(new { Status = "Ready" }));

    // --- REST API endpoints ---
    app.MapSessionEndpoints();
    app.MapAgentEndpoints();
    app.MapProviderEndpoints();
    app.MapChannelEndpoints();
    app.MapPluginEndpoints();
    app.MapMemoryEndpoints();
    app.MapRoutingEndpoints();
    app.MapGatewayConfigEndpoints();

    // --- SignalR hubs ---
    app.MapHub<AgentHub>("/hubs/agent");
    app.MapHub<EventHub>("/hubs/events");

    // --- Dashboard fallback (SPA routing) ---
    app.MapFallbackToFile("index.html");

    app.Run();
}

static async Task RunUpdateCommandAsync(bool checkOnly)
{
    IServiceManager? serviceManager = null;
    var shouldReconcileService = false;
    try
    {
        serviceManager = CreateServiceManager();
        var status = await serviceManager.GetStatusAsync();
        shouldReconcileService = status.State != ServiceState.NotInstalled;
    }
    catch (PlatformNotSupportedException)
    {
        // Update checks are supported on all platforms, but service reconciliation is Windows/Linux only.
    }

    // Build a minimal host just for the update checker
    var builder = Host.CreateApplicationBuilder([]);
    builder.Services.Configure<UpdateConfig>(
        builder.Configuration.GetSection("Updates"));
    builder.Services.AddHttpClient("NuGet");
    builder.Services.AddSingleton<UpdateChecker>();

    using var host = builder.Build();
    var checker = host.Services.GetRequiredService<UpdateChecker>();

    Console.WriteLine($"Current version: {checker.CurrentVersion}");
    Console.WriteLine("Checking NuGet for updates...");

    var update = await checker.CheckForUpdateAsync();
    if (update is null)
    {
        Console.WriteLine("✓ Already up-to-date.");
        return;
    }

    Console.WriteLine($"Update available: {update}");

    if (checkOnly)
    {
        Console.WriteLine($"Run '{DaemonServiceIdentity.ToolCommand} update' (without --check-only) to apply.");
        return;
    }

    Console.WriteLine("Applying update via 'dotnet tool update'...");
    var packageId = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpdateConfig>>().Value.PackageId;
    var updateResult = await JD.AI.Core.Infrastructure.ProcessExecutor.RunAsync(
        "dotnet", $"tool update -g {packageId}",
        timeout: TimeSpan.FromSeconds(120)).ConfigureAwait(false);

    if (!updateResult.Success)
    {
        Console.WriteLine($"✗ Update failed: {updateResult.StandardError}");
        return;
    }

    if (shouldReconcileService && serviceManager is not null)
    {
        var reconcileResult = await serviceManager.InstallAsync();
        if (reconcileResult.Success)
            Console.WriteLine("Service configuration refreshed.");
        else
            Console.WriteLine($"Warning: package updated, but failed to refresh service/task config: {reconcileResult.Message}");
    }

    Console.WriteLine($"✓ Updated to {update.LatestVersion}. Restart the service to apply.");
}

static async Task<int> HandleBridgeCommandAsync(string? action)
{
    var normalized = (action ?? "status").Trim().ToLowerInvariant();
    var appSettingsPath = ResolveDaemonAppSettingsPath();

    try
    {
        OpenClawBridgeState state;
        switch (normalized)
        {
            case "status":
                state = OpenClawBridgeConfigEditor.ReadState(appSettingsPath);
                WriteBridgeStatus(state, appSettingsPath);
                return 0;
            case "enable":
                state = OpenClawBridgeConfigEditor.SetEnabled(appSettingsPath, enabled: true);
                await TryRestartInstalledServiceAsync().ConfigureAwait(false);
                WriteBridgeStatus(state, appSettingsPath);
                return 0;
            case "disable":
                await TryDisableBridgeRuntimeAsync(appSettingsPath).ConfigureAwait(false);
                state = OpenClawBridgeConfigEditor.SetEnabled(appSettingsPath, enabled: false);
                await TryRestartInstalledServiceAsync().ConfigureAwait(false);
                WriteBridgeStatus(state, appSettingsPath);
                return 0;
            case "passthrough":
                state = OpenClawBridgeConfigEditor.SetPassthrough(appSettingsPath);
                await TryRestartInstalledServiceAsync().ConfigureAwait(false);
                WriteBridgeStatus(state, appSettingsPath);
                return 0;
            default:
                Console.Error.WriteLine("Usage: jdai-daemon bridge [status|enable|disable|passthrough]");
                return 1;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Bridge command failed: {ex.Message}");
        return 1;
    }
}

static async Task TryRestartInstalledServiceAsync()
{
    try
    {
        var manager = CreateServiceManager();
        var status = await manager.GetStatusAsync().ConfigureAwait(false);
        if (status.State != ServiceState.Running)
            return;

        var stop = await manager.StopAsync().ConfigureAwait(false);
        if (!stop.Success)
        {
            Console.WriteLine("Service restart note: could not stop running service automatically.");
            return;
        }

        var start = await manager.StartAsync().ConfigureAwait(false);
        if (!start.Success)
            Console.WriteLine("Service restart note: service stopped but could not be restarted automatically.");
        else
            Console.WriteLine("Service restart note: running service was restarted to apply bridge changes.");
    }
    catch (PlatformNotSupportedException)
    {
        // Service-manager restart is only available on supported OSes.
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Service restart note: automatic restart failed ({ex.Message}).");
    }
}

static async Task TryDisableBridgeRuntimeAsync(string appSettingsPath)
{
    try
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(appSettingsPath, optional: true, reloadOnChange: false)
            .Build();

        var port = config.GetValue<int?>("Gateway:Server:Port") ?? 15790;
        var disableUrl = new Uri($"http://127.0.0.1:{port}/api/gateway/openclaw/bridge/disable");

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        var response = await client.PostAsync(disableUrl, null).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
            Console.WriteLine("Runtime bridge cleanup note: active OpenClaw sessions were cleaned.");
        else
            Console.WriteLine($"Runtime bridge cleanup note: HTTP {(int)response.StatusCode}; continuing with config disable.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Runtime bridge cleanup note: skipped ({ex.Message}).");
    }
}

static string ResolveDaemonAppSettingsPath()
{
    var assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
    return Path.Combine(assemblyDir, "appsettings.json");
}

static void WriteBridgeStatus(OpenClawBridgeState state, string appSettingsPath)
{
    var mode = state.Enabled
        ? state.OverrideActive ? "Override active" : "Passthrough/observe-only"
        : "Disabled";

    Console.WriteLine($"Config:          {appSettingsPath}");
    Console.WriteLine($"Bridge enabled:  {state.Enabled}");
    Console.WriteLine($"Auto-connect:    {state.AutoConnect}");
    Console.WriteLine($"Default mode:    {state.DefaultMode}");
    Console.WriteLine($"Override active: {state.OverrideActive}");
    Console.WriteLine($"Effective mode:  {mode}");
    if (state.OverrideChannels.Count > 0)
        Console.WriteLine($"Override chans:  {string.Join(", ", state.OverrideChannels)}");
}
