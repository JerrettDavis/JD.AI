using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

public sealed class ChatRendererTests : IDisposable
{
    public ChatRendererTests()
    {
        ChatRenderer.ApplyTheme(TuiTheme.DefaultDark);
        ChatRenderer.SetOutputStyle(OutputStyle.Rich);
    }

    public void Dispose()
    {
        ChatRenderer.ApplyTheme(TuiTheme.DefaultDark);
        ChatRenderer.SetOutputStyle(OutputStyle.Rich);
    }

    // ── BuildWelcomeBody edge cases ──────────────────────────────────

    [Fact]
    public void BuildWelcomeBody_NullDetails_OmitsOptionalLines()
    {
        var result = ChatRenderer.BuildWelcomeBody(
            "model", "provider", 1,
            indicators: null,
            details: null,
            settings: new WelcomePanelSettings
            {
                ShowWorkingDirectory = true,
                ShowVersion = true,
                ShowMotd = true,
            });

        result.Should().NotContain("CWD:");
        result.Should().NotContain("Version:");
        result.Should().NotContain("MoTD:");
    }

    [Fact]
    public void BuildWelcomeBody_AlwaysContainsHeader()
    {
        var result = ChatRenderer.BuildWelcomeBody(
            "model", "provider", 1, null, null,
            new WelcomePanelSettings
            {
                ShowModelSummary = false,
                ShowServices = false,
            });

        result.Should().Contain("jdai");
        result.Should().Contain("/help");
    }

    [Fact]
    public void BuildWelcomeBody_EmptyDetailStrings_OmitsLines()
    {
        var result = ChatRenderer.BuildWelcomeBody(
            "model", "provider", 1, null,
            new WelcomeBannerDetails(WorkingDirectory: "", Version: "  ", Motd: null),
            new WelcomePanelSettings
            {
                ShowWorkingDirectory = true,
                ShowVersion = true,
                ShowMotd = true,
            });

        result.Should().NotContain("CWD:");
        result.Should().NotContain("Version:");
        result.Should().NotContain("MoTD:");
    }

    // ── BuildIndicatorsLine edge cases ───────────────────────────────

