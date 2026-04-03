using JD.AI.Gateway.Client;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Gateway.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGatewayClient_RegistersTypedClientAndTrimmedSignalRSingleton()
    {
        var services = new ServiceCollection();

        services.AddGatewayClient("https://gateway.test/root///");

        using var provider = services.BuildServiceProvider();
        var typedClient = provider.GetRequiredService<GatewayHttpClient>();
        var signalRClient = provider.GetRequiredService<GatewaySignalRClient>();

        Assert.NotNull(typedClient);
        Assert.NotNull(signalRClient);
        Assert.False(signalRClient.IsConnected);
        Assert.Null(signalRClient.ConnectionError);
    }

    [Fact]
    public void AddGatewayClient_ReturnsSameServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        var returned = services.AddGatewayClient("https://gateway.test");

        Assert.Same(services, returned);
    }
}
