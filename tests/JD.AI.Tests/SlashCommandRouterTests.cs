using JD.AI.Tui.Agent;
using JD.AI.Tui.Commands;
using JD.AI.Tui.Providers;
using Microsoft.SemanticKernel;
using NSubstitute;
using Xunit;

namespace JD.AI.Tui.Tests;

public sealed class SlashCommandRouterTests
{
    private readonly IProviderRegistry _registry = Substitute.For<IProviderRegistry>();
    private readonly AgentSession _session;
    private readonly SlashCommandRouter _router;

    public SlashCommandRouterTests()
    {
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        _session = new AgentSession(_registry, kernel, model);
        _router = new SlashCommandRouter(_session, _registry);
    }

    [Theory]
    [InlineData("/help")]
    [InlineData("/models")]
    [InlineData("/quit")]
    [InlineData("  /help")]
    public void IsSlashCommand_DetectsSlashPrefix(string input)
    {
        Assert.True(_router.IsSlashCommand(input));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData("not a /command")]
    public void IsSlashCommand_RejectsNonSlashInput(string input)
    {
        Assert.False(_router.IsSlashCommand(input));
    }

    [Fact]
    public async Task Help_ReturnsCommandList()
    {
        var result = await _router.ExecuteAsync("/help");

        Assert.NotNull(result);
        Assert.Contains("/help", result);
        Assert.Contains("/models", result);
        Assert.Contains("/quit", result);
    }

    [Fact]
    public async Task Provider_ReturnsCurrentModel()
    {
        var result = await _router.ExecuteAsync("/provider");

        Assert.NotNull(result);
        Assert.Contains("Test Model", result);
        Assert.Contains("TestProvider", result);
    }

    [Fact]
    public async Task Models_ListsAvailableModels()
    {
        _registry.GetModelsAsync(Arg.Any<CancellationToken>())
            .Returns([
                new ProviderModelInfo("m1", "Model One", "Provider1"),
                new ProviderModelInfo("m2", "Model Two", "Provider2"),
            ]);

        var result = await _router.ExecuteAsync("/models");

        Assert.NotNull(result);
        Assert.Contains("m1", result);
        Assert.Contains("m2", result);
    }

    [Fact]
    public async Task Models_HandlesNoModels()
    {
        _registry.GetModelsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderModelInfo>());

        var result = await _router.ExecuteAsync("/models");

