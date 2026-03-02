using System.CommandLine;
using JD.AI.Gateway.Config;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using JD.AI.Core.Events;
using JD.AI.Core.Providers;
using JD.AI.Core.Sessions;
using JD.AI.Daemon.Config;
using JD.AI.Daemon.Services;
using JD.AI.Gateway.Commands;
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
    var builder = Host.CreateApplicationBuilder(args);

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

    // Core services (same as Gateway)
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

    // Agent routing
    builder.Services.AddSingleton<AgentRouter>();

    // Gateway config
    var gatewayConfig = builder.Configuration.GetSection("Gateway").Get<GatewayConfig>() ?? new GatewayConfig();
    builder.Services.AddSingleton(gatewayConfig);

    // Command registry (same commands as Gateway)
    builder.Services.AddSingleton<ICommandRegistry>(sp =>
    {
        var registry = new CommandRegistry();
        var pool = sp.GetRequiredService<AgentPoolService>();
        registry.Register(new HelpCommand(registry));
        registry.Register(new UsageCommand(pool));
        registry.Register(new StatusCommand(pool, sp.GetRequiredService<IChannelRegistry>()));
        registry.Register(new ModelsCommand(pool, gatewayConfig));
        registry.Register(new ClearCommand(pool));
        registry.Register(new AgentsCommand(pool, sp.GetRequiredService<AgentRouter>()));
        return registry;
    });

    // Update services
    builder.Services.AddSingleton<UpdateChecker>();
    builder.Services.AddHostedService<UpdateService>();

    var host = builder.Build();
    host.Run();
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
