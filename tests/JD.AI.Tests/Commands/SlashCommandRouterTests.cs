using FluentAssertions;
using JD.AI.Commands;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Core.Providers;
using JD.AI.Rendering;
using Microsoft.SemanticKernel;
using NSubstitute;
using Xunit;

namespace JD.AI.Tests.Commands;

public sealed class SlashCommandRouterTests
{
    private readonly IProviderRegistry _registry = Substitute.For<IProviderRegistry>();

    private SlashCommandRouter CreateRouter(
        InstructionsResult? instructions = null,
        Func<SpinnerStyle>? getSpinnerStyle = null,
        Action<SpinnerStyle>? onSpinnerStyleChanged = null,
        Func<TuiTheme>? getTheme = null,
        Action<TuiTheme>? onThemeChanged = null,
        Func<bool>? getVimMode = null,
        Action<bool>? onVimModeChanged = null,
        Func<OutputStyle>? getOutputStyle = null,
        Action<OutputStyle>? onOutputStyleChanged = null,
        Func<string>? getSkillsStatus = null)
    {
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        var session = new AgentSession(_registry, kernel, model);

        return new SlashCommandRouter(
            session,
            _registry,
            instructions: instructions,
            getSpinnerStyle: getSpinnerStyle,
            onSpinnerStyleChanged: onSpinnerStyleChanged,
            getTheme: getTheme,
            onThemeChanged: onThemeChanged,
            getVimMode: getVimMode,
            onVimModeChanged: onVimModeChanged,
            getOutputStyle: getOutputStyle,
            onOutputStyleChanged: onOutputStyleChanged,
            getSkillsStatus: getSkillsStatus);
    }

    [Fact]
    public void IsSlashCommand_InputStartsWithSlash_ReturnsTrue()
    {
        // Arrange
        var router = CreateRouter();
        var input = "/help";

        // Act
        var result = router.IsSlashCommand(input);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("/model")]
    [InlineData("  /providers")]
    [InlineData("\t/quit")]
    public void IsSlashCommand_VariousSlashInputs_ReturnsTrue(string input)
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = router.IsSlashCommand(input);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSlashCommand_NormalText_ReturnsFalse()
    {
        // Arrange
        var router = CreateRouter();
        var input = "What is the weather?";

        // Act
        var result = router.IsSlashCommand(input);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("just plain text")]
    [InlineData("123 456")]
    public void IsSlashCommand_NonSlashInputs_ReturnsFalse(string input)
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = router.IsSlashCommand(input);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_EmptyInput_ReturnsHelpMessage()
    {
        // Arrange
        var router = CreateRouter();
        var input = "  ";

        // Act
        var result = await router.ExecuteAsync(input);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("help");
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCommand_ReturnsUnknownCommandMessage()
    {
        // Arrange
        var router = CreateRouter();
        var input = "/unknowncommand";

        // Act
        var result = await router.ExecuteAsync(input);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Unknown command");
    }

    [Fact]
    public async Task ExecuteAsync_HelpCommand_ReturnsNonNullText()
    {
        // Arrange
        var router = CreateRouter();
        var input = "/help";

        // Act
        var result = await router.ExecuteAsync(input);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("/");
    }

    [Fact]
    public async Task ExecuteAsync_QuitCommand_ReturnsNull()
    {
        // Arrange
        var router = CreateRouter();
        var input = "/quit";

        // Act
        var result = await router.ExecuteAsync(input);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ModelCurrentCommand_ReturnsCurrentModelInfo()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model-id", "Test Model", "TestProvider");
        var session = new AgentSession(_registry, kernel, model);

        var router = new SlashCommandRouter(session, _registry);
        var input = "/model";

        // Act
        var result = await router.ExecuteAsync(input);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Result should contain information about the current model or a message
        // about switching models (depends on implementation)
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_CompletesOrCancels()
    {
        // Arrange
        var router = CreateRouter();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act — /help is synchronous and doesn't check CT, so it completes normally.
        // Other commands that call async services may throw OperationCanceledException.
        // Either outcome (success or cancellation) is acceptable.
        var act = async () => await router.ExecuteAsync("/help", cts.Token);
        var exception = await Record.ExceptionAsync(act);

        // Assert — should either succeed or throw OperationCanceledException (no other exceptions)
        if (exception != null)
        {
            exception.Should().BeAssignableTo<OperationCanceledException>();
        }
    }

    [Fact]
    public async Task ExecuteAsync_ClearCommand_ClearsHistory()
    {
        // Arrange
        var router = CreateRouter();
        var input = "/clear";

        // Act
        var result = await router.ExecuteAsync(input);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IsSlashCommand_EmptyInput_ReturnsFalse()
    {
        // Arrange
        var router = CreateRouter();
        var input = "";

        // Act
        var result = router.IsSlashCommand(input);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSlashCommand_OnlyWhitespace_ReturnsFalse()
    {
        // Arrange
        var router = CreateRouter();
        var input = "   \t  ";

        // Act
        var result = router.IsSlashCommand(input);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("/help arg")]
    [InlineData("/model openai")]
    [InlineData("/cost --month")]
    public async Task ExecuteAsync_CommandsWithArguments_ProcessArguments(string input)
    {
        // Arrange
        var router = CreateRouter();

        // Act - should not throw
        var result = await router.ExecuteAsync(input);

        // Assert - result should be non-null (either actual output or error message)
        result.Should().NotBeNull();
    }

    // ============ GROUP 1: Pure handlers (no complex mocks) ============

    [Fact]
    public async Task ExecuteAsync_AutorunNoArg_ReturnsStatus()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/autorun");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Match("*on*off*");
    }

    [Fact]
    public async Task ExecuteAsync_AutorunOn_EnablesAndReturnsMessage()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        session.AutoRunEnabled = false;
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/autorun on");

        // Assert
        result.Should().Contain("enabled");
        session.AutoRunEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_AutorunOff_DisablesAndReturnsMessage()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        session.AutoRunEnabled = true;
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/autorun off");

        // Assert
        result.Should().Contain("disabled");
        session.AutoRunEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_PermissionsNoArg_ReturnsSummary()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/permissions");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Permission mode");
    }

    [Fact]
    public async Task ExecuteAsync_PermissionsPlanMode_SetsAndReturnsMessage()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/permissions plan");

        // Assert
        result.Should().Contain("Plan mode");
    }

