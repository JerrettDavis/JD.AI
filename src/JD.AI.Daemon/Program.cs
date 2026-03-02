using System.CommandLine;
using JD.AI.Channels.OpenClaw;
using JD.AI.Channels.OpenClaw.Routing;
using JD.AI.Gateway.Config;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using JD.AI.Core.Events;
using JD.AI.Core.Memory;
using JD.AI.Core.Plugins;
using JD.AI.Core.Providers;
using JD.AI.Core.Security;
using JD.AI.Core.Sessions;
using JD.AI.Daemon.Config;
using JD.AI.Daemon.Services;
using JD.AI.Gateway.Commands;
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
    var builder = WebApplication.CreateBuilder(args);

    // Ensure Blazor WASM static assets resolve in all environments (not just Development)
    if (!builder.Environment.IsDevelopment())
        builder.WebHost.UseStaticWebAssets();

    // Platform-specific service hosting
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "JD.AI Gateway";
    });
    builder.Services.AddSystemd();

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
    builder.Services.AddSingleton<IRateLimiter>(
        new SlidingWindowRateLimiter(gatewayConfig.RateLimit.MaxRequestsPerMinute));

    // --- Core services ---
    builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
    builder.Services.AddSingleton<IChannelRegistry, ChannelRegistry>();
    builder.Services.AddSingleton<IProviderDetector, ClaudeCodeDetector>();
    builder.Services.AddSingleton<IProviderDetector, CopilotDetector>();
    builder.Services.AddSingleton<IProviderDetector, OllamaDetector>();
    builder.Services.AddSingleton<IProviderRegistry>(sp =>
        new ProviderRegistry(sp.GetServices<IProviderDetector>()));
    builder.Services.AddSingleton<SessionStore>(_ =>
        new SessionStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".jdai", "sessions.db")));
    builder.Services.AddSingleton<AgentPoolService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentPoolService>());

    // --- Plugin loader ---
    builder.Services.AddSingleton<PluginLoader>();
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
        return registry;
    });
    builder.Services.AddSingleton<IVectorStore>(_ =>
        new SqliteVectorStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".jdai", "vectors.db")));

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
    app.UseCors();

    if (gatewayConfig.Auth.Enabled)
        app.UseMiddleware<ApiKeyAuthMiddleware>();

    if (gatewayConfig.RateLimit.Enabled)
        app.UseMiddleware<RateLimitMiddleware>();

    // --- Blazor WASM Dashboard (static files at root) ---
    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    // --- Health ---
    app.MapHealthChecks("/health");
    app.MapGet("/ready", () => Results.Ok(new { Status = "Ready" }));

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
        Console.WriteLine("Run 'jdai-daemon update' (without --check-only) to apply.");
        return;
    }

    Console.WriteLine("Applying update via 'dotnet tool update'...");
    var process = new System.Diagnostics.Process
    {
        StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"tool update -g {host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpdateConfig>>().Value.PackageId}",
            UseShellExecute = false,
        },
    };

    process.Start();
    await process.WaitForExitAsync();

    Console.WriteLine(process.ExitCode == 0
        ? $"✓ Updated to {update.LatestVersion}. Restart the service to apply."
        : "✗ Update failed. Check the output above.");
}
