using Bunit;
using JD.AI.Dashboard.Wasm.Pages;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Tests.Dashboard;

public sealed class ChatPageBunitTests : DashboardBunitTestContext
{
    [Fact]
    public void Chat_HeaderDoesNotContainChannelSelector()
    {
        var api = CreateApiClient(_ => JsonResponse("[]")); // empty agents
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Chat>();

        Assert.Throws<Bunit.ElementNotFoundException>(() =>
            cut.Find("[data-testid='channel-selector']"));
    }

    [Fact]
    public void Chat_WhileAgentsLoading_ShowsAgentSelectorSkeleton()
    {
        var tcs = new TaskCompletionSource<HttpResponseMessage>();
        var api = CreateAsyncApiClient(_ => tcs.Task);
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Chat>();

        // While loading, skeleton should be shown
        Assert.NotNull(cut.Find("[data-testid='agent-selector-skeleton']"));
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("[data-testid='agent-selector']"));
    }

    [Fact]
    public void Chat_WhenAgentLoadFails_ShowsErrorBoundary()
    {
        var api = CreateApiClient(_ => throw new HttpRequestException("Connection refused"));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Chat>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='gateway-error-alert']"));
        }, timeout: TimeSpan.FromSeconds(3));
    }
}