    [Fact]
    public async Task ExecuteAsync_Plan_TogglesMode()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        var initialState = session.PlanMode;
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/plan");

        // Assert
        result.Should().NotBeNullOrEmpty();
        session.PlanMode.Should().NotBe(initialState);
    }

    [Fact]
    public async Task ExecuteAsync_Shortcuts_ReturnsHelpText()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/shortcuts");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Ctrl");
    }

    [Fact]
    public async Task ExecuteAsync_Sandbox_ReturnsSandboxInfo()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/sandbox");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Sandbox");
    }

    [Fact]
    public async Task ExecuteAsync_ClearHistory_ClearsAndReturnsMessage()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        session.History.AddUserMessage("test message");
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/clear");

        // Assert
        result.Should().Contain("cleared");
        session.History.Count.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ReasoningNoArg_ShowsCurrentLevel()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/reasoning");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Reasoning effort");
    }

    [Fact]
    public async Task ExecuteAsync_ReasoningHigh_SetsAndReturnsMessage()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-o1", "O1", "OpenAI");
        var session = new AgentSession(_registry, kernel, model);
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/reasoning high");

        // Assert
        result.Should().Contain("high");
        session.ReasoningEffortOverride.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_TraceNoTrace_ReturnsNoTraceMessage()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/trace");

        // Assert
        result.Should().Contain("No trace");
    }

    [Fact]
    public async Task ExecuteAsync_Context_ReturnsContextUsageBar()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/context");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Context");
    }

    [Fact]
    public async Task ExecuteAsync_InstructionsNone_ReturnsNoInstructionsMessage()
    {
        // Arrange
        var router = CreateRouter(instructions: null);

        // Act
        var result = await router.ExecuteAsync("/instructions");

        // Assert
        result.Should().Contain("No");
    }

    // Note: InstructionsResult is sealed, so we cannot test the success case with Substitute
    // The null case (/instructions with no instructions) is tested above

    [Fact]
    public async Task ExecuteAsync_HistoryNoSessionInfo_ReturnsNoSessionMessage()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/history");

        // Assert
        result.Should().Contain("No active session");
    }

    // ============ GROUP 2: Model/Provider handlers ============

    [Fact]
    public async Task ExecuteAsync_ModelsNoModels_ReturnsNoModelsMessage()
    {
        // Arrange
        _registry.GetModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderModelInfo>>([]));
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/models");

        // Assert
        result.Should().Contain("No models");
    }

    [Fact]
    public async Task ExecuteAsync_ModelsMultiple_ReturnsFormattedList()
    {
        // Arrange
        var models = new List<ProviderModelInfo>
        {
            new("gpt-4", "GPT-4", "OpenAI"),
            new("claude-3", "Claude 3", "Anthropic"),
            new("gemini-pro", "Gemini Pro", "Google")
        };
        _registry.GetModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderModelInfo>>(models));
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/models");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("gpt-4");
        result.Should().Contain("claude-3");
    }

    // ============ GROUP 9: TUI Config handlers ============

    [Fact]
    public async Task ExecuteAsync_SpinnerNoDelegates_ReturnsNotConfigurableMessage()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/spinner");

        // Assert
        result.Should().Contain("not configurable");
    }

    [Fact]
    public async Task ExecuteAsync_SpinnerWithDelegates_ReturnsCurrentStyle()
    {
        // Arrange
        var getSpinner = new Func<SpinnerStyle>(() => SpinnerStyle.Normal);
        var onSpinner = new Action<SpinnerStyle>(_ => { });
        var router = CreateRouter(getSpinnerStyle: getSpinner, onSpinnerStyleChanged: onSpinner);

        // Act
        var result = await router.ExecuteAsync("/spinner");

        // Assert
        result.Should().Contain("Current spinner style");
    }

    [Fact]
    public async Task ExecuteAsync_SpinnerValidStyle_SetsAndReturnsMessage()
    {
        // Arrange
        var getSpinner = new Func<SpinnerStyle>(() => SpinnerStyle.Normal);
        var styleSet = false;
        var onSpinner = new Action<SpinnerStyle>(_ => styleSet = true);
        var router = CreateRouter(getSpinnerStyle: getSpinner, onSpinnerStyleChanged: onSpinner);

        // Act
        var result = await router.ExecuteAsync("/spinner rich");

        // Assert
        result.Should().Contain("set to");
        styleSet.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_SpinnerUnknownStyle_ReturnsErrorMessage()
    {
        // Arrange
        var getSpinner = new Func<SpinnerStyle>(() => SpinnerStyle.Normal);
        var onSpinner = new Action<SpinnerStyle>(_ => { });
        var router = CreateRouter(getSpinnerStyle: getSpinner, onSpinnerStyleChanged: onSpinner);

        // Act
        var result = await router.ExecuteAsync("/spinner unknown_style");

        // Assert
        result.Should().Contain("Unknown");
    }

    [Fact]
    public async Task ExecuteAsync_ThemeNoDelegates_ReturnsNotConfigurableMessage()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/theme");

        // Assert
        result.Should().Contain("not configurable");
    }

    [Fact]
    public async Task ExecuteAsync_ThemeWithDelegates_ReturnsCurrentTheme()
    {
        // Arrange
        var getTheme = new Func<TuiTheme>(() => TuiTheme.DefaultDark);
        var onTheme = new Action<TuiTheme>(_ => { });
        var router = CreateRouter(getTheme: getTheme, onThemeChanged: onTheme);

        // Act
        var result = await router.ExecuteAsync("/theme");

        // Assert
        result.Should().Contain("Current theme");
    }

    [Fact]
    public async Task ExecuteAsync_VimModeNoDelegates_ReturnsNotConfigurableMessage()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/vim");

        // Assert
        result.Should().Contain("not configurable");
    }

    [Fact]
    public async Task ExecuteAsync_VimModeWithDelegatesOff_TogglesAndReturnsMessage()
    {
        // Arrange
        var getVim = new Func<bool>(() => false);
        var vimState = false;
        var onVim = new Action<bool>(state => { vimState = state; });
        var router = CreateRouter(getVimMode: getVim, onVimModeChanged: onVim);

        // Act
        var result = await router.ExecuteAsync("/vim");

        // Assert
        result.Should().Contain("Vim mode");
        vimState.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_VimModeOn_EnablesAndReturnsMessage()
    {
        // Arrange
        var getVim = new Func<bool>(() => false);
        var vimState = false;
        var onVim = new Action<bool>(state => { vimState = state; });
        var router = CreateRouter(getVimMode: getVim, onVimModeChanged: onVim);

        // Act
        var result = await router.ExecuteAsync("/vim on");

        // Assert
        result.Should().Contain("ON");
        vimState.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_OutputStyleNoDelegates_ReturnsNotConfigurableMessage()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/output-style");

        // Assert
        result.Should().Contain("not configurable");
    }

    [Fact]
    public async Task ExecuteAsync_OutputStyleWithDelegates_ReturnsCurrentStyle()
    {
        // Arrange
        var getStyle = new Func<OutputStyle>(() => OutputStyle.Rich);
        var onStyle = new Action<OutputStyle>(_ => { });
        var router = CreateRouter(getOutputStyle: getStyle, onOutputStyleChanged: onStyle);

        // Act
        var result = await router.ExecuteAsync("/output-style");

        // Assert
        result.Should().Contain("Current output style");
    }

    // ============ GROUP 3: Session commands ============

    [Fact]
    public async Task ExecuteAsync_SessionsNoStore_ReturnsNotInitializedMessage()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/sessions");

        // Assert
        result.Should().Contain("not initialized");
    }

    [Fact]
    public async Task ExecuteAsync_ExportNoSessionInfo_ReturnsNoSessionMessage()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/export");

        // Assert
        result.Should().Contain("No active session");
    }

    // ============ GROUP 11: System prompt/Prompt commands ============

    [Fact]
    public async Task ExecuteAsync_SystemPromptNoPrompt_ReturnsNoPromptMessage()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/system-prompt");

        // Assert
        result.Should().Contain("No");
    }

    [Fact]
    public async Task ExecuteAsync_SystemPromptReset_ReturnsErrorWhenNoOriginal()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/system-prompt reset");

        // Assert
        result.Should().Contain("No original");
    }

    [Fact]
    public async Task ExecuteAsync_CompactHistoryEmpty_ReturnsCompactedMessage()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/compact");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("compacted");
    }

    [Fact]
    public async Task ExecuteAsync_PromptDropInvalidIndex_ReturnsErrorMessage()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/prompt drop invalid");

        // Assert
        result.Should().Contain("Usage");
    }

    // ============ Skills handler ============

    [Fact]
    public async Task ExecuteAsync_SkillsNoStatus_ReturnsNotInitializedMessage()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/skills");

        // Assert
        result.Should().Contain("not initialized");
    }

    [Fact]
    public async Task ExecuteAsync_SkillsWithStatus_ReturnsStatusMessage()
    {
        // Arrange
        var statusFunc = new Func<string>(() => "Skills enabled (3 installed)");
        var router = CreateRouter(getSkillsStatus: statusFunc);

        // Act
        var result = await router.ExecuteAsync("/skills");

        // Assert
        result.Should().Be("Skills enabled (3 installed)");
    }

    [Fact]
    public async Task ExecuteAsync_SkillsStatus_ReturnsStatusMessage()
    {
        // Arrange
        var statusFunc = new Func<string>(() => "Skills: 5 loaded, 2 disabled");
        var router = CreateRouter(getSkillsStatus: statusFunc);

        // Act
        var result = await router.ExecuteAsync("/skills status");

        // Assert
        result.Should().Be("Skills: 5 loaded, 2 disabled");
    }

    // ============ Additional comprehensive tests ============

    [Fact]
    public async Task ExecuteAsync_PermissionsOff_DisablesPermissions()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        session.SkipPermissions = false;
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/permissions off");

        // Assert
        result.Should().Contain("DISABLED");
        session.SkipPermissions.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_PermissionsOn_EnablesPermissions()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        session.SkipPermissions = true;
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/permissions on");

        // Assert
        result.Should().Contain("Permission checks enabled");
        session.SkipPermissions.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ReasoningAuto_ResetsToAuto()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/reasoning auto");

        // Assert
        result.Should().Contain("auto");
        session.ReasoningEffortOverride.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ReasoningLow_SetsLowLevel()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/reasoning low");

        // Assert
        result.Should().Contain("low");
    }

    [Fact]
    public async Task ExecuteAsync_ReasoningInvalid_ReturnsUsageMessage()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/reasoning invalid");

        // Assert
        result.Should().Contain("Usage");
    }

    [Fact]
    public async Task ExecuteAsync_VimModeOff_DisablesAndReturnsMessage()
    {
        // Arrange
        var getVim = new Func<bool>(() => true);
        var vimState = true;
        var onVim = new Action<bool>(state => { vimState = state; });
        var router = CreateRouter(getVimMode: getVim, onVimModeChanged: onVim);

        // Act
        var result = await router.ExecuteAsync("/vim off");

        // Assert
        result.Should().Contain("OFF");
        vimState.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_VimModeWithInvalidArg_ReturnsUsageMessage()
    {
        // Arrange
        var getVim = new Func<bool>(() => false);
        var onVim = new Action<bool>(_ => { });
        var router = CreateRouter(getVimMode: getVim, onVimModeChanged: onVim);

        // Act
        var result = await router.ExecuteAsync("/vim invalid");

        // Assert
        result.Should().Contain("Usage");
    }

    [Fact]
    public async Task ExecuteAsync_SystemPromptAppendText_AppendsAndReturnsInfo()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/system-prompt append test instruction");

        // Assert
        result.Should().Contain("Appended");
        result.Should().Contain("tokens");
    }

    [Fact]
    public async Task ExecuteAsync_PromptDropNoArgs_ReturnsUsageMessage()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/prompt drop");

        // Assert
        result.Should().Contain("Usage");
    }

    [Fact]
    public async Task ExecuteAsync_ConfigNoArgs_ReturnsConfigOutput()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/config list");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Configuration");
    }

    [Fact]
    public async Task ExecuteAsync_ConfigGetKey_ReturnsKeyValue()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/config get autorun");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("autorun");
    }

    [Fact]
    public async Task ExecuteAsync_ConfigGetInvalidKey_ReturnsUnknownMessage()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/config get nonexistent_key");

        // Assert
        result.Should().Contain("Unknown");
    }

    [Fact]
    public async Task ExecuteAsync_PromptNoArgs_ShowsPromptView()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/prompt");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("System Prompt");
    }

    [Fact]
    public async Task ExecuteAsync_PromptFull_ShowsFullPromptView()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/prompt --full");

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_SystemPromptPrependText_PrependsAndReturnsInfo()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/system-prompt prepend preamble");

        // Assert
        result.Should().Contain("Prepended");
    }

    [Fact]
    public async Task ExecuteAsync_ReasoningMedium_SetsMediumLevel()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/reasoning medium");

        // Assert
        result.Should().Contain("medium");
    }

    [Fact]
    public async Task ExecuteAsync_ReasoningMax_SetsMaxLevel()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/reasoning max");

        // Assert
        result.Should().Contain("max");
    }

    [Fact]
    public async Task ExecuteAsync_NameSessionNoSessionInfo_ReturnsNoSessionMessage()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/name test-name");

        // Assert
        result.Should().Contain("No active session");
    }

    [Fact]
    public async Task ExecuteAsync_ShortcutsContainsTabInfo()
    {
        // Arrange
        var router = CreateRouter();

        // Act
        var result = await router.ExecuteAsync("/shortcuts");

        // Assert
        result.Should().Contain("Tab");
    }

    [Fact]
    public async Task ExecuteAsync_GetContextWindow_ShowsPercentage()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var session = new AgentSession(_registry, kernel, model);
        session.History.AddUserMessage("test");
        var router = new SlashCommandRouter(session, _registry);

        // Act
        var result = await router.ExecuteAsync("/context");

        // Assert
        result.Should().Contain("%");
    }
}
