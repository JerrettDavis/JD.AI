using System.Text.Json;
using FluentAssertions;
using JD.AI.Channels.OpenClaw;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JD.AI.IntegrationTests;

/// <summary>
/// Integration tests for the OpenClaw WebSocket JSON-RPC bridge.
/// These tests require a running OpenClaw gateway on localhost:18789
/// with a paired device identity at ~/.openclaw/identity/.
/// </summary>
[Collection("OpenClaw")]
public sealed class OpenClawBridgeIntegrationTests : IAsyncDisposable
{
    private OpenClawRpcClient? _rpc;
    private OpenClawBridgeChannel? _channel;

    private static OpenClawConfig CreateConfig()
    {
        var config = new OpenClawConfig
        {
            WebSocketUrl = "ws://127.0.0.1:18789",
            InstanceName = "local",
            SessionKey = "agent:main:main",
        };

        // Load device identity from the default OpenClaw state directory
        var stateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw");
        var devicePath = Path.Combine(stateDir, "identity", "device.json");
        var authPath = Path.Combine(stateDir, "identity", "device-auth.json");

        if (!File.Exists(devicePath) || !File.Exists(authPath))
            return config;

        using var deviceDoc = JsonDocument.Parse(File.ReadAllText(devicePath));
        config.DeviceId = deviceDoc.RootElement.GetProperty("deviceId").GetString() ?? "";
        config.PublicKeyPem = deviceDoc.RootElement.GetProperty("publicKeyPem").GetString() ?? "";
        config.PrivateKeyPem = deviceDoc.RootElement.GetProperty("privateKeyPem").GetString() ?? "";

        using var authDoc = JsonDocument.Parse(File.ReadAllText(authPath));
        if (authDoc.RootElement.TryGetProperty("tokens", out var tokens)
            && tokens.TryGetProperty("operator", out var op)
            && op.TryGetProperty("token", out var token))
        {
            config.DeviceToken = token.GetString() ?? "";
        }

        // Load gateway shared token
        var configPath = Path.Combine(stateDir, "openclaw.json");
        if (File.Exists(configPath))
        {
            using var configDoc = JsonDocument.Parse(File.ReadAllText(configPath));
            if (configDoc.RootElement.TryGetProperty("gateway", out var gw)
                && gw.TryGetProperty("auth", out var gwAuth)
                && gwAuth.TryGetProperty("token", out var gwToken))
            {
                config.GatewayToken = gwToken.GetString() ?? "";
            }
        }

        return config;
    }

    private static bool IsOpenClawAvailable()
    {
        var config = CreateConfig();
        if (string.IsNullOrEmpty(config.DeviceId)
            || string.IsNullOrEmpty(config.PrivateKeyPem)
            || string.IsNullOrEmpty(config.DeviceToken))
            return false;

        // Check if OpenClaw is actually reachable
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            var uri = new Uri(config.WebSocketUrl.Replace("ws://", "http://"));
            tcp.Connect(uri.Host, uri.Port);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [SkippableFact]
    public async Task RpcClient_Connects_And_Authenticates()
    {
        Skip.IfNot(IsOpenClawAvailable(), "OpenClaw not available");

        var config = CreateConfig();
        _rpc = new OpenClawRpcClient(config, NullLogger<OpenClawRpcClient>.Instance);

        await _rpc.ConnectAsync();

        _rpc.IsConnected.Should().BeTrue();
    }

    [SkippableFact]
    public async Task SessionsList_Returns_Sessions()
    {
        Skip.IfNot(IsOpenClawAvailable(), "OpenClaw not available");

        var config = CreateConfig();
        _rpc = new OpenClawRpcClient(config, NullLogger<OpenClawRpcClient>.Instance);
        await _rpc.ConnectAsync();

        var response = await _rpc.RequestAsync("sessions.list", new { });

        response.Ok.Should().BeTrue();
        response.Payload.Should().NotBeNull();
        response.Payload!.Value.TryGetProperty("sessions", out var sessions).Should().BeTrue();
        sessions.GetArrayLength().Should().BeGreaterThan(0);
    }

    [SkippableFact]
    public async Task ChannelsStatus_Returns_Channels()
    {
        Skip.IfNot(IsOpenClawAvailable(), "OpenClaw not available");

        var config = CreateConfig();
        _rpc = new OpenClawRpcClient(config, NullLogger<OpenClawRpcClient>.Instance);
        await _rpc.ConnectAsync();

        var response = await _rpc.RequestAsync("channels.status", new { });

        response.Ok.Should().BeTrue();
        response.Payload.Should().NotBeNull();
        response.Payload!.Value.TryGetProperty("channels", out _).Should().BeTrue();
    }

    [SkippableFact]
    public async Task BridgeChannel_Connects_Via_WebSocket()
    {
        Skip.IfNot(IsOpenClawAvailable(), "OpenClaw not available");

        var config = CreateConfig();
        _rpc = new OpenClawRpcClient(config, NullLogger<OpenClawRpcClient>.Instance);
        _channel = new OpenClawBridgeChannel(
            _rpc, NullLogger<OpenClawBridgeChannel>.Instance, config);

        await _channel.ConnectAsync();

        _channel.IsConnected.Should().BeTrue();
        _channel.ChannelType.Should().Be("openclaw");
    }

    [SkippableFact]
    public async Task BridgeChannel_ListSessions_Works()
    {
        Skip.IfNot(IsOpenClawAvailable(), "OpenClaw not available");

        var config = CreateConfig();
        _rpc = new OpenClawRpcClient(config, NullLogger<OpenClawRpcClient>.Instance);
        _channel = new OpenClawBridgeChannel(
            _rpc, NullLogger<OpenClawBridgeChannel>.Instance, config);

        await _channel.ConnectAsync();

        var sessions = await _channel.ListSessionsAsync();
        sessions.Ok.Should().BeTrue();
    }

    [SkippableFact]
    public async Task BridgeChannel_GetChannelStatus_ShowsRunningChannels()
    {
        Skip.IfNot(IsOpenClawAvailable(), "OpenClaw not available");

        var config = CreateConfig();
        _rpc = new OpenClawRpcClient(config, NullLogger<OpenClawRpcClient>.Instance);
        _channel = new OpenClawBridgeChannel(
            _rpc, NullLogger<OpenClawBridgeChannel>.Instance, config);

        await _channel.ConnectAsync();

        var status = await _channel.GetChannelStatusAsync();
        status.Ok.Should().BeTrue();

        // Verify at least one channel is running
        var channels = status.Payload!.Value.GetProperty("channels");
        channels.EnumerateObject().Should().NotBeEmpty();
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        else if (_rpc is not null)
            await _rpc.DisposeAsync();
    }
}
