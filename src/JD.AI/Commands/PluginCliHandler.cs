using JD.AI.Core.Plugins;
using JD.AI.Plugins.SDK;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace JD.AI.Commands;

/// <summary>
/// Handles <c>jdai plugin ...</c> CLI lifecycle commands.
/// </summary>
internal static class PluginCliHandler
{
    public static async Task<int> RunAsync(string[] args)
    {
        var sub = args.Length > 0 ? args[0].ToLowerInvariant() : "list";
        var manager = CreateManager();

        try
        {
            return sub switch
            {
                "list" => await ListAsync(manager).ConfigureAwait(false),
                "install" => await InstallAsync(manager, args[1..]).ConfigureAwait(false),
                "enable" => await EnableDisableAsync(manager, args[1..], enabled: true).ConfigureAwait(false),
                "disable" => await EnableDisableAsync(manager, args[1..], enabled: false).ConfigureAwait(false),
                "update" => await UpdateAsync(manager, args[1..]).ConfigureAwait(false),
                "uninstall" or "remove" => await UninstallAsync(manager, args[1..]).ConfigureAwait(false),
                "info" => await InfoAsync(manager, args[1..]).ConfigureAwait(false),
                "help" or "--help" or "-h" => PrintHelp(),
                _ => PrintUnknown(sub),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Plugin command failed: {ex.Message}");
            return 1;
        }
    }

    private static PluginLifecycleManager CreateManager()
    {
        var loader = new PluginLoader(NullLogger<PluginLoader>.Instance);
        var registry = new PluginRegistryStore();
        var installer = new PluginInstaller(
            new HttpClient(),
            NullLogger<PluginInstaller>.Instance);
        var contextFactory = new DelegatePluginContextFactory(
            () => new CliPluginContext(new Kernel()));
        return new PluginLifecycleManager(
            installer,
            registry,
            loader,
            contextFactory,
            NullLogger<PluginLifecycleManager>.Instance);
    }

    private static async Task<int> ListAsync(PluginLifecycleManager manager)
    {
        var plugins = await manager.ListAsync().ConfigureAwait(false);
        if (plugins.Count == 0)
        {
            Console.WriteLine("No plugins installed.");
            return 0;
        }

        Console.WriteLine($"Plugins ({plugins.Count}):");
        foreach (var plugin in plugins)
        {
            var status = plugin.Enabled ? (plugin.Loaded ? "enabled+loaded" : "enabled") : "disabled";
            Console.WriteLine($"  - {plugin.Id} v{plugin.Version} [{status}]");
        }

        return 0;
    }

    private static async Task<int> InstallAsync(PluginLifecycleManager manager, string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: jdai plugin install <path-or-url>");
            return 1;
        }

        var plugin = await manager.InstallAsync(args[0], enable: true).ConfigureAwait(false);
        Console.WriteLine($"Installed {plugin.Id} v{plugin.Version}.");
        return 0;
    }

    private static async Task<int> EnableDisableAsync(
        PluginLifecycleManager manager,
        string[] args,
        bool enabled)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine($"Usage: jdai plugin {(enabled ? "enable" : "disable")} <id>");
            return 1;
        }

        if (enabled)
            await manager.EnableAsync(args[0]).ConfigureAwait(false);
        else
            await manager.DisableAsync(args[0]).ConfigureAwait(false);

        Console.WriteLine($"Plugin '{args[0]}' {(enabled ? "enabled" : "disabled")}.");
        return 0;
    }

    private static async Task<int> UninstallAsync(PluginLifecycleManager manager, string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: jdai plugin uninstall <id>");
            return 1;
        }

        var removed = await manager.UninstallAsync(args[0]).ConfigureAwait(false);
        Console.WriteLine(removed
            ? $"Plugin '{args[0]}' uninstalled."
            : $"Plugin '{args[0]}' is not installed.");
        return removed ? 0 : 1;
    }

    private static async Task<int> UpdateAsync(PluginLifecycleManager manager, string[] args)
    {
        if (args.Length == 0)
        {
            var updated = await manager.UpdateAllAsync().ConfigureAwait(false);
            Console.WriteLine($"Updated {updated.Count} plugin(s).");
            return 0;
        }

        var plugin = await manager.UpdateAsync(args[0]).ConfigureAwait(false);
        Console.WriteLine($"Updated {plugin.Id} to v{plugin.Version}.");
        return 0;
    }

    private static async Task<int> InfoAsync(PluginLifecycleManager manager, string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: jdai plugin info <id>");
            return 1;
        }

        var plugin = await manager.GetAsync(args[0]).ConfigureAwait(false);
        if (plugin is null)
        {
            Console.Error.WriteLine($"Plugin '{args[0]}' not found.");
            return 1;
        }

        Console.WriteLine($"Id:          {plugin.Id}");
        Console.WriteLine($"Name:        {plugin.Name}");
        Console.WriteLine($"Version:     {plugin.Version}");
        Console.WriteLine($"Enabled:     {plugin.Enabled}");
        Console.WriteLine($"Loaded:      {plugin.Loaded}");
        Console.WriteLine($"Source:      {plugin.Source}");
        Console.WriteLine($"InstallPath: {plugin.InstallPath}");
        if (!string.IsNullOrWhiteSpace(plugin.LastError))
            Console.WriteLine($"LastError:   {plugin.LastError}");
        return 0;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            jdai plugin — Manage JD.AI SDK plugins

            Usage: jdai plugin <subcommand> [options]

            Subcommands:
              list                               List installed plugins
              install <path-or-url>             Install and enable a plugin package
              enable <id>                        Enable an installed plugin
              disable <id>                       Disable an installed plugin
              update [id]                        Update one plugin (or all installed plugins)
              uninstall <id>                     Uninstall a plugin
              info <id>                          Show plugin details
            """);
        return 0;
    }

    private static int PrintUnknown(string sub)
    {
        Console.Error.WriteLine($"Unknown plugin subcommand '{sub}'. Run 'jdai plugin --help' for usage.");
        return 1;
    }

    private sealed class CliPluginContext : IPluginContext
    {
        public CliPluginContext(Kernel kernel)
        {
            Kernel = kernel;
        }

        public Kernel Kernel { get; }

        public IReadOnlyDictionary<string, string> Configuration { get; } =
            new Dictionary<string, string>();

        public void OnEvent(string eventType, Func<object?, Task> handler)
        {
        }

        public T? GetService<T>() where T : class => null;

        public void Log(PluginLogLevel level, string message)
        {
        }
    }
}
