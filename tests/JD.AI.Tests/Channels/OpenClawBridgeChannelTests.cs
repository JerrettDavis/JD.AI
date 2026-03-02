using FluentAssertions;
using JD.AI.Channels.OpenClaw;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace JD.AI.Tests.Channels;

public sealed class OpenClawBridgeChannelTests
{
    private readonly OpenClawConfig _config = new()
    {
        WebSocketUrl = "ws://localhost:19999",
        InstanceName = "test",
        SessionKey = "agent:test:main",
        DeviceId = "abc123",
        DeviceToken = "tok123",
        GatewayToken = "gw-tok-456",
        PublicKeyPem = "-----BEGIN PUBLIC KEY-----\ntest\n-----END PUBLIC KEY-----",
        PrivateKeyPem = "-----BEGIN PRIVATE KEY-----\ntest\n-----END PRIVATE KEY-----",
    };

    [Fact]
    public void ChannelType_IsOpenClaw()
    {
        var rpc = new OpenClawRpcClient(_config, NullLogger<OpenClawRpcClient>.Instance);
        var channel = new OpenClawBridgeChannel(
            rpc, NullLogger<OpenClawBridgeChannel>.Instance, _config);

        channel.ChannelType.Should().Be("openclaw");
    }

    [Fact]
    public void DisplayName_IncludesInstanceName()
    {
        var rpc = new OpenClawRpcClient(_config, NullLogger<OpenClawRpcClient>.Instance);
        var channel = new OpenClawBridgeChannel(
            rpc, NullLogger<OpenClawBridgeChannel>.Instance, _config);

        channel.DisplayName.Should().Contain("test");
    }

    [Fact]
    public void IsConnected_DefaultsFalse()
    {
        var rpc = new OpenClawRpcClient(_config, NullLogger<OpenClawRpcClient>.Instance);
        var channel = new OpenClawBridgeChannel(
            rpc, NullLogger<OpenClawBridgeChannel>.Instance, _config);

        channel.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Config_DefaultValues()
    {
        var config = new OpenClawConfig();

        config.WebSocketUrl.Should().Be("ws://localhost:18789");
        config.InstanceName.Should().Be("local");
        config.SessionKey.Should().Be("agent:main:main");
    }

    [Fact]
    public void RpcResponse_GetPayload_Deserializes()
    {
        var json = System.Text.Json.JsonDocument.Parse("""{"key":"agent:main:main","kind":"direct"}""");
        var response = new RpcResponse
        {
            Ok = true,
            Payload = json.RootElement,
        };

        var session = response.GetPayload<OpenClawSession>();
        session.Should().NotBeNull();
        session!.Key.Should().Be("agent:main:main");
        session.Kind.Should().Be("direct");
    }
}
