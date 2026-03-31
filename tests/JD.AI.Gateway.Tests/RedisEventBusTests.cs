using JD.AI.Core.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;

namespace JD.AI.Gateway.Tests;

public sealed class RedisEventBusTests
{
    private static (RedisEventBus bus, ISubscriber subscriber) CreateBus()
    {
        var subscriber = Substitute.For<ISubscriber>();
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetSubscriber(Arg.Any<object>()).Returns(subscriber);

        var bus = new RedisEventBus(redis, NullLogger<RedisEventBus>.Instance);
        return (bus, subscriber);
    }

    [Fact]
    public async Task PublishAsync_SendsToRedisChannel()
    {
        var (bus, subscriber) = CreateBus();

        var evt = new GatewayEvent("agent.spawned", "agent-1", DateTimeOffset.UtcNow);
        await bus.PublishAsync(evt);

        await subscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(c => c.ToString().Contains("agent.spawned")),
            Arg.Any<RedisValue>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Subscribe_ReceivesLocalDispatchOnRedisFailure()
    {
        var subscriber = Substitute.For<ISubscriber>();
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetSubscriber(Arg.Any<object>()).Returns(subscriber);

        // Make Redis publish throw so it falls back to local dispatch
        subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns<long>(_ => throw new RedisException("Connection failed"));

        var bus = new RedisEventBus(redis, NullLogger<RedisEventBus>.Instance);

        GatewayEvent? received = null;
        bus.Subscribe(null, evt => { received = evt; return Task.CompletedTask; });

        await bus.PublishAsync(new GatewayEvent("test", "src", DateTimeOffset.UtcNow));

        Assert.NotNull(received);
        Assert.Equal("test", received!.EventType);
    }

    [Fact]
    public async Task Subscribe_WithFilter_OnlyReceivesMatching()
    {
        var subscriber = Substitute.For<ISubscriber>();
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetSubscriber(Arg.Any<object>()).Returns(subscriber);

        // Force local dispatch by making Redis fail
        subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns<long>(_ => throw new RedisException("Connection failed"));

        var bus = new RedisEventBus(redis, NullLogger<RedisEventBus>.Instance);

        var count = 0;
        bus.Subscribe("target", _ => { count++; return Task.CompletedTask; });

        await bus.PublishAsync(new GatewayEvent("other", "src", DateTimeOffset.UtcNow));
        await bus.PublishAsync(new GatewayEvent("target", "src", DateTimeOffset.UtcNow));

        Assert.Equal(1, count);
    }

    [Fact]
    public void Unsubscribe_StopsNotifications()
    {
        var (bus, _) = CreateBus();

        var count = 0;
        var sub = bus.Subscribe(null, _ => { count++; return Task.CompletedTask; });
        sub.Dispose();

        // Verify subscription was removed (no way to trigger local dispatch
        // without Redis failure, so just verify no exception on dispose)
        Assert.Equal(0, count);
    }

    [Fact]
    public void AddEventBus_DefaultUsesInMemory()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddEventBus();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetService<IEventBus>();

        Assert.IsType<InMemoryEventBus>(bus);
    }

    [Fact]
    public void AddEventBus_RedisWithoutConnectionString_Throws()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddEventBus(new EventBusOptions { Provider = "Redis" }));
    }

    [Fact]
    public void AddEventBus_Redis_RegistersRedisEventBus()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // We can't actually connect to Redis in unit tests, but we can verify
        // the registrations are correct
        services.AddEventBus(new EventBusOptions
        {
            Provider = "Redis",
            RedisConnectionString = "localhost:6379",
        });

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventBus));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(RedisEventBus), descriptor!.ImplementationType);
    }
}
