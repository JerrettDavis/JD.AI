using FluentAssertions;
using JD.AI.Channels.OpenClaw;
using JD.AI.Channels.OpenClaw.Routing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json;

namespace JD.AI.Tests.Channels.OpenClaw;

/// <summary>
/// Unit tests for OpenClaw mode handlers: Passthrough, Intercept, Sidecar, and Proxy.
/// Tests focus on behavior and routing logic without requiring live connections.
/// </summary>
public sealed class OpenClawModeHandlerTests
{
    // ── Passthrough Mode Handler tests ─────────────────────────────────────────

    [Fact]
    public async Task PassthroughHandler_Always_ReturnsFalse()
    {
        var logger = Substitute.For<ILogger<PassthroughModeHandler>>();
        var handler = new PassthroughModeHandler(logger);

        var evt = new OpenClawEvent
        {
            EventName = "agent",
            Payload = JsonSerializer.SerializeToElement(new { sessionKey = "test", stream = "user" })
        };

        var result = await handler.HandleAsync(
            evt,
            "discord",
            new OpenClawChannelRouteConfig { Mode = OpenClawRoutingMode.Passthrough },
            (OpenClawBridgeChannel)null!,
            async (_, _) => "response",
            CancellationToken.None);

        result.Should().BeFalse("Passthrough should always return false");
    }

