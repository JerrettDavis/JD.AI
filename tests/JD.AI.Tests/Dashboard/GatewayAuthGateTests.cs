using Bunit;
using JD.AI.Dashboard.Wasm.Components;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace JD.AI.Tests.Dashboard;

public sealed class GatewayAuthGateTests : DashboardBunitTestContext
{
    public GatewayAuthGateTests()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetVoidResult();
    }

    [Fact]
    public void GatewayAuthGate_WhenDisconnected_ShowsForm()
    {
        var signalR = Substitute.For<ISignalRService>();
        signalR.IsConnected.Returns(false);
        Services.AddSingleton(signalR);

        var cut = RenderWithMudProviders<GatewayAuthGate>();

        Assert.NotNull(cut.Find("[data-testid='auth-gate']"));
        Assert.NotNull(cut.Find("[data-testid='gateway-url-input']"));
        Assert.NotNull(cut.Find("[data-testid='gateway-token-input']"));
        Assert.NotNull(cut.Find("[data-testid='connect-button']"));
    }

    [Fact]
    public void GatewayAuthGate_WhenConnected_HidesForm()
    {
        var signalR = Substitute.For<ISignalRService>();
        signalR.IsConnected.Returns(true);
        Services.AddSingleton(signalR);

        var cut = RenderWithMudProviders<GatewayAuthGate>();

        Assert.Empty(cut.FindAll("[data-testid='auth-gate']"));
    }

    [Fact]
    public void GatewayAuthGate_PreFillsDefaultUrl()
    {
        var signalR = Substitute.For<ISignalRService>();
        signalR.IsConnected.Returns(false);
        Services.AddSingleton(signalR);

        var cut = RenderWithMudProviders<GatewayAuthGate>();

        // Default URL is in the rendered markup (MudTextField renders value in input or data attribute)
        Assert.Contains("ws://127.0.0.1:18789", cut.Markup);
    }

    [Fact]
    public void GatewayAuthGate_PreFillsUrlFromLocalStorage()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        JSInterop.Setup<string?>("localStorage.getItem",
            inv => string.Equals((string)inv.Arguments[0]!, "jd-gateway-url", StringComparison.Ordinal))
            .SetResult("ws://192.168.1.10:18789");

        var signalR = Substitute.For<ISignalRService>();
        signalR.IsConnected.Returns(false);
        Services.AddSingleton(signalR);

        var cut = RenderWithMudProviders<GatewayAuthGate>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("ws://192.168.1.10:18789", cut.Markup);
        });
    }

    [Fact]
    public async Task GatewayAuthGate_ConnectButtonCallsConnectAsync()
    {
        var signalR = Substitute.For<ISignalRService>();
        signalR.IsConnected.Returns(false);
        Services.AddSingleton(signalR);

        var cut = RenderWithMudProviders<GatewayAuthGate>();

        await cut.Find("[data-testid='connect-button']").ClickAsync(new());

        await signalR.Received().ConnectAsync(
            Arg.Is<string>(s => s.StartsWith("ws://")),
            Arg.Any<string?>());
    }
}
