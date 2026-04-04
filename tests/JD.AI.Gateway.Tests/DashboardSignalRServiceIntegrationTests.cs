using FluentAssertions;
using JD.AI.Core.Events;
using JD.AI.Dashboard.Wasm.Models;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Gateway.Tests;

public sealed class DashboardSignalRServiceIntegrationTests : IClassFixture<GatewayTestFactory>
{
    private readonly GatewayTestFactory _factory;
    private readonly HttpClient _client;

    public DashboardSignalRServiceIntegrationTests(GatewayTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ConnectAsync_WhenGatewayPublishesEvent_RaisesActivityEvent()
    {
        await using var service = CreateService();
        var received = new TaskCompletionSource<ActivityEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.OnActivityEvent += (_, e) => received.TrySetResult(e.Activity);

        await service.ConnectAsync();

        var bus = _factory.Services.GetRequiredService<IEventBus>();
        await bus.PublishAsync(new GatewayEvent(
            "gateway.started",
            "orchestrator",
            DateTimeOffset.UtcNow,
            "Gateway started"));

        var activity = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        activity.EventType.Should().Be("gateway.started");
        activity.SourceId.Should().Be("orchestrator");
        activity.Message.Should().Be("Gateway started");
    }

    [Fact]
    public async Task ConnectAsync_WhenGatewayPublishesChannelEvent_RaisesChannelStatusChanged()
    {
        await using var service = CreateService();
        var received = new TaskCompletionSource<ChannelStatusEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.OnChannelStatusChanged += (_, e) => received.TrySetResult(e);

        await service.ConnectAsync();

        var bus = _factory.Services.GetRequiredService<IEventBus>();
        await bus.PublishAsync(new GatewayEvent(
            "channel.connected",
            "discord",
            DateTimeOffset.UtcNow,
            "Discord connected"));

        var update = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        update.Channel.Should().Be("discord");
        update.Connected.Should().BeTrue();
    }

    private SignalRService CreateService() =>
        new(_client.BaseAddress!.ToString(), options =>
        {
            options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
        });
}
