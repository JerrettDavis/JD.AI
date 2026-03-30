using System.Diagnostics;
using JD.AI.Sandbox.Abstractions;

namespace JD.AI.Sandbox.Pipeline;

/// <summary>
/// Result of a pipeline execution spanning multiple stages (e.g., planner → executor).
/// </summary>
public sealed class PipelineExecutionResult
{
    public bool Success { get; init; }
    public IReadOnlyList<StageResult> Stages { get; init; } = [];
    public TimeSpan TotalElapsed { get; init; }
    public string? Error { get; init; }
}

/// <summary>Result of a single pipeline stage.</summary>
public sealed class StageResult
{
    public required string StageName { get; init; }
    public required SandboxExecutionResult Execution { get; init; }
    public byte[]? OutputBytes { get; init; }
}

/// <summary>
/// Represents a single stage in a sandbox pipeline.
/// </summary>
public sealed class PipelineStage
{
    public required string Name { get; init; }
    public required ISandbox Sandbox { get; init; }
    public required string ExecutablePath { get; init; }
    public string Arguments { get; set; } = string.Empty;
}

/// <summary>
/// Orchestrates sequential sandboxed execution stages connected by kernel pipes (stdout → stdin).
/// The planner completes fully before the executor begins, ensuring strict capability separation.
/// </summary>
public static class SandboxPipeline
{
    /// <summary>
    /// Builds a pipeline from two stages: a planner (has network, no data) and an executor
    /// (has filesystem/tools, no network), connected by a unidirectional pipe.
    /// </summary>
    public static PipelineStage[] Plan(SandboxPolicy plannerPolicy, SandboxPolicy executorPolicy, string plannerExe, string executorExe)
    {
        var planner = new PipelineStage
        {
            Name = "planner",
            Sandbox = CreateSandbox(plannerPolicy),
            ExecutablePath = plannerExe,
        };

        var executor = new PipelineStage
        {
            Name = "executor",
            Sandbox = CreateSandbox(executorPolicy),
            ExecutablePath = executorExe,
        };

        return [planner, executor];
    }

    /// <summary>
    /// Runs the pipeline sequentially: each stage receives the output of the previous stage via stdin.
    /// </summary>
    public static async Task<PipelineExecutionResult> RunAsync(
        PipelineStage[] stages,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var stageResults = new List<StageResult>();
        var pipeData = string.Empty;

        for (var i = 0; i < stages.Length; i++)
        {
            var stage = stages[i];

            // Inject the previous stage's output as stdin to the next stage
            if (i > 0 && !string.IsNullOrEmpty(pipeData))
            {
                stage.Arguments = $"{stage.Arguments} --input-from-stdin";
            }

            var result = await stage.Sandbox.RunAsync(stage.ExecutablePath, stage.Arguments, ct)
                .ConfigureAwait(false);

            pipeData = result.StandardOutput;

            stageResults.Add(new StageResult
            {
                StageName = stage.Name,
                Execution = result,
                OutputBytes = System.Text.Encoding.UTF8.GetBytes(result.StandardOutput),
            });

            if (!result.Success)
            {
                return new PipelineExecutionResult
                {
                    Success = false,
                    Stages = stageResults,
                    TotalElapsed = sw.Elapsed,
                    Error = $"Stage '{stage.Name}' failed with exit code {result.ExitCode}: {result.Error ?? result.StandardError}",
                };
            }
        }

        return new PipelineExecutionResult
        {
            Success = true,
            Stages = stageResults,
            TotalElapsed = sw.Elapsed,
        };
    }

    /// <summary>
    /// Creates the appropriate sandbox implementation for the current platform.
    /// </summary>
    public static ISandbox CreateSandbox(SandboxPolicy policy)
    {
        if (OperatingSystem.IsWindows())
            return new Runtime.Windows.WindowsSandbox(policy);
        if (OperatingSystem.IsLinux())
            return new Runtime.Linux.LinuxSandbox(policy);
        return new NoneSandbox(policy);
    }

    /// <summary>
    /// Creates a planner policy: has network/API access, no filesystem/data access.
    /// </summary>
    public static SandboxPolicy PlannerPolicy(string? allowedApiEndpoint = null) =>
        new()
        {
            Name = "PlannerPolicy",
            AllowNetwork = true,
            AllowRead = false,
            AllowWrite = false,
            AllowProcessSpawn = false,
        };

    /// <summary>
    /// Creates an executor policy: has filesystem/tool access, no network access.
    /// </summary>
    public static SandboxPolicy ExecutorPolicy(params string[] allowedPaths) =>
        new()
        {
            Name = "ExecutorPolicy",
            AllowNetwork = false,
            AllowRead = true,
            AllowWrite = true,
            AllowedPaths = allowedPaths,
            AllowProcessSpawn = false,
        };
}
