using System.IO;
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Rendering;
using Spectre.Console;

namespace JD.AI.Tests.Rendering;

/// <summary>
/// End-to-end tests: FooterStateProvider -> FooterState -> FooterTemplate -> FooterBar -> rendered output.
/// </summary>
public sealed class FooterIntegrationTests
{
    private static string Render(FooterBar bar, FooterState state)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(new StringWriter()),
        });
        console.Profile.Width = 200;
        console.Write(bar.ToRenderable(state));
        return ((StringWriter)console.Profile.Out.Writer).ToString().Trim();
    }

    [Fact]
    public void FullPipeline_DefaultTemplate_RendersCorrectly()
    {
        var provider = new FooterStateProvider("/home/user/project");
        provider.Update("anthropic", "claude-sonnet-4-6", 12_000, 200_000, 5,
            PermissionMode.Normal, 15);
        provider.SetGitInfo("main", null);

        var bar = new FooterBar(new FooterSettings().Template);
        var output = Render(bar, provider.CurrentState);

        output.Should().Contain("main");
        output.Should().Contain("12.0k/200.0k");
        output.Should().Contain("anthropic");
        output.Should().Contain("turn 5");
    }

    [Fact]
    public void FullPipeline_ContextWarning_AppearsAtThreshold()
    {
        var provider = new FooterStateProvider("/tmp/test");
        provider.Update("anthropic", "claude-sonnet-4-6", 180_000, 200_000, 10,
            PermissionMode.Normal, 15);

        var bar = new FooterBar("{context} │ {compact?}");
        var output = Render(bar, provider.CurrentState);

        output.Should().Contain("remaining");
    }

    [Fact]
    public void FullPipeline_PluginSegment_RenderedInTemplate()
    {
        var provider = new FooterStateProvider("/tmp/test");
        provider.Update("anthropic", "claude-sonnet-4-6", 1_000, 200_000, 1,
            PermissionMode.Normal, 15);
        provider.AddPluginSegment("deploy", "prod-v3");

        var bar = new FooterBar("{model} │ {plugin:deploy?}");
        var output = Render(bar, provider.CurrentState);

        output.Should().Contain("prod-v3");
    }

    [Fact]
    public void FullPipeline_CustomTemplate_RespectsOrder()
    {
        var provider = new FooterStateProvider("/tmp/test");
        provider.Update("openai", "gpt-4o", 50_000, 128_000, 7,
            PermissionMode.Plan, 15);

        var bar = new FooterBar("{model} │ {turns} │ {mode?}");
        var output = Render(bar, provider.CurrentState);

        output.Should().Contain("gpt-4o");
        output.Should().Contain("turn 7");
        output.Should().Contain("Plan");
    }

    [Fact]
    public void FullPipeline_DisabledFooter_RendersNothing()
    {
        var provider = new FooterStateProvider("/tmp/test");
        provider.Update("anthropic", "claude-sonnet-4-6", 1_000, 200_000, 1,
            PermissionMode.Normal, 15);

        var bar = new FooterBar(new FooterSettings().Template, enabled: false);
        var output = Render(bar, provider.CurrentState);

        output.Should().BeEmpty();
    }
}
