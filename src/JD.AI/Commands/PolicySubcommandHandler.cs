namespace JD.AI.Commands;

/// <summary>
/// Top-level dispatcher for <c>jdai policy ...</c> subcommands.
/// </summary>
internal static class PolicySubcommandHandler
{
    public static async Task<int> RunAsync(string[] args)
    {
        var sub = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

        return sub switch
        {
            "compliance" => await PolicyComplianceCliHandler.RunAsync(args[1..]).ConfigureAwait(false),
            "help" or "--help" or "-h" => PrintHelp(),
            _ => PrintUnknown(sub),
        };
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            jdai policy — policy management commands

            Usage:
              jdai policy compliance <subcommand> [options]

            Subcommands:
              compliance list             List available compliance presets
              compliance check --profile  Run compliance check against a preset

            Run 'jdai policy compliance help' for detailed usage.
            """);
        return 0;
    }

    private static int PrintUnknown(string sub)
    {
        Console.Error.WriteLine($"Unknown policy command: '{sub}'. Run 'jdai policy help' for usage.");
        return 1;
    }
}