    [Fact]
    public async Task PassthroughHandler_WithNullPayload_ReturnsFalse()
    {
        var logger = Substitute.For<ILogger<PassthroughModeHandler>>();
        var handler = new PassthroughModeHandler(logger);

        var evt = new OpenClawEvent { EventName = "agent", Payload = null };

        var result = await handler.HandleAsync(
            evt,
            "discord",
            new OpenClawChannelRouteConfig { Mode = OpenClawRoutingMode.Passthrough },
            (OpenClawBridgeChannel)null!,
            async (_, _) => "response",
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task PassthroughHandler_DoesNotCallMessageProcessor()
    {
        var logger = Substitute.For<ILogger<PassthroughModeHandler>>();
        var handler = new PassthroughModeHandler(logger);

        var processorCalled = false;
        async Task<string?> FakeProcessor(string _sessionKey, string _content) { processorCalled = true; return ""; }

        var evt = new OpenClawEvent
        {
            EventName = "agent",
            Payload = JsonSerializer.SerializeToElement(new { sessionKey = "test", stream = "user", data = new { text = "hello" } })
        };

        await handler.HandleAsync(
            evt,
            "discord",
            new OpenClawChannelRouteConfig(),
            (OpenClawBridgeChannel)null!,
            FakeProcessor,
            CancellationToken.None);

        processorCalled.Should().BeFalse("Passthrough should not process messages");
    }

    // ── Intercept Mode Handler tests ───────────────────────────────────────────

    [Fact]
    public async Task InterceptHandler_WithValidUserMessage_CallsMessageProcessor()
    {
        var logger = Substitute.For<ILogger<InterceptModeHandler>>();
        var handler = new InterceptModeHandler(logger);

        var processorCalled = false;
        async Task<string?> FakeProcessor(string _sessionKey2, string _content2) { processorCalled = true; return "response"; }

        var evt = new OpenClawEvent
        {
            EventName = "agent",
            Payload = JsonSerializer.SerializeToElement(new
            {
                sessionKey = "session-123",
                stream = "user",
                data = new { text = "hello world" }
            })
        };

        // bridge is null — handlers use bridge?.IsConnected so null means "not connected"
        var bridge = (OpenClawBridgeChannel)null!;

        var result = await handler.HandleAsync(
            evt,
            "discord",
            new OpenClawChannelRouteConfig(),
            bridge,
            FakeProcessor,
            CancellationToken.None);

        result.Should().BeTrue();
        processorCalled.Should().BeTrue("Intercept should call messageProcessor");
    }

    [Fact]
    public async Task InterceptHandler_WithEmptyResponse_ReturnsTrue()
    {
        var logger = Substitute.For<ILogger<InterceptModeHandler>>();
        var handler = new InterceptModeHandler(logger);

        async Task<string?> FakeProcessor(string _sessionKey3, string _content3) => "";

        var evt = new OpenClawEvent
        {
            EventName = "agent",
            Payload = JsonSerializer.SerializeToElement(new
            {
                sessionKey = "session-123",
                stream = "user",
                data = new { text = "hello" }
            })
        };

        var result = await handler.HandleAsync(
            evt,
            "discord",
            new OpenClawChannelRouteConfig(),
            (OpenClawBridgeChannel)null!,
            FakeProcessor,
            CancellationToken.None);

        result.Should().BeTrue("Should return true even with empty response");
    }

    [Fact]
    public async Task InterceptHandler_WithNullPayload_ReturnsFalse()
    {
        var logger = Substitute.For<ILogger<InterceptModeHandler>>();
        var handler = new InterceptModeHandler(logger);

        var evt = new OpenClawEvent { EventName = "agent", Payload = null };

        var result = await handler.HandleAsync(
            evt,
            "discord",
            new OpenClawChannelRouteConfig(),
            (OpenClawBridgeChannel)null!,
            async (_, _) => "response",
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task InterceptHandler_WithNoSessionKey_StillHandlesIfContentPresent()
    {
        // The handler extracts sessionKey as "" when missing — it does NOT require a non-empty key
        var logger = Substitute.For<ILogger<InterceptModeHandler>>();
        var handler = new InterceptModeHandler(logger);

        var evt = new OpenClawEvent
        {
            EventName = "agent",
            Payload = JsonSerializer.SerializeToElement(new
            {
                stream = "user",
                data = new { text = "hello" }
                // sessionKey is missing — defaults to ""
            })
        };

        var result = await handler.HandleAsync(
            evt,
            "discord",
            new OpenClawChannelRouteConfig(),
            (OpenClawBridgeChannel)null!,
            async (_, _) => "response",
            CancellationToken.None);

        // Handler processes any user message regardless of empty sessionKey
        result.Should().BeTrue("handler returns true when content is present, even without sessionKey");
    }

    // ── Sidecar Mode Handler tests ─────────────────────────────────────────────

    [Fact]
    public async Task SidecarHandler_WithCommandPrefix_TriggersAndStripsPrefix()
    {
        var logger = Substitute.For<ILogger<SidecarModeHandler>>();
        var handler = new SidecarModeHandler(logger);

        var capturedContent = "";
        async Task<string?> FakeProcessor(string _, string content) { capturedContent = content; return "response"; }

        var evt = new OpenClawEvent
        {
            EventName = "agent",
            Payload = JsonSerializer.SerializeToElement(new
            {
                sessionKey = "session-123",
                stream = "user",
                data = new { text = "/jdai what is 2+2" }
            })
        };

        // bridge is null — handlers use bridge?.IsConnected so null means "not connected"
        var bridge = (OpenClawBridgeChannel)null!;

        var result = await handler.HandleAsync(
            evt,
            "discord",
            new OpenClawChannelRouteConfig { CommandPrefix = "/jdai" },
            bridge,
            FakeProcessor,
            CancellationToken.None);

        result.Should().BeTrue();
        capturedContent.Should().Be("what is 2+2");
    }

    [Fact]
    public async Task SidecarHandler_WithoutTrigger_ReturnsFalse()
    {
        var logger = Substitute.For<ILogger<SidecarModeHandler>>();
        var handler = new SidecarModeHandler(logger);

        var processorCalled = false;
        async Task<string?> FakeProcessor(string _sessionKey2, string _content2) { processorCalled = true; return "response"; }

        var evt = new OpenClawEvent
        {
            EventName = "agent",
            Payload = JsonSerializer.SerializeToElement(new
            {
                sessionKey = "session-123",
                stream = "user",
                data = new { text = "this doesn't match any prefix" }
            })
        };

        var result = await handler.HandleAsync(
            evt,
            "discord",
            new OpenClawChannelRouteConfig { CommandPrefix = "/jdai" },
            (OpenClawBridgeChannel)null!,
            FakeProcessor,
            CancellationToken.None);

        result.Should().BeFalse("Should not trigger without prefix");
        processorCalled.Should().BeFalse();
    }

    [Fact]
    public async Task SidecarHandler_WithDiscordMention_StripsAndTriggers()
    {
        var logger = Substitute.For<ILogger<SidecarModeHandler>>();
        var handler = new SidecarModeHandler(logger);

        var capturedContent = "";
        async Task<string?> FakeProcessor(string _, string content) { capturedContent = content; return "response"; }

        var evt = new OpenClawEvent
        {
            EventName = "agent",
            Payload = JsonSerializer.SerializeToElement(new
            {
                sessionKey = "session-123",
                stream = "user",
                data = new { text = "<@123456789> /jdai hello there" }
            })
        };

        // bridge is null — handlers use bridge?.IsConnected so null means "not connected"
        var bridge = (OpenClawBridgeChannel)null!;

        var result = await handler.HandleAsync(
            evt,
            "discord",
            new OpenClawChannelRouteConfig { CommandPrefix = "/jdai" },
            bridge,
            FakeProcessor,
            CancellationToken.None);

        result.Should().BeTrue();
        capturedContent.Should().Be("hello there");
    }

    [Fact]
    public async Task SidecarHandler_WithRegexTrigger_MatchesAndProcesses()
    {
        var logger = Substitute.For<ILogger<SidecarModeHandler>>();
        var handler = new SidecarModeHandler(logger);

        var processorCalled = false;
        async Task<string?> FakeProcessor(string _sessionKey2, string _content2) { processorCalled = true; return "response"; }

        var evt = new OpenClawEvent
        {
            EventName = "agent",
            Payload = JsonSerializer.SerializeToElement(new
            {
                sessionKey = "session-123",
                stream = "user",
                data = new { text = "Hey bot, what's up?" }
            })
        };

        // bridge is null — handlers use bridge?.IsConnected so null means "not connected"
        var bridge = (OpenClawBridgeChannel)null!;

        var result = await handler.HandleAsync(
            evt,
            "discord",
            new OpenClawChannelRouteConfig { TriggerPattern = "^Hey bot" },
            bridge,
            FakeProcessor,
            CancellationToken.None);

        result.Should().BeTrue();
        processorCalled.Should().BeTrue();
    }

    // ── Proxy Mode Handler tests ───────────────────────────────────────────────

    [Fact]
    public async Task ProxyHandler_WithValidMessage_ProcessesAndInjects()
    {
        var logger = Substitute.For<ILogger<ProxyModeHandler>>();
        var handler = new ProxyModeHandler(logger);

        var processorCalled = false;
        async Task<string?> FakeProcessor(string _sessionKey4, string _content4) { processorCalled = true; return "proxy response"; }

        var evt = new OpenClawEvent
        {
            EventName = "agent",
            Payload = JsonSerializer.SerializeToElement(new
            {
                sessionKey = "proxy-session",
                stream = "user",
                data = new { text = "any message" }
            })
        };

        // bridge is null — handlers use bridge?.IsConnected so null means "not connected"
        var bridge = (OpenClawBridgeChannel)null!;

        var result = await handler.HandleAsync(
            evt,
            "proxy-channel",
            new OpenClawChannelRouteConfig { Mode = OpenClawRoutingMode.Proxy },
            bridge,
            FakeProcessor,
            CancellationToken.None);

        result.Should().BeTrue();
        processorCalled.Should().BeTrue("Proxy should always process");
    }

    [Fact]
    public async Task ProxyHandler_WithEmptyResponse_ReturnsTrue()
    {
        var logger = Substitute.For<ILogger<ProxyModeHandler>>();
        var handler = new ProxyModeHandler(logger);

        async Task<string?> FakeProcessor(string _sessionKey3, string _content3) => null;

        var evt = new OpenClawEvent
        {
            EventName = "agent",
            Payload = JsonSerializer.SerializeToElement(new
            {
                sessionKey = "proxy-session",
                stream = "user",
                data = new { text = "any message" }
            })
        };

        var result = await handler.HandleAsync(
            evt,
            "proxy-channel",
            new OpenClawChannelRouteConfig(),
            (OpenClawBridgeChannel)null!,
            FakeProcessor,
            CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ProxyHandler_WithNullPayload_ReturnsFalse()
    {
        var logger = Substitute.For<ILogger<ProxyModeHandler>>();
        var handler = new ProxyModeHandler(logger);

        var evt = new OpenClawEvent { EventName = "agent", Payload = null };

        var result = await handler.HandleAsync(
            evt,
            "proxy-channel",
            new OpenClawChannelRouteConfig(),
            (OpenClawBridgeChannel)null!,
            async (_, _) => "response",
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProxyHandler_WithoutSessionKey_StillHandlesIfContentPresent()
    {
        // ProxyHandler extracts sessionKey as "" when missing — does NOT require a non-empty key
        var logger = Substitute.For<ILogger<ProxyModeHandler>>();
        var handler = new ProxyModeHandler(logger);

        var evt = new OpenClawEvent
        {
            EventName = "agent",
            Payload = JsonSerializer.SerializeToElement(new
            {
                stream = "user",
                data = new { text = "message" }
                // sessionKey missing — defaults to ""
            })
        };

        var result = await handler.HandleAsync(
            evt,
            "proxy-channel",
            new OpenClawChannelRouteConfig(),
            (OpenClawBridgeChannel)null!,
            async (_, _) => "response",
            CancellationToken.None);

        // ProxyHandler processes any user message regardless of empty sessionKey
        result.Should().BeTrue("handler returns true when content is present, even without sessionKey");
    }

    [Fact]
    public async Task ProxyHandler_WithDirectContentField_ExtractsAndProcesses()
    {
        var logger = Substitute.For<ILogger<ProxyModeHandler>>();
        var handler = new ProxyModeHandler(logger);

        var capturedSessionKey = "";
        var capturedContent = "";
        async Task<string?> FakeProcessor(string sessionKey, string content)
        {
            capturedSessionKey = sessionKey;
            capturedContent = content;
            return "response";
        }

        var evt = new OpenClawEvent
        {
            EventName = "agent",
            Payload = JsonSerializer.SerializeToElement(new
            {
                sessionKey = "direct-test",
                content = "direct message"
            })
        };

        // bridge is null — handlers use bridge?.IsConnected so null means "not connected"
        var bridge = (OpenClawBridgeChannel)null!;

        var result = await handler.HandleAsync(
            evt,
            "proxy-channel",
            new OpenClawChannelRouteConfig(),
            bridge,
            FakeProcessor,
            CancellationToken.None);

        result.Should().BeTrue();
        capturedSessionKey.Should().Be("direct-test");
        capturedContent.Should().Be("direct message");
    }
}