    [Fact]
    public void BuildIndicatorsLine_EmptyList_ReturnsEmpty()
    {
        var result = ChatRenderer.BuildIndicatorsLine([]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildIndicatorsLine_BlankNameOrValue_Filtered()
    {
        var indicators = new List<WelcomeIndicator>
        {
            new("", "running", IndicatorState.Healthy),
            new("Daemon", "", IndicatorState.Healthy),
            new("  ", "ok", IndicatorState.Neutral),
        };

        var result = ChatRenderer.BuildIndicatorsLine(indicators);
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildIndicatorsLine_SingleIndicator_NoSeparator()
    {
        var indicators = new List<WelcomeIndicator>
        {
            new("Gateway", "online", IndicatorState.Healthy),
        };

        var result = ChatRenderer.BuildIndicatorsLine(indicators);
        result.Should().Contain("Gateway:");
        result.Should().NotContain(" | Gateway"); // no separator before first
    }

    [Fact]
    public void BuildIndicatorsLine_MultipleIndicators_SeparatedByPipe()
    {
        var indicators = new List<WelcomeIndicator>
        {
            new("Daemon", "running", IndicatorState.Healthy),
            new("Gateway", "online", IndicatorState.Neutral),
        };

        var result = ChatRenderer.BuildIndicatorsLine(indicators);
        result.Should().Contain(" | ");
        result.Should().Contain("Daemon:");
        result.Should().Contain("Gateway:");
    }

    // ── FormatElapsedMetric edge cases ───────────────────────────────

    [Theory]
    [InlineData(0L, "0.0s")]
    [InlineData(1_500L, "1.5s")]
    [InlineData(59_999L, "60.0s")]
    [InlineData(60_000L, "1m 0s")]
    [InlineData(90_000L, "1m 30s")]
    [InlineData(3_600_000L, "60m 0s")]
    public void FormatElapsedMetric_BoundaryValues(long ms, string expected) =>
        ChatRenderer.FormatElapsedMetric(ms).Should().Be(expected);

    // ── FormatBytes edge cases ───────────────────────────────────────

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1.0 KB")]
    [InlineData(1_048_575L, "1024.0 KB")]
    [InlineData(1_048_576L, "1.0 MB")]
    public void FormatBytes_BoundaryValues(long bytes, string expected) =>
        ChatRenderer.FormatBytes(bytes).Should().Be(expected);

    // ── EscapeJsonString edge cases ──────────────────────────────────

    [Fact]
    public void EscapeJsonString_PlainText_ReturnsSame() =>
        ChatRenderer.EscapeJsonString("hello world").Should().Be("hello world");

    [Fact]
    public void EscapeJsonString_TabsAndBackslashes_Escaped()
    {
        var result = ChatRenderer.EscapeJsonString("path\\to\\file\ttab");
        result.Should().Contain("\\");
    }

    // ── ApplyTheme / SetOutputStyle ──────────────────────────────────

    [Theory]
    [InlineData(TuiTheme.DefaultDark)]
    [InlineData(TuiTheme.Dracula)]
    [InlineData(TuiTheme.Monokai)]
    [InlineData(TuiTheme.SolarizedDark)]
    [InlineData(TuiTheme.SolarizedLight)]
    [InlineData(TuiTheme.Nord)]
    [InlineData(TuiTheme.OneDark)]
    [InlineData(TuiTheme.CatppuccinMocha)]
    [InlineData(TuiTheme.Gruvbox)]
    [InlineData(TuiTheme.HighContrast)]
    public void ApplyTheme_SetsCurrentTheme(TuiTheme theme)
    {
        ChatRenderer.ApplyTheme(theme);
        ChatRenderer.CurrentTheme.Should().Be(theme);
    }

    [Theory]
    [InlineData(OutputStyle.Rich)]
    [InlineData(OutputStyle.Plain)]
    [InlineData(OutputStyle.Compact)]
    public void SetOutputStyle_SetsCurrentOutputStyle(OutputStyle style)
    {
        ChatRenderer.SetOutputStyle(style);
        ChatRenderer.CurrentOutputStyle.Should().Be(style);
    }

    [Theory]
    [InlineData(PermissionMode.Normal, "Normal")]
    [InlineData(PermissionMode.Plan, "Plan (read-only)")]
    [InlineData(PermissionMode.AcceptEdits, "Auto-edit")]
    [InlineData(PermissionMode.BypassAll, "Autopilot")]
    public void GetModeBarLabel_UsesExpectedLabelPerPermissionMode(PermissionMode mode, string expectedLabel)
    {
        var (label, _) = ChatRenderer.GetModeBarLabel(mode);

        label.Should().Be(expectedLabel);
    }

    // ── RenderBanner edge cases ──────────────────────────────────────

    [Fact]
    public void RenderBanner_WithValidInput_DoesNotThrow()
    {
        var action = () => ChatRenderer.RenderBanner(
            "gpt-4", "OpenAI", 10,
            indicators: null,
            details: null,
            welcomeSettings: null);

        action.Should().NotThrow();
    }

    [Fact]
    public void RenderBanner_WithIndicators_DoesNotThrow()
    {
        var indicators = new List<WelcomeIndicator>
        {
            new("Gateway", "online", IndicatorState.Healthy),
        };

        var action = () => ChatRenderer.RenderBanner(
            "gpt-4", "OpenAI", 10,
            indicators: indicators,
            details: null,
            welcomeSettings: null);

        action.Should().NotThrow();
    }

    [Fact]
    public void RenderBanner_WithDetails_DoesNotThrow()
    {
        var details = new WelcomeBannerDetails(
            WorkingDirectory: "/home/user",
            Version: "1.0.0",
            Motd: "Welcome!");

        var action = () => ChatRenderer.RenderBanner(
            "gpt-4", "OpenAI", 10,
            indicators: null,
            details: details,
            welcomeSettings: null);

        action.Should().NotThrow();
    }

    // ── RenderError edge cases ───────────────────────────────────────

    [Fact]
    public void RenderError_WithMessage_DoesNotThrow()
    {
        var action = () => ChatRenderer.RenderError("Something went wrong");
        action.Should().NotThrow();
    }

    [Fact]
    public void RenderError_WithEmptyMessage_DoesNotThrow()
    {
        var action = () => ChatRenderer.RenderError("");
        action.Should().NotThrow();
    }

    // ── RenderAssistantMessage edge cases ────────────────────────────

    [Fact]
    public void RenderAssistantMessage_WithContent_DoesNotThrow()
    {
        var action = () => ChatRenderer.RenderAssistantMessage("This is the assistant response");
        action.Should().NotThrow();
    }

    [Fact]
    public void RenderAssistantMessage_WithEmptyContent_DoesNotThrow()
    {
        var action = () => ChatRenderer.RenderAssistantMessage("");
        action.Should().NotThrow();
    }

    // ── BeginThinking / EndThinking edge cases ──────────────────────

    [Fact]
    public void BeginThinking_DoesNotThrow()
    {
        var action = () => ChatRenderer.BeginThinking();
        action.Should().NotThrow();
    }

    [Fact]
    public void EndThinking_DoesNotThrow()
    {
        var action = () => ChatRenderer.EndThinking();
        action.Should().NotThrow();
    }

    [Fact]
    public void ConfirmWorkflow_DoesNotThrow()
    {
        var action = () => ChatRenderer.ConfirmWorkflow("Test workflow");
        action.Should().NotThrow();
    }
}
