using JD.AI.Commands;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using NSubstitute;

namespace JD.AI.Tests.Commands;

public sealed class ToolHistoryActionServiceTests
{
    [Fact]
    public async Task ViewDetails_ReturnsExpectedSummary()
    {
        var session = CreateSession();
        var service = new ToolHistoryActionService(session, configStore: null, checkpointStrategy: null);
        var entry = CreateEntry();

        var result = await service.ApplyAsync(entry, ToolHistoryAction.ViewDetails);

        Assert.Contains("Tool: run_command", result);
        Assert.Contains("Status: ok", result);
        Assert.Contains("Args: command=ls", result);
    }

    [Fact]
    public async Task AllowGlobal_UpdatesSessionProfile_AndPersistsConfig()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-tool-history-global-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var session = CreateSession(tempDirectory);
            using var store = new AtomicConfigStore(Path.Combine(tempDirectory, "config.json"));
            var service = new ToolHistoryActionService(session, store, checkpointStrategy: null);
            var entry = CreateEntry(toolName: "bash");

            var result = await service.ApplyAsync(entry, ToolHistoryAction.AllowGlobal);
            var profile = await store.GetToolPermissionProfileAsync(tempDirectory);

            Assert.Contains("Allowed", result);
            Assert.Contains("run_command", profile.GlobalAllowed);
            Assert.True(session.ToolPermissionProfile.IsExplicitlyAllowed("run_command"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AllowProject_UpdatesSessionProfile_AndPersistsConfig()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-tool-history-project-allow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var session = CreateSession(tempDirectory);
            using var store = new AtomicConfigStore(Path.Combine(tempDirectory, "config.json"));
            var service = new ToolHistoryActionService(session, store, checkpointStrategy: null);
            var entry = CreateEntry(toolName: "read_file");

            var result = await service.ApplyAsync(entry, ToolHistoryAction.AllowProject);
            var profile = await store.GetToolPermissionProfileAsync(tempDirectory);

            Assert.Contains("Allowed", result);
            Assert.Contains("read_file", profile.ProjectAllowed);
            Assert.True(session.ToolPermissionProfile.IsExplicitlyAllowed("read_file"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DenyGlobal_UpdatesSessionProfile_AndPersistsConfig()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-tool-history-global-deny-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var session = CreateSession(tempDirectory);
            using var store = new AtomicConfigStore(Path.Combine(tempDirectory, "config.json"));
            var service = new ToolHistoryActionService(session, store, checkpointStrategy: null);
            var entry = CreateEntry(toolName: "run_command");

            var result = await service.ApplyAsync(entry, ToolHistoryAction.DenyGlobal);
            var profile = await store.GetToolPermissionProfileAsync(tempDirectory);

            Assert.Contains("Denied", result);
            Assert.Contains("run_command", profile.GlobalDenied);
            Assert.True(session.ToolPermissionProfile.IsExplicitlyDenied("run_command"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DenyProject_UpdatesSessionProfile_AndPersistsConfig()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-tool-history-project-deny-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var session = CreateSession(tempDirectory);
            using var store = new AtomicConfigStore(Path.Combine(tempDirectory, "config.json"));
            var service = new ToolHistoryActionService(session, store, checkpointStrategy: null);
            var entry = CreateEntry(toolName: "git_push");

            var result = await service.ApplyAsync(entry, ToolHistoryAction.DenyProject);
            var profile = await store.GetToolPermissionProfileAsync(tempDirectory);

            Assert.Contains("Denied", result);
            Assert.Contains("git_push", profile.ProjectDenied);
            Assert.True(session.ToolPermissionProfile.IsExplicitlyDenied("git_push"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static AgentSession CreateSession(string? projectPath = null)
    {
        var registry = Substitute.For<IProviderRegistry>();
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        var session = new AgentSession(registry, kernel, model);
        session.SessionInfo = new JD.AI.Core.Sessions.SessionInfo
        {
            ProjectPath = projectPath ?? Directory.GetCurrentDirectory(),
            ProjectHash = "testhash",
        };
        return session;
    }

    private static ToolHistoryEntry CreateEntry(
        string toolName = "run_command",
        string arguments = "command=ls",
        string result = "line1\nline2\nline3")
    {
        return new ToolHistoryEntry(
            SessionId: "session-1",
            SessionName: "Session 1",
            TurnIndex: 3,
            CreatedAt: DateTime.UtcNow,
            ToolName: toolName,
            Arguments: arguments,
            Result: result,
            Status: "ok",
            DurationMs: 123,
            Label: "label");
    }
}
