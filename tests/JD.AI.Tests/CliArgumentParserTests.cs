using JD.AI.Startup;

namespace JD.AI.Tests;

public sealed class CliArgumentParserTests
{
    [Fact]
    public async Task ParseAsync_Onboard_SubcommandIsDetected()
    {
        var opts = await CliArgumentParser.ParseAsync(["onboard"]);

        Assert.Equal("onboard", opts.Subcommand);
        Assert.Empty(opts.SubcommandArgs);
    }

    [Fact]
    public async Task ParseAsync_Wizard_SubcommandArgsAreCaptured()
    {
        var opts = await CliArgumentParser.ParseAsync(["wizard", "--global", "--provider", "openai"]);

        Assert.Equal("wizard", opts.Subcommand);
        Assert.Equal(["--global", "--provider", "openai"], opts.SubcommandArgs);
    }

    [Fact]
    public async Task ParseAsync_RegularFlags_DoNotBecomeSubcommand()
    {
        var opts = await CliArgumentParser.ParseAsync(["--provider", "openai", "--model", "gpt-4.1"]);

        Assert.Null(opts.Subcommand);
        Assert.Equal("openai", opts.CliProvider);
        Assert.Equal("gpt-4.1", opts.CliModel);
    }

    [Fact]
    public async Task ParseAsync_RoutingFlags_AreCaptured()
    {
        var opts = await CliArgumentParser.ParseAsync(
        [
            "--routing-strategy", "capability",
            "--routing-fallback-providers", "openai,openrouter",
            "--routing-capabilities", "tools,vision"
        ]);

        Assert.Equal("capability", opts.RoutingStrategy);
        Assert.Equal(["openai", "openrouter"], opts.RoutingFallbackProviders);
        Assert.Equal(["tools", "vision"], opts.RoutingCapabilities);
    }
}