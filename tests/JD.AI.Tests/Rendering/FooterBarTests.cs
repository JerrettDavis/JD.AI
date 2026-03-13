using System.IO;
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Rendering;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace JD.AI.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="FooterBar"/>.
/// </summary>
public sealed class FooterBarTests
{
    private const string DefaultTemplate =
        "{folder} │ {branch?} │ {pr?} │ {context} │ {provider} │ {model} │ {turns}";

    private static FooterState CreateState() => new(
        WorkingDirectory: "/home/user/app",
        GitBranch: "main",
        PrLink: null,
        ContextTokensUsed: 5_000,
        ContextWindowSize: 200_000,
        Provider: "anthropic",
        Model: "claude-sonnet-4-6",
        TurnCount: 3,
        Mode: PermissionMode.Normal,
        WarnThresholdPercent: 15,
        PluginSegments: []);

    /// <summary>
    /// Renders an <see cref="IRenderable"/> to a plain-text string via a Spectre test console.
    /// </summary>
    private static string RenderToString(IRenderable renderable)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(new StringWriter()),
        });
        console.Write(renderable);
        return ((StringWriter)console.Profile.Out.Writer).ToString().Trim();
    }

    // ── Enabled rendering ─────────────────────────────────────────────────

    [Fact]
    public void ToRenderable_ContainsAllDefaultSegments_WhenStateIsPopulated()
    {
        var footer   = new FooterBar(DefaultTemplate);
        var state    = CreateState();
        var rendered = footer.ToRenderable(state);
        var markup   = RenderToString(rendered);

        // Key rendered values that should appear
        markup.Should().Contain("app");          // folder (shortened path)
        markup.Should().Contain("anthropic");    // provider
        markup.Should().Contain("claude-sonnet-4-6"); // model
        markup.Should().Contain("5.0k/200.0k"); // context
        markup.Should().Contain("turn 3");       // turns
    }

    [Fact]
    public void ToRenderable_OmitsBranch_WhenGitBranchIsNull()
    {
        var footer = new FooterBar(DefaultTemplate);
        var state  = CreateState() with { GitBranch = null };
        var markup = RenderToString(footer.ToRenderable(state));

        // The branch value should be absent from the rendered output
        markup.Should().NotContain("main");
        // The remaining segments should still be present
        markup.Should().Contain("anthropic");
        markup.Should().Contain("claude-sonnet-4-6");
    }

    [Fact]
    public void ToRenderable_ShowsBranch_WhenGitBranchIsPresent()
    {
        var footer = new FooterBar(DefaultTemplate);
        var state  = CreateState(); // GitBranch = "main"
        var markup = RenderToString(footer.ToRenderable(state));

        markup.Should().Contain("main");
    }

    // ── Disabled footer ───────────────────────────────────────────────────

    [Fact]
    public void ToRenderable_ReturnsEmptyRenderable_WhenFooterIsDisabled()
    {
        var footer   = new FooterBar(DefaultTemplate, enabled: false);
        var rendered = footer.ToRenderable(CreateState());

        // Should return an empty Text, not a Markup with content
        rendered.Should().BeOfType<Text>();
        RenderToString(rendered).Should().BeEmpty();
    }
}
