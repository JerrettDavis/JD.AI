using JD.AI.Core.Agents;
using JD.AI.Core.Config;

namespace JD.AI.Commands;

/// <summary>
/// Handles <c>jdai agents ...</c> CLI commands for managing versioned agent definitions.
/// </summary>
internal static class AgentsCliHandler
{
    public static async Task<int> RunAsync(string[] args)
    {
        var sub = args.Length > 0 ? args[0].ToLowerInvariant() : "list";
        var remainingArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();
        var registry = CreateRegistry();

        try
        {
            return sub switch
            {
                "list" => await ListAsync(registry, remainingArgs).ConfigureAwait(false),
                "tag" => await TagAsync(registry, remainingArgs).ConfigureAwait(false),
                "promote" => await PromoteAsync(registry, remainingArgs).ConfigureAwait(false),
                "remove" or "unregister"
                          => await RemoveAsync(registry, remainingArgs).ConfigureAwait(false),
                "help" or "--help" or "-h" => PrintHelp(),
                _ => PrintUnknown(sub),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Agents command failed: {ex.Message}");
            return 1;
        }
    }

    // ── list ──────────────────────────────────────────────────────────────

    private static async Task<int> ListAsync(FileAgentDefinitionRegistry registry, string[] args)
    {
        var env = GetFlag(args, "--env") ?? AgentEnvironments.Dev;
        var verbose = args.Contains("--verbose") || args.Contains("-v");

        var agentsRoot = Path.Combine(DataDirectories.Root, "agents");
        var agents = await registry.ListAsync(env).ConfigureAwait(false);

        if (agents.Count == 0)
        {
            Console.WriteLine($"No agents registered in '{env}' environment.");
            Console.WriteLine($"  Hint: place .agent.yaml files in: {Path.Combine(agentsRoot, env)}");
            return 0;
        }

        Console.WriteLine($"Agents [{env}]:");
        foreach (var a in agents.OrderBy(a => a.Name).ThenByDescending(a => a.Version))
        {
            Console.WriteLine($"  {a.Name}@{a.Version}" +
                              (a.IsDeprecated ? "  [deprecated]" : ""));
            if (verbose)
            {
                if (!string.IsNullOrWhiteSpace(a.Description))
                    Console.WriteLine($"      {a.Description}");
                if (a.Model is { } m)
                    Console.WriteLine($"      Model: {m.Provider}/{m.Id}");
                if (!string.IsNullOrWhiteSpace(a.Loadout))
                    Console.WriteLine($"      Loadout: {a.Loadout}");
                if (a.Workflows.Count > 0)
                    Console.WriteLine($"      Workflows: {string.Join(", ", a.Workflows)}");
                if (a.Tags.Count > 0)
                    Console.WriteLine($"      Tags: {string.Join(", ", a.Tags)}");
            }
        }
        return 0;
    }

    // ── tag ───────────────────────────────────────────────────────────────

    private static async Task<int> TagAsync(FileAgentDefinitionRegistry registry, string[] args)
    {
        // jdai agents tag <name> <version> [--env dev|staging|prod]
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: jdai agents tag <name> <version> [--env <env>]");
            return 1;
        }

        var nameArg = args[0];
        var version = args[1];
        var env = GetFlag(args[2..], "--env") ?? AgentEnvironments.Dev;

        // Resolve existing or require explicit file path
        var existing = await registry.ResolveAsync(nameArg, null, env).ConfigureAwait(false);
        if (existing is null)
        {
            Console.Error.WriteLine($"Agent '{nameArg}' not found in '{env}'.");
            return 1;
        }

        existing.Version = version;
        existing.UpdatedAt = DateTime.UtcNow;
        await registry.RegisterAsync(existing, env).ConfigureAwait(false);

        Console.WriteLine($"✓ Tagged '{nameArg}' as v{version} in '{env}'.");
        return 0;
    }

    // ── promote ───────────────────────────────────────────────────────────

    private static async Task<int> PromoteAsync(FileAgentDefinitionRegistry registry, string[] args)
    {
        // jdai agents promote <name> [<version>] --to <env>
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: jdai agents promote <name> [<version>] --from <env> --to <env>");
            return 1;
        }

        var name = args[0];
        var version = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : "latest";
        var from = GetFlag(args, "--from") ?? AgentEnvironments.Dev;
        var to = GetFlag(args, "--to");

        if (to is null)
        {
            // Default: promote to next environment in chain
            to = AgentEnvironments.NextAfter(from);
            if (to is null)
            {
                Console.Error.WriteLine($"'{from}' is the highest environment. Use --to to specify a target.");
                return 1;
            }
        }

        try
        {
            await registry.PromoteAsync(name, version, from, to).ConfigureAwait(false);
            Console.WriteLine($"✓ Promoted '{name}@{version}' from '{from}' to '{to}'.");
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Promotion failed: {ex.Message}");
            return 1;
        }
    }

    // ── remove ────────────────────────────────────────────────────────────

    private static async Task<int> RemoveAsync(FileAgentDefinitionRegistry registry, string[] args)
    {
        // jdai agents remove <name> <version> [--env dev|staging|prod]
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: jdai agents remove <name> <version> [--env <env>]");
            return 1;
        }

        var name = args[0];
        var version = args[1];
        var env = GetFlag(args[2..], "--env") ?? AgentEnvironments.Dev;

        await registry.UnregisterAsync(name, version, env).ConfigureAwait(false);
        Console.WriteLine($"✓ Removed '{name}@{version}' from '{env}'.");
        return 0;
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static FileAgentDefinitionRegistry CreateRegistry() =>
        new(DataDirectories.Root + Path.DirectorySeparatorChar + "agents");

    private static string? GetFlag(string[] args, string flag)
    {
        var idx = Array.IndexOf(args, flag);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            jdai agents — manage versioned agent definitions

            Usage:
              jdai agents list [--env dev|staging|prod] [-v]
              jdai agents tag <name> <version> [--env <env>]
              jdai agents promote <name> [<version>] [--from <env>] [--to <env>]
              jdai agents remove <name> <version> [--env <env>]

            Environments: dev (default) → staging → prod

            Examples:
              jdai agents list --verbose
              jdai agents tag pr-reviewer 1.2.0
              jdai agents promote pr-reviewer 1.2.0 --from dev --to staging
              jdai agents remove pr-reviewer 1.0.0
            """);
        return 0;
    }

    private static int PrintUnknown(string sub)
    {
        Console.Error.WriteLine($"Unknown agents command: '{sub}'. Run 'jdai agents help' for usage.");
        return 1;
    }
}
