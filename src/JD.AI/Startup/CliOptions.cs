using System.Globalization;

namespace JD.AI.Startup;

/// <summary>
/// Strongly-typed representation of all jdai CLI arguments.
/// Produced by <see cref="CliArgumentParser"/>.
/// </summary>
internal sealed record CliOptions
{
    // Mode flags
    public bool SkipPermissions { get; init; }
    public bool PrintMode { get; init; }
    public bool GatewayMode { get; init; }
    public bool VerboseMode { get; init; }
    public bool DebugMode { get; init; }
    public bool ContinueSession { get; init; }
    public bool IsNewSession { get; init; }
    public bool UseWorktree { get; init; }
    public bool ForkSession { get; init; }
    public bool NoSessionPersistence { get; init; }
    public bool ForceUpdateCheck { get; init; }

    // Value flags
    public string? ResumeId { get; init; }
    public string? CliModel { get; init; }
    public string? CliProvider { get; init; }
    public string? GatewayPort { get; init; }
    public string? PrintQuery { get; init; }
    public string? SystemPromptOverride { get; init; }
    public string? AppendSystemPrompt { get; init; }
    public string? SystemPromptFile { get; init; }
    public string? AppendSystemPromptFile { get; init; }
    public string OutputFormat { get; init; } = "text";
    public int? MaxTurns { get; init; }
    public string? PermissionModeStr { get; init; }
    public string? CliSessionId { get; init; }
    public decimal? MaxBudgetUsd { get; init; }
    public string? JsonSchemaArg { get; init; }
    public string InputFormat { get; init; } = "text";
    public string? DebugCategories { get; init; }

    // Collections
    public IReadOnlyList<string> AdditionalDirs { get; init; } = [];
    public string[]? AllowedTools { get; init; }
    public string[]? DisallowedTools { get; init; }
    public string[] FallbackModels { get; init; } = [];

    // Piped input
    public string? PipedInput { get; init; }

    // Subcommand interception
    public string? Subcommand { get; init; }
    public string[] SubcommandArgs { get; init; } = [];
}

/// <summary>
/// Parses raw <c>args</c> into a <see cref="CliOptions"/> record.
/// </summary>
internal static class CliArgumentParser
{
    public static async Task<CliOptions> ParseAsync(string[] args)
    {
        var skipPermissions = args.Contains("--dangerously-skip-permissions");
        var forceUpdateCheck = args.Contains("--force-update-check");
        var resumeId = GetFlagValue(args, "--resume");
        var isNewSession = args.Contains("--new");
        var cliModel = GetFlagValue(args, "--model");
        var cliProvider = GetFlagValue(args, "--provider");
        var gatewayMode = args.Contains("--gateway");
        var gatewayPort = GetFlagValue(args, "--gateway-port");

        // Subcommand interception (mcp, plugin)
        var firstNonOptionIndex = Array.FindIndex(args, a => !a.StartsWith('-'));
        string? subcommand = null;
        string[] subcommandArgs = [];
        if (firstNonOptionIndex >= 0)
        {
            var candidate = args[firstNonOptionIndex];
            if (string.Equals(candidate, "mcp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate, "plugin", StringComparison.OrdinalIgnoreCase))
            {
                subcommand = candidate.ToLowerInvariant();
                subcommandArgs = args.Skip(firstNonOptionIndex + 1).ToArray();
            }
        }

        // Print mode
        var printMode = args.Contains("-p") || args.Contains("--print");
        var printQuery = printMode
            ? GetFlagValue(args, "-p") ?? GetFlagValue(args, "--print")
            : null;

        var continueSession = args.Contains("-c") || args.Contains("--continue");
        var systemPromptOverride = GetFlagValue(args, "--system-prompt");
        var appendSystemPrompt = GetFlagValue(args, "--append-system-prompt");
        var systemPromptFile = GetFlagValue(args, "--system-prompt-file");
        var appendSystemPromptFile = GetFlagValue(args, "--append-system-prompt-file");
        var outputFormat = GetFlagValue(args, "--output-format") ?? "text";

        var maxTurnsStr = GetFlagValue(args, "--max-turns");
        int? maxTurns = int.TryParse(maxTurnsStr, out var mt) ? mt : null;

        var verboseMode = args.Contains("--verbose");

        var addDirs = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--add-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                addDirs.Add(args[++i]);
            }
        }

        var allowedTools = GetFlagValue(args, "--allowedTools")
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var disallowedTools = GetFlagValue(args, "--disallowedTools")
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var permissionModeStr = GetFlagValue(args, "--permission-mode");
        var fallbackModelStr = GetFlagValue(args, "--fallback-model");
        var fallbackModels = fallbackModelStr?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        var cliSessionId = GetFlagValue(args, "--session-id");
        var forkSession = args.Contains("--fork-session");
        var noSessionPersistence = args.Contains("--no-session-persistence");

        var maxBudgetStr = GetFlagValue(args, "--max-budget-usd");
        decimal? maxBudgetUsd = decimal.TryParse(maxBudgetStr, CultureInfo.InvariantCulture, out var mb) ? mb : null;

        var useWorktree = args.Contains("-w") || args.Contains("--worktree");
        var jsonSchemaArg = GetFlagValue(args, "--json-schema");
        var inputFormat = GetFlagValue(args, "--input-format") ?? "text";

        var debugMode = args.Contains("--debug");
        var debugCategories = debugMode ? GetFlagValue(args, "--debug") : null;
        if (debugCategories != null && debugCategories.StartsWith('-'))
        {
            debugCategories = null;
        }

        // Read piped stdin
        string? pipedInput = null;
        if (Console.IsInputRedirected)
        {
            pipedInput = await Console.In.ReadToEndAsync().ConfigureAwait(false);
        }

        return new CliOptions
        {
            SkipPermissions = skipPermissions,
            ForceUpdateCheck = forceUpdateCheck,
            ResumeId = resumeId,
            IsNewSession = isNewSession,
            CliModel = cliModel,
            CliProvider = cliProvider,
            GatewayMode = gatewayMode,
            GatewayPort = gatewayPort,
            Subcommand = subcommand,
            SubcommandArgs = subcommandArgs,
            PrintMode = printMode,
            PrintQuery = printQuery,
            ContinueSession = continueSession,
            SystemPromptOverride = systemPromptOverride,
            AppendSystemPrompt = appendSystemPrompt,
            SystemPromptFile = systemPromptFile,
            AppendSystemPromptFile = appendSystemPromptFile,
            OutputFormat = outputFormat,
            MaxTurns = maxTurns,
            VerboseMode = verboseMode,
            AdditionalDirs = addDirs,
            AllowedTools = allowedTools,
            DisallowedTools = disallowedTools,
            PermissionModeStr = permissionModeStr,
            FallbackModels = fallbackModels,
            CliSessionId = cliSessionId,
            ForkSession = forkSession,
            NoSessionPersistence = noSessionPersistence,
            MaxBudgetUsd = maxBudgetUsd,
            UseWorktree = useWorktree,
            JsonSchemaArg = jsonSchemaArg,
            InputFormat = inputFormat,
            DebugMode = debugMode,
            DebugCategories = debugCategories,
            PipedInput = pipedInput,
        };
    }

    private static string? GetFlagValue(string[] args, string flag)
    {
        return args
            .SkipWhile(a => !string.Equals(a, flag, StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault();
    }
}
