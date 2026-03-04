using JD.AI.Commands;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using NSubstitute;
using Xunit;

namespace JD.AI.Tests;

[Collection("DataDirectories")]
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
    public async Task Models_NoModels_ReturnsTextFallback()
    {
        _registry.GetModelsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderModelInfo>());

        var result = await _router.ExecuteAsync("/models");

        Assert.NotNull(result);
        Assert.Contains("No models", result);
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
    public async Task Model_NoArgs_NoModels_ReturnsMessage()
    {
        _registry.GetModelsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderModelInfo>());

        var result = await _router.ExecuteAsync("/model");

        Assert.NotNull(result);
        Assert.Contains("No models", result);
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
        Assert.Contains("✓", result);
        Assert.Contains("✗", result);
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
    public async Task Help_IncludesSpinnerCommand()
    {
        var result = await _router.ExecuteAsync("/help");

        Assert.NotNull(result);
        Assert.Contains("/spinner", result);
    }

    [Fact]
    public async Task Spinner_NoCallbacks_ReportsNotConfigurable()
    {
        var result = await _router.ExecuteAsync("/spinner");

        Assert.NotNull(result);
        Assert.Contains("not configurable", result);
    }

    [Fact]
    public async Task Spinner_ShowsCurrent()
    {
        var router = CreateRouterWithSpinner(SpinnerStyle.Normal);

        var result = await router.ExecuteAsync("/spinner");

        Assert.NotNull(result);
        Assert.Contains("normal", result);
        Assert.Contains("Available", result);
    }

    [Fact]
    public async Task Spinner_SetsStyle()
    {
        var currentStyle = SpinnerStyle.Normal;
        var router = CreateRouterWithSpinner(
            currentStyle,
            style => currentStyle = style);

        var result = await router.ExecuteAsync("/spinner rich");

        Assert.NotNull(result);
        Assert.Contains("rich", result);
        Assert.Equal(SpinnerStyle.Rich, currentStyle);
    }

    [Fact]
    public async Task Spinner_InvalidStyle_ReportsError()
    {
        var router = CreateRouterWithSpinner(SpinnerStyle.Normal);

        var result = await router.ExecuteAsync("/spinner fancy");

        Assert.NotNull(result);
        Assert.Contains("Unknown style", result);
        Assert.Contains("fancy", result);
    }

    [Theory]
    [InlineData("/spinner none", SpinnerStyle.None)]
    [InlineData("/spinner minimal", SpinnerStyle.Minimal)]
    [InlineData("/spinner normal", SpinnerStyle.Normal)]
    [InlineData("/spinner rich", SpinnerStyle.Rich)]
    [InlineData("/spinner nerdy", SpinnerStyle.Nerdy)]
    [InlineData("/jdai-spinner rich", SpinnerStyle.Rich)]
    public async Task Spinner_AllStyles_SetCorrectly(string command, SpinnerStyle expected)
    {
        var currentStyle = SpinnerStyle.Normal;
        var router = CreateRouterWithSpinner(
            currentStyle,
            style => currentStyle = style);

        await router.ExecuteAsync(command);

        Assert.Equal(expected, currentStyle);
    }

    private SlashCommandRouter CreateRouterWithSpinner(
        SpinnerStyle initial,
        Action<SpinnerStyle>? onChanged = null)
    {
        var style = initial;
        return new SlashCommandRouter(
            _session, _registry,
            getSpinnerStyle: () => style,
            onSpinnerStyleChanged: s =>
            {
                style = s;
                onChanged?.Invoke(s);
            });
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

    [Fact]
    public async Task Theme_NoCallbacks_ReportsNotConfigurable()
    {
        var result = await _router.ExecuteAsync("/theme");

        Assert.NotNull(result);
        Assert.Contains("not configurable", result);
    }

    [Fact]
    public async Task Theme_SetsTheme()
    {
        var currentTheme = TuiTheme.DefaultDark;
        var router = new SlashCommandRouter(
            _session, _registry,
            getTheme: () => currentTheme,
            onThemeChanged: t => currentTheme = t);

        var result = await router.ExecuteAsync("/theme monokai");

        Assert.NotNull(result);
        Assert.Contains("monokai", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TuiTheme.Monokai, currentTheme);
    }

    [Fact]
    public async Task Vim_TogglesOnAndOff()
    {
        var vimMode = false;
        var router = new SlashCommandRouter(
            _session, _registry,
            getVimMode: () => vimMode,
            onVimModeChanged: enabled => vimMode = enabled);

        var onResult = await router.ExecuteAsync("/vim on");
        var offResult = await router.ExecuteAsync("/vim off");

        Assert.NotNull(onResult);
        Assert.NotNull(offResult);
        Assert.Contains("ON", onResult, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OFF", offResult, StringComparison.OrdinalIgnoreCase);
        Assert.False(vimMode);
    }

    [Fact]
    public async Task OutputStyle_SetsStyle()
    {
        var style = OutputStyle.Rich;
        var router = new SlashCommandRouter(
            _session, _registry,
            getOutputStyle: () => style,
            onOutputStyleChanged: s => style = s);

        var result = await router.ExecuteAsync("/output-style json");

        Assert.NotNull(result);
        Assert.Contains("json", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(OutputStyle.Json, style);
    }

    [Fact]
    public async Task Config_List_IncludesTheme()
    {
        var result = await _router.ExecuteAsync("/config");

        Assert.NotNull(result);
        Assert.Contains("theme", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vim_mode", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stats_WithoutSession_UsesFallback()
    {
        var result = await _router.ExecuteAsync("/stats");

        Assert.NotNull(result);
        Assert.Contains("unavailable", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Review_WithTargetOutsideGitRepo_ReturnsFailureMessage()
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-review-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            Directory.SetCurrentDirectory(tempDirectory);
            var result = await _router.ExecuteAsync("/review --target main");

            Assert.NotNull(result);
            Assert.Contains("Review failed:", result, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("git", result, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AgentsList_WithCorruptJson_ReturnsEmptyStateMessage()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-agents-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        DataDirectories.SetRoot(tempDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDirectory, "agents.json"), "{{not valid json}}");
            var result = await _router.ExecuteAsync("/agents list");

            Assert.NotNull(result);
            Assert.Contains("No agent profiles configured", result, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DataDirectories.Reset();
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task HooksList_WithCorruptJson_ReturnsEmptyStateMessage()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-hooks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        DataDirectories.SetRoot(tempDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDirectory, "hooks.json"), "{{not valid json}}");
            var result = await _router.ExecuteAsync("/hooks list");

            Assert.NotNull(result);
            Assert.Contains("No hooks configured", result, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DataDirectories.Reset();
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
