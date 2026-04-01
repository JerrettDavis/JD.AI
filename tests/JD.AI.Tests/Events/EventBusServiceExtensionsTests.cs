using FluentAssertions;
using JD.AI.Core.Events;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Tests.Events;

/// <summary>
/// Tests for <see cref="EventBusServiceExtensions.AddEventBus"/> DI registration.
/// Verifies: default InMemory registration, Redis conditional, options validation.
/// </summary>
public sealed class EventBusServiceExtensionsTests
{
    [Fact]
    public void AddEventBus_DefaultOptions_ResolvesInMemoryEventBus()
    {
        var services = new ServiceCollection();
        services.AddEventBus();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetService<IEventBus>();

        bus.Should().NotBeNull();
        bus.Should().BeOfType<InMemoryEventBus>();
    }

    [Fact]
    public void AddEventBus_DefaultOptions_MultipleCalls_ResolvesSameInstance()
    {
        var services = new ServiceCollection();
        services.AddEventBus();

        var provider = services.BuildServiceProvider();
        var bus1 = provider.GetService<IEventBus>();
        var bus2 = provider.GetService<IEventBus>();

        bus1.Should().BeSameAs(bus2); // Singleton
    }

    [Fact]
    public void AddEventBus_RedisUnavailable_ResolvesInMemoryEventBus()
    {
        var services = new ServiceCollection();
        services.AddEventBus(new EventBusOptions
        {
            Provider = "Redis",
            RedisConnectionString = "localhost:6379"
        });

        var provider = services.BuildServiceProvider();
        var bus = provider.GetService<IEventBus>();

        // Should fall back to InMemory when Redis not available
        bus.Should().NotBeNull();
        bus.Should().BeOfType<InMemoryEventBus>();
    }

    [Fact]
    public void AddEventBus_RedisExplicitOptions_ThrowsWhenConnectionStringMissing()
    {
        var services = new ServiceCollection();

        var act = () => services.AddEventBus(new EventBusOptions
        {
            Provider = "Redis",
            RedisConnectionString = null
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RedisConnectionString*");
    }

    [Fact]
    public void AddEventBus_InProcessExplicit_ResolvesInMemoryEventBus()
    {
        var services = new ServiceCollection();
        services.AddEventBus(new EventBusOptions { Provider = "InProcess" });

        var provider = services.BuildServiceProvider();
        var bus = provider.GetService<IEventBus>();

        bus.Should().BeOfType<InMemoryEventBus>();
    }

    [Fact]
    public void AddEventBus_UnknownProvider_ResolvesInMemoryEventBus()
    {
        var services = new ServiceCollection();
        services.AddEventBus(new EventBusOptions { Provider = "Unknown" });

        var provider = services.BuildServiceProvider();
        var bus = provider.GetService<IEventBus>();

        bus.Should().BeOfType<InMemoryEventBus>();
    }

    [Fact]
    public async Task AddEventBus_CanPublishAndSubscribe()
    {
        var services = new ServiceCollection();
        services.AddEventBus();
        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        var received = new List<GatewayEvent>();
        using var _ = bus.Subscribe("test.event", evt =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new GatewayEvent("test.event", "test-source", DateTimeOffset.UtcNow, "payload"));

        received.Should().HaveCount(1);
        received[0].SourceId.Should().Be("test-source");
    }

    [Fact]
    public async Task AddEventBus_PublishAsync_AfterCancellation_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddEventBus();
        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var act = async () => await bus.PublishAsync(
            new GatewayEvent("x", "s", DateTimeOffset.UtcNow),
            cts.Token);

        await act().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AddEventBus_PublishAsync_AfterDispose_ThrowsObjectDisposed()
    {
        var services = new ServiceCollection();
        services.AddEventBus();
        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>() as IDisposable;
        bus!.Dispose();

        var act = async () => await ((IEventBus)bus).PublishAsync(
            new GatewayEvent("x", "s", DateTimeOffset.UtcNow));

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}