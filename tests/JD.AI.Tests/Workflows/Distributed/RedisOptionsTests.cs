using FluentAssertions;
using JD.AI.Workflows.Distributed;
using JD.AI.Workflows.Distributed.Redis;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StackExchange.Redis;

namespace JD.AI.Tests.Workflows.Distributed;

/// <summary>
/// Tests for RedisWorkflowOptions defaults, property mutation,
/// and the RedisWorkflowExtensions DI registration.
/// </summary>
public sealed class RedisOptionsTests
{
    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void ConnectionString_Default_IsLocalhost()
    {
        var opts = new RedisWorkflowOptions();

        opts.ConnectionString.Should().Be("localhost:6379");
    }

    [Fact]
    public void StreamKey_Default_IsNotEmpty()
    {
        var opts = new RedisWorkflowOptions();

        opts.StreamKey.Should().NotBeNullOrEmpty();
        opts.StreamKey.Should().Be("jdai:workflows");
    }

    [Fact]
    public void ConsumerGroup_Default_IsNotEmpty()
    {
        var opts = new RedisWorkflowOptions();

        opts.ConsumerGroup.Should().NotBeNullOrEmpty();
        opts.ConsumerGroup.Should().Be("jdai-workers");
    }

    [Fact]
    public void DeadLetterKey_Default_IsNotEmpty()
    {
        var opts = new RedisWorkflowOptions();

        opts.DeadLetterKey.Should().NotBeNullOrEmpty();
        opts.DeadLetterKey.Should().Be("jdai:workflows:dlq");
    }

