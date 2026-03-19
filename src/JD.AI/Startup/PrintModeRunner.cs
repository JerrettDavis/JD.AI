using JD.AI.Core.Agents;
using JD.AI.Core.Governance;
using JD.AI.Core.Providers;
using JD.AI.Core.Skills;

namespace JD.AI.Startup;

/// <summary>
/// Runs jdai in non-interactive print mode: query → stdout → exit.
/// Extracted from Program.cs lines 656-744.
/// </summary>
internal static class PrintModeRunner
{
    public static async Task<int> RunAsync(
        CliOptions opts,
        AgentSession session,
        ProviderModelInfo selectedModel,
        SkillLifecycleManager skillLifecycleManager,
        GovernanceSetup governance)
    {
        var query = new System.Text.StringBuilder();
        if (opts.PipedInput != null)
        {
            query.AppendLine(opts.PipedInput);
            query.AppendLine("---");
        }

        if (opts.PrintQuery != null)
        {
            query.Append(opts.PrintQuery);
        }
        else if (opts.PipedInput == null)
        {
            Console.Error.WriteLine("Error: --print requires a query argument or piped input.");
            return 1;
        }

        var printAgentLoop = new AgentLoop(session);
        var turnOrchestrator = new SessionTurnOrchestrator(session, governance, skillLifecycleManager);
        var turnCount = 0;
        var lastResponse = "";
        string? currentPrintMessage = query.ToString();

        while (currentPrintMessage != null)
        {
            turnCount++;
            if (opts.MaxTurns.HasValue && turnCount > opts.MaxTurns.Value)
            {
                Console.Error.WriteLine($"Error: max turns ({opts.MaxTurns.Value}) exceeded.");
                return 1;
            }

            var turnResult = await turnOrchestrator
                .ExecuteAsync(
                    printAgentLoop,
                    currentPrintMessage,
                    new SessionTurnExecutionOptions(
                        Streaming: false,
                        AutoCompact: false,
                        CompactThresholdPercent: 0,
                        ContextWindowTokens: selectedModel.ContextWindowTokens),
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (!turnResult.Completed)
                return 1;

            lastResponse = turnResult.Response ?? string.Empty;
            currentPrintMessage = null;
        }

        if (string.Equals(opts.OutputFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            var jsonResult = new
            {
                result = lastResponse,
                model = selectedModel.Id,
                provider = selectedModel.ProviderName,
                turns = turnCount,
            };
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(jsonResult,
                JsonOptions.Indented));
        }
        else
        {
            Console.Write(lastResponse);
        }

        // JSON schema validation
        if (opts.JsonSchemaArg is not null)
        {
            return await ValidateSchemaAsync(opts, printAgentLoop, skillLifecycleManager, lastResponse)
                .ConfigureAwait(false);
        }

        return 0;
    }

    private static async Task<int> ValidateSchemaAsync(
        CliOptions opts,
        AgentLoop agentLoop,
        SkillLifecycleManager skillLifecycleManager,
        string lastResponse)
    {
        try
        {
            var schema = JD.AI.Core.Agents.OutputSchemaValidator.LoadSchema(opts.JsonSchemaArg!);
            var errors = JD.AI.Core.Agents.OutputSchemaValidator.Validate(lastResponse, schema);
            if (errors.Count > 0)
            {
                // Retry once with schema feedback
                var retryPrompt = JD.AI.Core.Agents.OutputSchemaValidator.GenerateRetryPrompt(errors, schema);
                using (skillLifecycleManager.BeginRunScope())
                    lastResponse = await agentLoop.RunTurnAsync(retryPrompt).ConfigureAwait(false);
                errors = JD.AI.Core.Agents.OutputSchemaValidator.Validate(lastResponse, schema);
                if (errors.Count > 0)
                {
                    Console.Error.WriteLine("Schema validation failed:");
                    foreach (var err in errors)
                        Console.Error.WriteLine($"  - {err}");
                    return JD.AI.Core.Agents.OutputSchemaValidator.SchemaValidationExitCode;
                }

                Console.Write(lastResponse);
            }
        }
#pragma warning disable CA1031 // schema validation is best-effort
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Schema error: {ex.Message}");
            return JD.AI.Core.Agents.OutputSchemaValidator.SchemaValidationExitCode;
        }
#pragma warning restore CA1031

        return 0;
    }
}

/// <summary>Cached JSON serializer options to satisfy CA1869.</summary>
internal static class JsonOptions
{
    public static readonly System.Text.Json.JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
    };
}
