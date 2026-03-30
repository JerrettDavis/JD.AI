using System.CommandLine;
using System.Diagnostics;
using System.Security.Principal;
using JD.AI.Channels.OpenClaw;
using JD.AI.Channels.OpenClaw.Routing;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using JD.AI.Core.Config;
using JD.AI.Core.Events;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Installation;
using JD.AI.Core.Mcp;
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
using JD.AI.Workflows;

var rootCommand = new RootCommand("JD.AI Gateway Daemon — run as a system service with auto-updates");
var serviceElevatedOption = new Option<bool>("--elevated")
{
    Description = "Internal flag used after UAC/sudo relaunch.",
    Hidden = true,
};

// ── run (default) ──────────────────────────────────────────────────
var runCommand = new Command("run", "Start the daemon (default when no subcommand is given)");
runCommand.SetAction(_ => RunDaemon(args));
rootCommand.Subcommands.Add(runCommand);

// Make "run" the default action when no subcommand is given
rootCommand.SetAction(_ => RunDaemon(args));

// ── install ────────────────────────────────────────────────────────
var installCommand = new Command("install", "Install as a Windows Service or systemd unit");
installCommand.Options.Add(serviceElevatedOption);
installCommand.SetAction(async parseResult =>
{
    var privilegeExitCode = EnsureServicePrivilegesForAction("install", parseResult.GetValue(serviceElevatedOption));
    if (privilegeExitCode.HasValue)
        return privilegeExitCode.Value;

    var mgr = CreateServiceManager();
    var result = await mgr.InstallAsync();
    Console.WriteLine(result.Message);
    return result.Success ? 0 : 1;
});
rootCommand.Subcommands.Add(installCommand);

// ── uninstall ──────────────────────────────────────────────────────
var uninstallCommand = new Command("uninstall", "Remove the system service");
uninstallCommand.Options.Add(serviceElevatedOption);
uninstallCommand.SetAction(async parseResult =>
{
    var privilegeExitCode = EnsureServicePrivilegesForAction("uninstall", parseResult.GetValue(serviceElevatedOption));
    if (privilegeExitCode.HasValue)
        return privilegeExitCode.Value;

    var mgr = CreateServiceManager();
    var result = await mgr.UninstallAsync();
    Console.WriteLine(result.Message);
    return result.Success ? 0 : 1;
});
rootCommand.Subcommands.Add(uninstallCommand);

// ── start ──────────────────────────────────────────────────────────
var startCommand = new Command("start", "Start the installed service");
startCommand.Options.Add(serviceElevatedOption);
startCommand.SetAction(async parseResult =>
{
    var privilegeExitCode = EnsureServicePrivilegesForAction("start", parseResult.GetValue(serviceElevatedOption));
    if (privilegeExitCode.HasValue)
        return privilegeExitCode.Value;

    var mgr = CreateServiceManager();
    var result = await mgr.StartAsync();
    Console.WriteLine(result.Message);
    return result.Success ? 0 : 1;
});
rootCommand.Subcommands.Add(startCommand);

