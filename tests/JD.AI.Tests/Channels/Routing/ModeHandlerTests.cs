using System.Text.Json;
using FluentAssertions;
using JD.AI.Channels.OpenClaw;
using JD.AI.Channels.OpenClaw.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace JD.AI.Tests.Channels.Routing;

public sealed class ModeHandlerTests
{
    private static OpenClawEvent MakeUserChatEvent(string content, string channel = "discord", string sessionKey = "agent:main:main")
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            stream = "user",
            channel,
            sessionKey,
            data = new { text = content },
        });

        return new OpenClawEvent
        {
            EventName = "chat",
            Payload = JsonDocument.Parse(payloadJson).RootElement,
        };
    }

    private static OpenClawEvent MakeAssistantChatEvent(string content) =>
        new()
        {
            EventName = "chat",
            Payload = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                stream = "assistant",
                data = new { text = content },
            })).RootElement,
        };

    // --- Passthrough ---

    [Fact]
    public async Task Passthrough_NeverHandlesMessages()
    {
        var handler = new PassthroughModeHandler(NullLogger<PassthroughModeHandler>.Instance);
        var evt = MakeUserChatEvent("hello");
        var config = new OpenClawChannelRouteConfig { Mode = OpenClawRoutingMode.Passthrough };

        var handled = await handler.HandleAsync(evt, "discord", config, null!, (_, _) => Task.FromResult<string?>("test"), default);

        handled.Should().BeFalse();
    }

    // --- Intercept ---

    [Fact]
    public async Task Intercept_HandlesUserMessages()
    {
        var handler = new InterceptModeHandler(NullLogger<InterceptModeHandler>.Instance);
        var evt = MakeUserChatEvent("hello world");
        var config = new OpenClawChannelRouteConfig { Mode = OpenClawRoutingMode.Intercept };

        string? capturedContent = null;
        var processor = new Func<string, string, Task<string?>>((session, content) =>
        {
            capturedContent = content;
            return Task.FromResult<string?>(null); // null response skips send
        });

        // Use a mock bridge that just tracks calls
        var rpcConfig = new OpenClawConfig
        {
            WebSocketUrl = "ws://localhost:0",
            DeviceId = "test",
            DeviceToken = "test",
            GatewayToken = "test",
            PublicKeyPem = "-----BEGIN PUBLIC KEY-----\ntest\n-----END PUBLIC KEY-----",
            PrivateKeyPem = "-----BEGIN PRIVATE KEY-----\ntest\n-----END PRIVATE KEY-----",
        };
        var rpc = new OpenClawRpcClient(rpcConfig, NullLogger<OpenClawRpcClient>.Instance);
        var bridge = new OpenClawBridgeChannel(rpc, NullLogger<OpenClawBridgeChannel>.Instance, rpcConfig);

        // Can't call RPC without connection, but HandleAsync should still process and catch the error
        var handled = await handler.HandleAsync(evt, "discord", config, bridge, processor, default);

        handled.Should().BeTrue();
        capturedContent.Should().Be("hello world");
    }

    [Fact]
    public async Task Intercept_IgnoresAssistantMessages()
    {
        var handler = new InterceptModeHandler(NullLogger<InterceptModeHandler>.Instance);
        var evt = MakeAssistantChatEvent("bot response");
        var config = new OpenClawChannelRouteConfig { Mode = OpenClawRoutingMode.Intercept };

        var handled = await handler.HandleAsync(evt, "discord", config, null!, (_, _) => Task.FromResult<string?>("test"), default);

        handled.Should().BeFalse();
    }

    // --- Sidecar ---

    [Fact]
    public async Task Sidecar_OnlyHandlesMatchingPrefix()
    {
        var handler = new SidecarModeHandler(NullLogger<SidecarModeHandler>.Instance);
        var config = new OpenClawChannelRouteConfig
        {
            Mode = OpenClawRoutingMode.Sidecar,
            CommandPrefix = "/jdai",
        };

        // Message with prefix — should be handled (return null to skip send)
        string? capturedContent = null;
        var evtMatch = MakeUserChatEvent("/jdai what's the weather?");
        var handled = await handler.HandleAsync(evtMatch, "discord", config, null!,
            (_, content) => { capturedContent = content; return Task.FromResult<string?>(null); }, default);

        handled.Should().BeTrue();
        capturedContent.Should().Be("what's the weather?");

        // Message without prefix — should NOT be handled
        var evtNoMatch = MakeUserChatEvent("regular message");
        var handled2 = await handler.HandleAsync(evtNoMatch, "discord", config, null!,
            (_, _) => Task.FromResult<string?>(null), default);

        handled2.Should().BeFalse();
    }

    [Fact]
    public async Task Sidecar_SupportsRegexTrigger()
    {
        var handler = new SidecarModeHandler(NullLogger<SidecarModeHandler>.Instance);
        var config = new OpenClawChannelRouteConfig
        {
            Mode = OpenClawRoutingMode.Sidecar,
            TriggerPattern = @"@jdai\b",
        };

        var evtMatch = MakeUserChatEvent("hey @jdai help me");
        var handled = await handler.HandleAsync(evtMatch, "signal", config, null!,
            (_, _) => Task.FromResult<string?>(null), default);

        handled.Should().BeTrue();

        var evtNoMatch = MakeUserChatEvent("hey everyone");
        var handled2 = await handler.HandleAsync(evtNoMatch, "signal", config, null!,
            (_, _) => Task.FromResult<string?>(null), default);

        handled2.Should().BeFalse();
    }

    // --- Proxy ---

    [Fact]
    public async Task Proxy_HandlesAllUserMessages()
    {
        var handler = new ProxyModeHandler(NullLogger<ProxyModeHandler>.Instance);
        var evt = MakeUserChatEvent("process this");
        var config = new OpenClawChannelRouteConfig { Mode = OpenClawRoutingMode.Proxy };

        string? capturedContent = null;
        var handled = await handler.HandleAsync(evt, "signal", config, null!,
            (_, content) => { capturedContent = content; return Task.FromResult<string?>(null); }, default);

        handled.Should().BeTrue();
        capturedContent.Should().Be("process this");
    }

    // --- Config ---

    [Fact]
    public void RoutingConfig_DefaultValues()
    {
        var config = new OpenClawRoutingConfig();

        config.DefaultMode.Should().Be(OpenClawRoutingMode.Passthrough);
        config.AutoConnect.Should().BeTrue();
        config.Channels.Should().BeEmpty();
        config.AgentProfiles.Should().BeEmpty();
    }

    [Fact]
    public void AgentProfileConfig_DefaultValues()
    {
        var profile = new OpenClawAgentProfileConfig();

        profile.Provider.Should().Be("claude-code");
        profile.Model.Should().Be("claude-sonnet-4-5");
        profile.MaxTurns.Should().Be(50);
        profile.Tools.Should().Contain("file");
    }
}