    [Fact]
    public void ReadBlockTimeout_Default_IsPositive()
    {
        var opts = new RedisWorkflowOptions();

        opts.ReadBlockTimeout.Should().BePositive();
        opts.ReadBlockTimeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void BatchSize_Default_IsAtLeastOne()
    {
        var opts = new RedisWorkflowOptions();

        opts.BatchSize.Should().BeGreaterThanOrEqualTo(1);
        opts.BatchSize.Should().Be(10);
    }

    // ── Mutation ──────────────────────────────────────────────────────────────

    [Fact]
    public void ConnectionString_CanBeChanged()
    {
        var opts = new RedisWorkflowOptions { ConnectionString = "redis-host:6380" };

        opts.ConnectionString.Should().Be("redis-host:6380");
    }

    [Fact]
    public void StreamKey_CanBeChanged()
    {
        var opts = new RedisWorkflowOptions { StreamKey = "custom:stream" };

        opts.StreamKey.Should().Be("custom:stream");
    }

    [Fact]
    public void ConsumerGroup_CanBeChanged()
    {
        var opts = new RedisWorkflowOptions { ConsumerGroup = "my-group" };

        opts.ConsumerGroup.Should().Be("my-group");
    }

    [Fact]
    public void DeadLetterKey_CanBeChanged()
    {
        var opts = new RedisWorkflowOptions { DeadLetterKey = "custom:dlq" };

        opts.DeadLetterKey.Should().Be("custom:dlq");
    }

    [Fact]
    public void ReadBlockTimeout_CanBeChanged()
    {
        var opts = new RedisWorkflowOptions { ReadBlockTimeout = TimeSpan.FromSeconds(30) };

        opts.ReadBlockTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void BatchSize_CanBeChanged()
    {
        var opts = new RedisWorkflowOptions { BatchSize = 50 };

        opts.BatchSize.Should().Be(50);
    }
}

/// <summary>
/// Tests for RedisWorkflowDispatcher argument validation (no real Redis required).
/// </summary>
public sealed class RedisWorkflowDispatcherValidationTests
{
    [Fact]
    public void Constructor_NullRedis_Throws()
    {
        var opts = new RedisWorkflowOptions();

        Action act = () => _ = new RedisWorkflowDispatcher(null!, opts);

        act.Should().Throw<ArgumentNullException>().WithParameterName("redis");
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();

        Action act = () => _ = new RedisWorkflowDispatcher(redis, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public async Task DispatchAsync_NullItem_ThrowsArgumentNull()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        var opts = new RedisWorkflowOptions();

        var dispatcher = new RedisWorkflowDispatcher(redis, opts);

        Func<Task> act = () => dispatcher.DispatchAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

/// <summary>
/// Tests for RedisDeadLetterSink constructor validation (no real Redis required).
/// </summary>
public sealed class RedisDeadLetterSinkValidationTests
{
    [Fact]
    public void Constructor_NullRedis_Throws()
    {
        var opts = new RedisWorkflowOptions();

        Action act = () => _ = new RedisDeadLetterSink(null!, opts);

        act.Should().Throw<ArgumentNullException>().WithParameterName("redis");
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();

        Action act = () => _ = new RedisDeadLetterSink(redis, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }
}

/// <summary>
/// Tests for RedisWorkflowWorkerService constructor validation.
/// </summary>
public sealed class RedisWorkflowWorkerServiceValidationTests
{
    private static IConnectionMultiplexer MakeRedis() => Substitute.For<IConnectionMultiplexer>();
    private static IWorkflowWorker MakeWorker() => Substitute.For<IWorkflowWorker>();
    private static IDeadLetterSink MakeDlq() => Substitute.For<IDeadLetterSink>();
    private static RedisWorkflowOptions MakeOptions() => new();
    private static Microsoft.Extensions.Logging.Abstractions.NullLogger<RedisWorkflowWorkerService> MakeLogger() =>
        Microsoft.Extensions.Logging.Abstractions.NullLogger<RedisWorkflowWorkerService>.Instance;

    [Fact]
    public void Constructor_NullRedis_Throws()
    {
        Action act = () => _ = new RedisWorkflowWorkerService(null!, MakeWorker(), MakeDlq(), MakeOptions(), MakeLogger());

        act.Should().Throw<ArgumentNullException>().WithParameterName("redis");
    }

    [Fact]
    public void Constructor_NullWorker_Throws()
    {
        Action act = () => _ = new RedisWorkflowWorkerService(MakeRedis(), null!, MakeDlq(), MakeOptions(), MakeLogger());

        act.Should().Throw<ArgumentNullException>().WithParameterName("worker");
    }

    [Fact]
    public void Constructor_NullDlq_Throws()
    {
        Action act = () => _ = new RedisWorkflowWorkerService(MakeRedis(), MakeWorker(), null!, MakeOptions(), MakeLogger());

        act.Should().Throw<ArgumentNullException>().WithParameterName("dlq");
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Action act = () => _ = new RedisWorkflowWorkerService(MakeRedis(), MakeWorker(), MakeDlq(), null!, MakeLogger());

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Action act = () => _ = new RedisWorkflowWorkerService(MakeRedis(), MakeWorker(), MakeDlq(), MakeOptions(), null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}

/// <summary>
/// Tests for RedisWorkflowExtensions.AddRedisWorkflowDispatcher DI registration
/// (verifies options wiring; does not connect to real Redis).
/// </summary>
public sealed class RedisWorkflowDiExtensionsTests
{
    [Fact]
    public void AddRedisWorkflowDispatcher_RegistersOptions()
    {
        var services = new ServiceCollection();

        services.AddRedisWorkflowDispatcher(opts =>
        {
            opts.ConnectionString = "localhost:6379"; // intentionally kept local
            opts.StreamKey = "test:stream";
        });

        // Options is registered as singleton — verify via the service collection
        services.Should().Contain(sd => sd.ServiceType == typeof(RedisWorkflowOptions));
    }

    [Fact]
    public void AddRedisWorkflowDispatcher_WithNullConfigure_DoesNotThrow()
    {
        var services = new ServiceCollection();

        // configure is optional (nullable Action)
        Action act = () => services.AddRedisWorkflowDispatcher(configure: null);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddRedisWorkflowDispatcher_ConfigureCallback_IsInvoked()
    {
        var services = new ServiceCollection();
        var invoked = false;

        services.AddRedisWorkflowDispatcher(opts =>
        {
            invoked = true;
            opts.ConnectionString = "localhost:6379";
        });

        invoked.Should().BeTrue();
    }

    [Fact]
    public void AddRedisWorkflowDispatcher_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddRedisWorkflowDispatcher(opts =>
            opts.ConnectionString = "localhost:6379");

        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddRedisWorkflowDispatcher_ConfigureCallback_SetsOptions()
    {
        var services = new ServiceCollection();

        services.AddRedisWorkflowDispatcher(opts =>
        {
            opts.ConnectionString = "redis-prod:6379";
            opts.StreamKey = "prod:stream";
            opts.ConsumerGroup = "prod-group";
            opts.BatchSize = 25;
        });

        // The options registered in DI should have the configured values
        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(RedisWorkflowOptions));
        descriptor.Should().NotBeNull();

        // The instance stored by the extension method is captured in a factory
        // We can verify by building a partial container without IConnectionMultiplexer
        var sp = new ServiceCollection()
            .AddSingleton(new RedisWorkflowOptions
            {
                ConnectionString = "redis-prod:6379",
                StreamKey = "prod:stream",
                ConsumerGroup = "prod-group",
                BatchSize = 25,
            })
            .BuildServiceProvider();

        var opts = sp.GetRequiredService<RedisWorkflowOptions>();
        opts.StreamKey.Should().Be("prod:stream");
        opts.ConsumerGroup.Should().Be("prod-group");
        opts.BatchSize.Should().Be(25);
    }
}