        Assert.NotNull(result);
        Assert.Contains("No models", result);
    }

    [Fact]
    public async Task Model_SwitchesModel()
    {
        var newModel = new ProviderModelInfo("new-model", "New Model", "Provider1");
        _registry.GetModelsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderModelInfo> { newModel });
        _registry.BuildKernel(newModel).Returns(Kernel.CreateBuilder().Build());

        var result = await _router.ExecuteAsync("/model new-model");

        Assert.NotNull(result);
        Assert.Contains("Switched", result);
        Assert.Equal("new-model", _session.CurrentModel?.Id);
    }

    [Fact]
    public async Task Model_ReportsNotFound()
    {
        _registry.GetModelsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderModelInfo>());

        var result = await _router.ExecuteAsync("/model nonexistent");

        Assert.NotNull(result);
        Assert.Contains("not found", result);
    }

    [Fact]
    public async Task Model_RequiresArgument()
    {
        var result = await _router.ExecuteAsync("/model");

        Assert.NotNull(result);
        Assert.Contains("Usage", result);
    }

    [Fact]
    public async Task Clear_ClearsHistory()
    {
        _session.History.AddUserMessage("some message");
        Assert.NotEmpty(_session.History);

        var result = await _router.ExecuteAsync("/clear");

        Assert.NotNull(result);
        Assert.Contains("cleared", result);
        Assert.Empty(_session.History);
    }

    [Fact]
    public async Task Cost_ReturnsTokenCount()
    {
        _session.TotalTokens = 1234;

        var result = await _router.ExecuteAsync("/cost");

        Assert.NotNull(result);
        Assert.Contains("1,234", result);
    }

    [Fact]
    public async Task Autorun_TogglesOn()
    {
        Assert.False(_session.AutoRunEnabled);

        var result = await _router.ExecuteAsync("/autorun on");

        Assert.NotNull(result);
        Assert.True(_session.AutoRunEnabled);
        Assert.Contains("enabled", result);
    }

    [Fact]
    public async Task Autorun_TogglesOff()
    {
        _session.AutoRunEnabled = true;

        var result = await _router.ExecuteAsync("/autorun off");

        Assert.NotNull(result);
        Assert.False(_session.AutoRunEnabled);
        Assert.Contains("disabled", result);
    }

    [Fact]
    public async Task Autorun_ShowsCurrentState()
    {
        _session.AutoRunEnabled = true;

        var result = await _router.ExecuteAsync("/autorun");

        Assert.NotNull(result);
        Assert.Contains("on", result);
    }

    [Fact]
    public async Task Providers_ListsDetectedProviders()
    {
        _registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns([
                new ProviderInfo("Claude Code", true, "Authenticated", []),
                new ProviderInfo("Ollama", false, "Not running", []),
            ]);

        var result = await _router.ExecuteAsync("/providers");

        Assert.NotNull(result);
        Assert.Contains("Claude Code", result);
        Assert.Contains("Ollama", result);
        Assert.Contains("✅", result);
        Assert.Contains("❌", result);
    }

    [Fact]
    public async Task UnknownCommand_ReturnsError()
    {
        var result = await _router.ExecuteAsync("/foobar");

        Assert.NotNull(result);
        Assert.Contains("Unknown command", result);
    }

    [Fact]
    public async Task Quit_ReturnsNull()
    {
        var result = await _router.ExecuteAsync("/quit");

        Assert.Null(result);
    }

    [Fact]
    public async Task Exit_ReturnsNull()
    {
        var result = await _router.ExecuteAsync("/exit");

        Assert.Null(result);
    }

    [Fact]
    public async Task Permissions_TurnsOff()
    {
        Assert.False(_session.SkipPermissions);

        var result = await _router.ExecuteAsync("/permissions off");

        Assert.NotNull(result);
        Assert.True(_session.SkipPermissions);
        Assert.Contains("DISABLED", result);
    }

    [Fact]
    public async Task Permissions_TurnsOn()
    {
        _session.SkipPermissions = true;

        var result = await _router.ExecuteAsync("/permissions on");

        Assert.NotNull(result);
        Assert.False(_session.SkipPermissions);
        Assert.Contains("enabled", result);
    }

    [Fact]
    public async Task Permissions_AcceptsFalseAlias()
    {
        var result = await _router.ExecuteAsync("/permissions false");

        Assert.NotNull(result);
        Assert.True(_session.SkipPermissions);
    }

    [Fact]
    public async Task Permissions_AcceptsTrueAlias()
    {
        _session.SkipPermissions = true;

        var result = await _router.ExecuteAsync("/permissions true");

        Assert.NotNull(result);
        Assert.False(_session.SkipPermissions);
    }

    [Fact]
    public async Task Permissions_ShowsCurrentState()
    {
        var result = await _router.ExecuteAsync("/permissions");

        Assert.NotNull(result);
        Assert.Contains("ON", result);
    }

    [Fact]
    public async Task Permissions_ShowsOffState()
    {
        _session.SkipPermissions = true;

        var result = await _router.ExecuteAsync("/permissions");

        Assert.NotNull(result);
        Assert.Contains("OFF", result);
    }

    [Fact]
    public async Task Help_IncludesPermissionsCommand()
    {
        var result = await _router.ExecuteAsync("/help");

        Assert.NotNull(result);
        Assert.Contains("/permissions", result);
    }

    [Fact]
    public async Task Help_IncludesSessionCommands()
    {
        var result = await _router.ExecuteAsync("/help");

        Assert.NotNull(result);
        Assert.Contains("/sessions", result);
        Assert.Contains("/resume", result);
        Assert.Contains("/name", result);
        Assert.Contains("/history", result);
        Assert.Contains("/export", result);
        Assert.Contains("/update", result);
    }

    [Fact]
    public async Task Sessions_WithoutStore_ReportsNotInitialized()
    {
        var result = await _router.ExecuteAsync("/sessions");

        Assert.NotNull(result);
        Assert.Contains("not initialized", result);
    }

    [Fact]
    public async Task Name_WithoutSession_ReportsNoActive()
    {
        var result = await _router.ExecuteAsync("/name Test");

        Assert.NotNull(result);
        Assert.Contains("No active session", result);
    }

    [Fact]
    public async Task History_WithoutSession_ReportsNoActive()
    {
        var result = await _router.ExecuteAsync("/history");

        Assert.NotNull(result);
        Assert.Contains("No active session", result);
    }

    [Fact]
    public async Task Export_WithoutSession_ReportsNoActive()
    {
        var result = await _router.ExecuteAsync("/export");

        Assert.NotNull(result);
        Assert.Contains("No active session", result);
    }
}