// ── stop ───────────────────────────────────────────────────────────
var stopCommand = new Command("stop", "Stop the running service");
stopCommand.Options.Add(serviceElevatedOption);
stopCommand.SetAction(async parseResult =>
{
    var privilegeExitCode = EnsureServicePrivilegesForAction("stop", parseResult.GetValue(serviceElevatedOption));
    if (privilegeExitCode.HasValue)
        return privilegeExitCode.Value;

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
var elevatedOption = new Option<bool>("--elevated")
{
    Description = "Internal flag used after UAC/sudo relaunch.",
    Hidden = true,
};
updateCommand.Options.Add(checkOnlyOption);
updateCommand.Options.Add(elevatedOption);
updateCommand.SetAction(async parseResult =>
{
    var checkOnly = parseResult.GetValue(checkOnlyOption);
    var elevated = parseResult.GetValue(elevatedOption);
    return await RunUpdateCommandAsync(checkOnly, elevated);
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
var bridgeElevatedOption = new Option<bool>("--elevated")
{
    Description = "Internal flag used after UAC/sudo relaunch.",
    Hidden = true,
};
bridgeCommand.Arguments.Add(bridgeActionArg);
bridgeCommand.Options.Add(bridgeElevatedOption);
bridgeCommand.SetAction(async parseResult =>
{
    var action = parseResult.GetValue(bridgeActionArg);
    var elevated = parseResult.GetValue(bridgeElevatedOption);
    return await HandleBridgeCommandAsync(action, elevated);
});
rootCommand.Subcommands.Add(bridgeCommand);

var dashboardCommand = new Command("dashboard", "Open the dashboard in the default browser");
dashboardCommand.SetAction(async _ =>
{
    var port = GatewayRuntimeDefaults.DefaultPort;

    // Try multiple addresses — some Windows configs resolve 'localhost' to IPv6
    // which may not be bound when Kestrel listens on 0.0.0.0
    string[] candidates = [$"http://127.0.0.1:{port}", $"http://localhost:{port}"];
    string? reachableUrl = null;

    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    foreach (var candidate in candidates)
    {
        try
        {
            var response = await client.GetAsync(new Uri($"{candidate}{GatewayRuntimeDefaults.HealthPath}"));
            if (response.IsSuccessStatusCode)
            {
                reachableUrl = candidate;
                break;
            }
        }
        catch
        {
            // Try next candidate
        }
    }

    if (reachableUrl == null)
    {
        Console.Error.WriteLine($"Cannot reach gateway on port {port}.");
        Console.Error.WriteLine($"Is the daemon running? Check with: {DaemonServiceIdentity.ToolCommand} status");
        Console.Error.WriteLine($"Start with: {DaemonServiceIdentity.ToolCommand} run");
        return 1;
    }

    Console.WriteLine($"Opening dashboard at {reachableUrl}/ ...");
    if (OperatingSystem.IsWindows())
        Process.Start(new ProcessStartInfo("cmd", $"/c start {reachableUrl}/") { CreateNoWindow = true });
    else if (OperatingSystem.IsLinux())
        Process.Start("xdg-open", $"{reachableUrl}/");
    else if (OperatingSystem.IsMacOS())
        Process.Start("open", $"{reachableUrl}/");

    return 0;
});
rootCommand.Subcommands.Add(dashboardCommand);

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
    builder.Services.AddSingleton<McpManager>();
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

    // --- Workflow services ---
    builder.Services.AddSingleton<IWorkflowCatalog>(_ =>
        new FileWorkflowCatalog(Path.Combine(DataDirectories.Root, "workflows")));
    builder.Services.AddSingleton<IWorkflowBridge, WorkflowBridge>();
    builder.Services.AddSingleton<IPromptIntentClassifier, TfIdfIntentClassifier>();
    builder.Services.AddSingleton<IWorkflowMatcher, WorkflowMatcher>();
    builder.Services.AddSingleton<IWorkflowOrchestrator, WorkflowOrchestrator>();

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

    // --- Set daemon version metadata on agent pool for identity enrichment ---
    var agentPool = app.Services.GetRequiredService<AgentPoolService>();
    var updateChecker = app.Services.GetRequiredService<UpdateChecker>();
    agentPool.DaemonVersion = updateChecker.CurrentVersion.ToString();
    _ = Task.Run(async () =>
    {
        try
        {
            var update = await updateChecker.CheckForUpdateAsync();
            agentPool.LatestDaemonVersion = update?.LatestVersion.ToString()
                ?? updateChecker.CurrentVersion.ToString();
        }
        catch { agentPool.LatestDaemonVersion = "check failed"; }
    });

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
    app.MapWorkflowEndpoints();

    // --- SignalR hubs ---
    app.MapHub<AgentHub>("/hubs/agent");
    app.MapHub<EventHub>("/hubs/events");

    // --- Dashboard fallback (SPA routing) ---
    app.MapFallbackToFile("index.html");

    app.Run();
}

static async Task<int> RunUpdateCommandAsync(bool checkOnly, bool elevatedAttempt)
{
    IServiceManager? serviceManager = null;
    var shouldReconcileService = false;
    var serviceWasRunning = false;
    try
    {
        serviceManager = CreateServiceManager();
        var status = await serviceManager.GetStatusAsync();
        shouldReconcileService = status.State != ServiceState.NotInstalled;
        serviceWasRunning = status.State is ServiceState.Running or ServiceState.Starting;
    }
    catch (PlatformNotSupportedException)
    {
        // Update checks are supported on all platforms, but service reconciliation is Windows/Linux only.
    }

    var needsServiceControl = shouldReconcileService && !checkOnly;
    if (needsServiceControl)
    {
        if (OperatingSystem.IsWindows() && !IsWindowsElevated())
        {
            var updateArguments = checkOnly ? "update --check-only --elevated" : "update --elevated";
            if (!elevatedAttempt && TryRelaunchElevatedWithArgs(updateArguments))
                return 0;

            Console.WriteLine("✗ Admin rights are required to stop/start the daemon service during update.");
            Console.WriteLine($"  Re-run from an elevated terminal: {DaemonServiceIdentity.ToolCommand} update");
            return 1;
        }

        if (OperatingSystem.IsLinux() && !IsRunningAsRoot())
        {
            Console.WriteLine("✗ Root privileges are required to stop/start the daemon service during update.");
            Console.WriteLine($"  Re-run with sudo: sudo {DaemonServiceIdentity.ToolCommand} update");
            return 1;
        }
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
        return 0;
    }

    Console.WriteLine($"Update available: {update}");

    if (checkOnly)
    {
        Console.WriteLine($"Run '{DaemonServiceIdentity.ToolCommand} update' (without --check-only) to apply.");
        return 0;
    }

    if (serviceWasRunning && serviceManager is not null)
    {
        Console.WriteLine("Stopping daemon service to release locked tool files...");
        var stopResult = await serviceManager.StopAsync();
        if (!stopResult.Success)
        {
            Console.WriteLine($"✗ Failed to stop service before update: {stopResult.Message}");
            return 1;
        }
    }

    var packageId = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpdateConfig>>().Value.PackageId;
    Console.WriteLine("Applying update...");

    if (OperatingSystem.IsWindows())
    {
        var detached = DetachedUpdater.Launch(
            packageId,
            targetVersion: update.LatestVersion.ToString(),
            visibleWindow: true,
            pauseOnExit: true);

        if (!detached.Success)
        {
            Console.WriteLine($"✗ Failed to launch updater: {detached.Output}");

            if (serviceWasRunning && serviceManager is not null)
            {
                Console.WriteLine("Attempting to restart daemon service after failed updater launch...");
                var restartResult = await serviceManager.StartAsync();
                if (!restartResult.Success)
                    Console.WriteLine($"Warning: failed to restart service: {restartResult.Message}");
            }

            return 1;
        }

        Console.WriteLine(detached.Output);
        if (serviceWasRunning)
            Console.WriteLine("After the updater completes, run 'jdai-daemon start' if the service does not auto-start.");
        return 0;
    }

    Console.WriteLine("Running in-process update via 'dotnet tool update'...");
    var updateResult = await JD.AI.Core.Infrastructure.ProcessExecutor.RunAsync(
        "dotnet", $"tool update -g {packageId}",
        timeout: TimeSpan.FromSeconds(120)).ConfigureAwait(false);

    if (!updateResult.Success)
    {
        var errorText = string.IsNullOrWhiteSpace(updateResult.StandardError)
            ? updateResult.StandardOutput
            : updateResult.StandardError;
        Console.WriteLine($"✗ Update failed: {errorText}");

        if (serviceWasRunning && serviceManager is not null)
        {
            Console.WriteLine("Attempting to restart daemon service after failed update...");
            var restartResult = await serviceManager.StartAsync();
            if (!restartResult.Success)
                Console.WriteLine($"Warning: failed to restart service: {restartResult.Message}");
        }

        if (errorText.Contains("Access to the path", StringComparison.OrdinalIgnoreCase) ||
            errorText.Contains("is denied", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Hint: this usually means the daemon/service still has the tool package locked.");
            Console.WriteLine("      Ensure the service is stopped and rerun from an elevated shell.");
        }

        return 1;
    }

    if (shouldReconcileService && serviceManager is not null)
    {
        var reconcileResult = await serviceManager.InstallAsync();
        if (reconcileResult.Success)
            Console.WriteLine("Service configuration refreshed.");
        else
            Console.WriteLine($"Warning: package updated, but failed to refresh service/task config: {reconcileResult.Message}");
    }

    if (serviceWasRunning && serviceManager is not null)
    {
        Console.WriteLine("Starting daemon service...");
        var startResult = await serviceManager.StartAsync();
        if (!startResult.Success)
            Console.WriteLine($"Warning: update succeeded, but service restart failed: {startResult.Message}");
    }

    Console.WriteLine($"✓ Updated to {update.LatestVersion}.");
    return 0;
}

static bool IsWindowsElevated()
{
    if (!OperatingSystem.IsWindows())
        return true;

    try
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    catch
    {
        return false;
    }
}

static bool IsRunningAsRoot()
{
    if (!OperatingSystem.IsLinux())
        return true;

    return string.Equals(Environment.UserName, "root", StringComparison.Ordinal);
}

static bool TryRelaunchElevatedWithArgs(string arguments)
{
    if (!OperatingSystem.IsWindows())
        return false;

    try
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
            return false;

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas",
        };

        Process.Start(psi);
        Console.WriteLine("Relaunched with elevation prompt. Continuing in elevated process...");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Unable to relaunch elevated: {ex.Message}");
        return false;
    }
}

static int? EnsureServicePrivilegesForAction(string action, bool elevatedAttempt)
{
    if (OperatingSystem.IsWindows() && !IsWindowsElevated())
    {
        var cmdArgs = $"{action} --elevated";
        if (!elevatedAttempt && TryRelaunchElevatedWithArgs(cmdArgs))
            return 0;

        Console.WriteLine($"✗ Admin rights are required for '{DaemonServiceIdentity.ToolCommand} {action}'.");
        Console.WriteLine($"  Re-run from an elevated terminal: {DaemonServiceIdentity.ToolCommand} {action}");
        return 1;
    }

    if (OperatingSystem.IsLinux() && !IsRunningAsRoot())
    {
        Console.WriteLine($"✗ Root privileges are required for '{DaemonServiceIdentity.ToolCommand} {action}'.");
        Console.WriteLine($"  Re-run with sudo: sudo {DaemonServiceIdentity.ToolCommand} {action}");
        return 1;
    }

    return null;
}

static async Task<int> HandleBridgeCommandAsync(string? action, bool elevatedAttempt)
{
    var normalizedAction = (action ?? "status").Trim().ToLowerInvariant();
    var needsServiceControl = normalizedAction is "enable" or "disable" or "passthrough";

    if (needsServiceControl)
    {
        if (OperatingSystem.IsWindows() && !IsWindowsElevated())
        {
            var bridgeArguments = string.IsNullOrWhiteSpace(action)
                ? "bridge --elevated"
                : $"bridge {action.Trim()} --elevated";
            if (!elevatedAttempt && TryRelaunchElevatedWithArgs(bridgeArguments))
                return 0;

            Console.WriteLine("✗ Admin rights are required for bridge task/service operations.");
            Console.WriteLine($"  Re-run from an elevated terminal: {DaemonServiceIdentity.ToolCommand} bridge {normalizedAction}");
            return 1;
        }

        if (OperatingSystem.IsLinux() && !IsRunningAsRoot())
        {
            Console.WriteLine("✗ Root privileges are required for bridge task/service operations.");
            Console.WriteLine($"  Re-run with sudo: sudo {DaemonServiceIdentity.ToolCommand} bridge {normalizedAction}");
            return 1;
        }
    }

    var service = new BridgeCommandService(ResolveDaemonAppSettingsPath(), CreateServiceManager);
    return await service.ExecuteAsync(action).ConfigureAwait(false);
}

static string ResolveDaemonAppSettingsPath()
{
    var assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
    return Path.Combine(assemblyDir, "appsettings.json");
}
