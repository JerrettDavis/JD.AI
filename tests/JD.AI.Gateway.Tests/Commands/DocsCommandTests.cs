using FluentAssertions;
using JD.AI.Core.Commands;
using JD.AI.Gateway.Commands;

namespace JD.AI.Gateway.Tests.Commands;

public sealed class DocsCommandTests
{
    private static CommandContext MakeContext(string? topic = null) => new()
    {
        CommandName = "docs",
        InvokerId = "user1",
        ChannelId = "ch1",
        ChannelType = "discord",
        Arguments = topic is not null
            ? new Dictionary<string, string>(StringComparer.Ordinal) { ["topic"] = topic }
            : new Dictionary<string, string>(StringComparer.Ordinal),
    };

    [Fact]
    public void DocsCommand_HasCorrectName()
    {
        var cmd = new DocsCommand();
        cmd.Name.Should().Be("docs");
        cmd.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DocsCommand_HasTopicParameter()
    {
        var cmd = new DocsCommand();
        cmd.Parameters.Should().ContainSingle(p => p.Name == "topic" && !p.IsRequired);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTopic_ListsAllSections()
    {
        var cmd = new DocsCommand();
        var result = await cmd.ExecuteAsync(MakeContext());

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("JD.AI Documentation");
        result.Content.Should().Contain("Observability");
        result.Content.Should().Contain("Gateway API");
        result.Content.Should().Contain("Quickstart");
    }

    [Fact]
    public async Task ExecuteAsync_WithObservabilityTopic_ReturnsObservabilityLink()
    {
        var cmd = new DocsCommand();
        var result = await cmd.ExecuteAsync(MakeContext("observability"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("observability.html");
        result.Content.Should().Contain("Observability");
    }

    [Fact]
    public async Task ExecuteAsync_WithHealthTopic_ReturnsObservabilityLink()
    {
        var cmd = new DocsCommand();
        var result = await cmd.ExecuteAsync(MakeContext("health"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("observability.html");
    }

    [Fact]
    public async Task ExecuteAsync_WithTelemetryTopic_ReturnsObservabilityLink()
    {
        var cmd = new DocsCommand();
        var result = await cmd.ExecuteAsync(MakeContext("telemetry"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("observability.html");
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTopic_ReturnsFailure()
    {
        var cmd = new DocsCommand();
        var result = await cmd.ExecuteAsync(MakeContext("xyzzy-unknown-topic-12345"));

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("No documentation found");
    }

    [Fact]
    public async Task ExecuteAsync_TopicMatchIsCaseInsensitive()
    {
        var cmd = new DocsCommand();
        var result = await cmd.ExecuteAsync(MakeContext("OBSERVABILITY"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("observability.html");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTopic_ContainsBaseUrl()
    {
        var cmd = new DocsCommand();
        var result = await cmd.ExecuteAsync(MakeContext());

        result.Content.Should().Contain("jerrettdavis.github.io/JD.AI");
    }
}
