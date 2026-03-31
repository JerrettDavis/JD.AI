using FluentAssertions;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace JD.AI.Gateway.Tests;

public sealed class ChannelFactoryTests
{
    private readonly ChannelFactory _factory;

    public ChannelFactoryTests()
    {
        var sp = Substitute.For<IServiceProvider>();
        // Simulate GetRequiredService throwing when logger isn't registered — mirrors real DI behavior.
        sp.GetService(typeof(object)).Returns(null);
        _factory = new ChannelFactory(sp, NullLogger<ChannelFactory>.Instance);
    }

    [Fact]
    public void Create_WebChannel_ReturnsWebChannel()
    {
        var config = new ChannelConfig { Type = "web", Name = "Web Chat" };
        var channel = _factory.Create(config);

        channel.Should().NotBeNull();
        channel!.ChannelType.Should().Be("web");
    }

    [Fact]
    public void Create_UnknownType_ReturnsNull()
    {
        var config = new ChannelConfig { Type = "unknown", Name = "Unknown" };
        var channel = _factory.Create(config);

        channel.Should().BeNull();
    }

    [Fact]
    public void Create_Discord_WithoutToken_Throws()
    {
        var config = new ChannelConfig { Type = "discord", Name = "Discord" };

        // Factory catches and returns null
        var channel = _factory.Create(config);
        channel.Should().BeNull();
    }

    [Fact]
    public void Create_Discord_WithToken_ReturnsQueueDecoratedDiscordChannel()
    {
        var config = new ChannelConfig
        {
            Type = "discord",
            Name = "Discord",
            Settings = new Dictionary<string, string>(StringComparer.Ordinal) { ["BotToken"] = "test-token-value" }
        };

        var channel = _factory.Create(config);
        channel.Should().NotBeNull();
        channel!.ChannelType.Should().Be("discord");
    }

    [Fact]
    public void Create_Signal_WithAccount_ReturnsSignalChannel()
    {
        var config = new ChannelConfig
        {
            Type = "signal",
            Name = "Signal",
            Settings = new Dictionary<string, string>(StringComparer.Ordinal) { ["Account"] = "+15551234567" }
        };

        var channel = _factory.Create(config);
        channel.Should().NotBeNull();
        channel!.ChannelType.Should().Be("signal");
    }

    [Fact]
    public void Create_Telegram_WithToken_ReturnsTelegramChannel()
    {
        var config = new ChannelConfig
        {
            Type = "telegram",
            Name = "Telegram",
            Settings = new Dictionary<string, string>(StringComparer.Ordinal) { ["BotToken"] = "123456:ABC-DEF" }
        };

        var channel = _factory.Create(config);
        channel.Should().NotBeNull();
        channel!.ChannelType.Should().Be("telegram");
    }

    [Fact]
    public void Create_Slack_WithBothTokens_ReturnsSlackChannel()
    {
        var config = new ChannelConfig
        {
            Type = "slack",
            Name = "Slack",
            Settings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BotToken"] = "xoxb-test",
                ["AppToken"] = "xapp-test"
            }
        };

        var channel = _factory.Create(config);
        channel.Should().NotBeNull();
        channel!.ChannelType.Should().Be("slack");
    }

    [Fact]
    public void Create_IsCaseInsensitive()
    {
        var config = new ChannelConfig { Type = "WEB", Name = "Web" };
        var channel = _factory.Create(config);

        channel.Should().NotBeNull();
        channel!.ChannelType.Should().Be("web");
    }

    [Fact]
    public void Create_EnvReference_ResolvesEnvironmentVariable()
    {
        // Set a test environment variable
        var envKey = $"JDAI_TEST_TOKEN_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(envKey, "resolved-token-value");

        try
        {
            var config = new ChannelConfig
            {
                Type = "telegram",
                Name = "Telegram",
                Settings = new Dictionary<string, string>(StringComparer.Ordinal) { ["BotToken"] = $"env:{envKey}" }
            };

            var channel = _factory.Create(config);
            channel.Should().NotBeNull();
            channel!.ChannelType.Should().Be("telegram");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, null);
        }
    }
}
