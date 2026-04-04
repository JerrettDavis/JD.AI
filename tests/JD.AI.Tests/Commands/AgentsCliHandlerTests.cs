using JD.AI.Commands;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;

namespace JD.AI.Tests.Commands;

[Collection("DataDirectories")]
public sealed class AgentsCliHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalDataDir;

    public AgentsCliHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-agents-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _originalDataDir = Environment.GetEnvironmentVariable("JDAI_DATA_DIR") ?? string.Empty;
        Environment.SetEnvironmentVariable("JDAI_DATA_DIR", _tempDir);
        DataDirectories.Reset();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JDAI_DATA_DIR",
            string.IsNullOrEmpty(_originalDataDir) ? null : _originalDataDir);
        DataDirectories.Reset();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public async Task HelpAndUnknownSubcommands_ReturnExpectedExitCodes()
    {
        var help = await CaptureStdoutAsync(() => AgentsCliHandler.RunAsync(["help"]));
        var unknown = await CaptureStderrAsync(() => AgentsCliHandler.RunAsync(["bogus"]));

        Assert.Equal(0, help.ExitCode);
        Assert.Contains("jdai agents", help.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, unknown.ExitCode);
        Assert.Contains("Unknown agents command", unknown.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_WhenNoAgents_ReturnsHint()
    {
        var result = await CaptureStdoutAsync(() => AgentsCliHandler.RunAsync(["list", "--env", "dev"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No agents registered", result.Output, StringComparison.Ordinal);
        Assert.Contains("Hint: place .agent.yaml files", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_WhenNoArgs_DefaultsToDevEnvironment()
    {
        var result = await CaptureStdoutAsync(() => AgentsCliHandler.RunAsync([]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No agents registered in 'dev' environment.", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_WithVerboseFlag_PrintsDetailedAgentMetadata()
    {
        var registry = new FileAgentDefinitionRegistry(Path.Combine(DataDirectories.Root, "agents"));
        await registry.RegisterAsync(new AgentDefinition
        {
            Name = "reviewer",
            Version = "1.2.0",
            Description = "PR reviewer",
            IsDeprecated = true,
            Loadout = "developer",
            Model = new AgentModelSpec { Provider = "Test", Id = "model-a" },
            Workflows = ["triage", "review"],
            Tags = ["critical", "code-review"],
        }, AgentEnvironments.Dev);

        var result = await CaptureStdoutAsync(() => AgentsCliHandler.RunAsync(["list", "-v"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Agents [dev]:", result.Output, StringComparison.Ordinal);
        Assert.Contains("reviewer@1.2.0  [deprecated]", result.Output, StringComparison.Ordinal);
        Assert.Contains("PR reviewer", result.Output, StringComparison.Ordinal);
        Assert.Contains("Model: Test/model-a", result.Output, StringComparison.Ordinal);
        Assert.Contains("Loadout: developer", result.Output, StringComparison.Ordinal);
        Assert.Contains("Workflows: triage, review", result.Output, StringComparison.Ordinal);
        Assert.Contains("Tags: critical, code-review", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TagPromoteAndRemove_ExecuteSuccessfully()
    {
        var registry = new FileAgentDefinitionRegistry(Path.Combine(DataDirectories.Root, "agents"));
        await registry.RegisterAsync(new AgentDefinition
        {
            Name = "reviewer",
            Version = "1.0.0",
            Description = "PR reviewer",
            Model = new AgentModelSpec { Provider = "Test", Id = "model-a" },
        }, AgentEnvironments.Dev);

        var tag = await CaptureStdoutAsync(
            () => AgentsCliHandler.RunAsync(["tag", "reviewer", "1.1.0", "--env", "dev"]));

        Assert.Equal(0, tag.ExitCode);
        Assert.Contains("Tagged 'reviewer' as v1.1.0", tag.Output, StringComparison.Ordinal);
        Assert.NotNull(await registry.ResolveAsync("reviewer", "1.0.0", AgentEnvironments.Dev));
        Assert.NotNull(await registry.ResolveAsync("reviewer", "1.1.0", AgentEnvironments.Dev));

        var promote = await CaptureStdoutAsync(
            () => AgentsCliHandler.RunAsync(["promote", "reviewer", "1.1.0", "--from", "dev", "--to", "staging"]));

        Assert.Equal(0, promote.ExitCode);
        Assert.Contains("Promoted 'reviewer@1.1.0'", promote.Output, StringComparison.Ordinal);
        Assert.NotNull(await registry.ResolveAsync("reviewer", "1.1.0", AgentEnvironments.Dev));
        Assert.NotNull(await registry.ResolveAsync("reviewer", "1.1.0", AgentEnvironments.Staging));

        var remove = await CaptureStdoutAsync(
            () => AgentsCliHandler.RunAsync(["remove", "reviewer", "1.1.0", "--env", "staging"]));

        Assert.Equal(0, remove.ExitCode);
        Assert.Contains("Removed 'reviewer@1.1.0'", remove.Output, StringComparison.Ordinal);
        Assert.NotNull(await registry.ResolveAsync("reviewer", "1.1.0", AgentEnvironments.Dev));
        Assert.Null(await registry.ResolveAsync("reviewer", "1.1.0", AgentEnvironments.Staging));
    }

    [Fact]
    public async Task Tag_WhenAgentIsMissing_ReturnsNotFoundError()
    {
        var result = await CaptureStderrAsync(
            () => AgentsCliHandler.RunAsync(["tag", "missing-agent", "1.1.0"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Agent 'missing-agent' not found in 'dev'.", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Promote_WhenAgentIsMissing_ReturnsPromotionFailedError()
    {
        var result = await CaptureStderrAsync(
            () => AgentsCliHandler.RunAsync(["promote", "missing-agent", "1.1.0", "--to", "staging"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Promotion failed:", result.Output, StringComparison.Ordinal);
        Assert.Contains("missing-agent@1.1.0", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Remove_WithoutEnvFlag_UsesDevEnvironment()
    {
        var registry = new FileAgentDefinitionRegistry(Path.Combine(DataDirectories.Root, "agents"));
        await registry.RegisterAsync(new AgentDefinition
        {
            Name = "reviewer",
            Version = "1.0.0",
        }, AgentEnvironments.Dev);

        var result = await CaptureStdoutAsync(
            () => AgentsCliHandler.RunAsync(["remove", "reviewer", "1.0.0"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Removed 'reviewer@1.0.0' from 'dev'.", result.Output, StringComparison.Ordinal);
        Assert.Null(await registry.ResolveAsync("reviewer", "1.0.0", AgentEnvironments.Dev));
    }

    [Fact]
    public async Task MissingArgsAndInvalidPromotionPath_ReturnErrors()
    {
        var tagUsage = await CaptureStderrAsync(() => AgentsCliHandler.RunAsync(["tag"]));
        var removeUsage = await CaptureStderrAsync(() => AgentsCliHandler.RunAsync(["remove", "reviewer"]));
        var promoteHighest = await CaptureStderrAsync(
            () => AgentsCliHandler.RunAsync(["promote", "reviewer", "--from", "prod"]));

        Assert.Equal(1, tagUsage.ExitCode);
        Assert.Contains("Usage: jdai agents tag", tagUsage.Output, StringComparison.Ordinal);

        Assert.Equal(1, removeUsage.ExitCode);
        Assert.Contains("Usage: jdai agents remove", removeUsage.Output, StringComparison.Ordinal);

        Assert.Equal(1, promoteHighest.ExitCode);
        Assert.Contains("highest environment", promoteHighest.Output, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(int ExitCode, string Output)> CaptureStdoutAsync(Func<Task<int>> action)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var code = await action().ConfigureAwait(false);
            return (code, writer.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static async Task<(int ExitCode, string Output)> CaptureStderrAsync(Func<Task<int>> action)
    {
        var original = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            var code = await action().ConfigureAwait(false);
            return (code, writer.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }
}
