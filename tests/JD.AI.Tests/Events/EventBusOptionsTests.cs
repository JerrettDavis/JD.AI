using FluentAssertions;
using JD.AI.Core.Events;

namespace JD.AI.Tests.Events;

public sealed class EventBusOptionsTests
{
    [Fact]
    public void Default_ProviderIsInProcess()
    {
        var opts = new EventBusOptions();
        opts.Provider.Should().Be("InProcess");
        opts.RedisConnectionString.Should().BeNull();
    }

    [Fact]
    public void SetRedis_Roundtrip()
    {
        var opts = new EventBusOptions
        {
            Provider = "Redis",
            RedisConnectionString = "localhost:6379",
        };
        opts.Provider.Should().Be("Redis");
        opts.RedisConnectionString.Should().Be("localhost:6379");
    }
}
