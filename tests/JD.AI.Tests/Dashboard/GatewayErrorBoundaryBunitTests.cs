using Bunit;
using JD.AI.Dashboard.Wasm.Components.Shared;
using Microsoft.AspNetCore.Components;

namespace JD.AI.Tests.Dashboard;

public sealed class GatewayErrorBoundaryBunitTests : DashboardBunitTestContext
{
    [Fact]
    public void GatewayErrorBoundary_WhenHasError_ShowsAlertWithRetryButton()
    {
        var retryCalled = false;
        var cut = RenderWithMudProviders<GatewayErrorBoundary>(p => p
            .Add(c => c.HasError, true)
            .Add(c => c.Message, "Gateway unreachable")
            .Add(c => c.OnRetry, EventCallback.Factory.Create(this, () => retryCalled = true)));

        Assert.NotNull(cut.Find("[data-testid='gateway-error-alert']"));
        Assert.Contains("Gateway unreachable", cut.Find("[data-testid='gateway-error-message']").TextContent);
        var retryBtn = cut.Find("[data-testid='gateway-retry-button']");
        retryBtn.Click();
        Assert.True(retryCalled);
    }

    [Fact]
    public void GatewayErrorBoundary_WhenNoError_RendersChildContent()
    {
        var cut = RenderWithMudProviders<GatewayErrorBoundary>(p => p
            .Add(c => c.HasError, false)
            .Add<RenderFragment>(c => c.ChildContent, builder =>
            {
                builder.AddMarkupContent(0, "<span data-testid='child'>ok</span>");
            }));

        Assert.NotNull(cut.Find("[data-testid='child']"));
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("[data-testid='gateway-error-alert']"));
    }
}
