using FluentAssertions;
using JD.AI.Core.Commands;
using JD.AI.Core.Providers;
using JD.AI.Gateway.Commands;
using NSubstitute;

namespace JD.AI.Gateway.Tests.Commands;

public sealed class ProvidersCommandTests
{
    private static readonly CommandContext Context = new()
    {
        CommandName = "providers",
        InvokerId = "user123",
        ChannelId = "ch456",
        ChannelType = "discord",
    };

    [Fact]
    public void Metadata_IsExpected()
    {
        var command = new ProvidersCommand(Substitute.For<IProviderRegistry>());

        command.Name.Should().Be("providers");
        command.Description.Should().Be("Lists all detected AI providers and their status.");
        command.Parameters.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoProvidersDetected_ShowsEmptyState()
    {
        var registry = CreateRegistry([]);
        var command = new ProvidersCommand(registry);

        var result = await command.ExecuteAsync(Context);

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("**AI Providers**");
        result.Content.Should().Contain("No providers detected.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderIsOnline_ShowsModels()
    {
        var registry = CreateRegistry(
        [
            new ProviderInfo(
                "OpenAI",
                true,
                null,
                [new ProviderModelInfo("gpt-5.3-codex", "GPT-5.3 Codex", "OpenAI")])
        ]);
        var command = new ProvidersCommand(registry);

        var result = await command.ExecuteAsync(Context);

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("🟢 Online **OpenAI** — 1 model(s)");
        result.Content.Should().Contain("• `GPT-5.3 Codex`");
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderIsOffline_ShowsStatusWithoutModels()
    {
        var registry = CreateRegistry(
        [
            new ProviderInfo("Claude", false, "missing API key", [])
        ]);
        var command = new ProvidersCommand(registry);

        var result = await command.ExecuteAsync(Context);

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("🔴 Offline **Claude** — 0 model(s)");
        result.Content.Should().Contain("_missing API key_");
        result.Content.Should().NotContain("• `");
    }

    private static IProviderRegistry CreateRegistry(IReadOnlyList<ProviderInfo> providers)
    {
        var registry = Substitute.For<IProviderRegistry>();
        registry
            .DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(providers));
        return registry;
    }
}
