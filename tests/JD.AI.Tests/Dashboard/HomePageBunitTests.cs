using Bunit;
using JD.AI.Dashboard.Wasm.Pages;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Tests.Dashboard;

public sealed class HomePageBunitTests : DashboardBunitTestContext
{
    [Fact]
    public void Home_WhenGatewayStatusLoads_RendersStatsBridgeAndEmptyActivityState()
    {
        var api = CreateApiClient(request =>
        {
            Assert.Equal("http://localhost/api/gateway/status", request.RequestUri!.ToString());
            return JsonResponse(
                """
                {
                  "status": "running",
                  "uptime": "2026-04-02T12:00:00Z",
                  "channels": [
                    { "channelType": "discord", "displayName": "Discord", "isConnected": true },
                    { "channelType": "signal", "displayName": "Signal", "isConnected": false }
                  ],
                  "agents": [
                    { "id": "jdai-default", "provider": "openai", "model": "gpt-5" },
                    { "id": "jdai-research", "provider": "anthropic", "model": "claude-opus" }
                  ],
                  "routes": { "discord": "jdai-default" },
                  "openClaw": {
                    "enabled": true,
                    "connected": false,
                    "defaultMode": "Sidecar",
                    "overrideActive": false,
                    "overrideChannels": ["discord"],
                    "registeredAgents": ["jdai-default", "jdai-research"]
                  }
                }
                """);
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Home>();

        var statCards = cut.FindAll("[data-testid='stat-card']");
        Assert.Equal(4, statCards.Count);
        Assert.Contains("2", cut.Find("[data-testid='stat-card-agents'] [data-testid='stat-value']").TextContent);
        Assert.Contains("1", cut.Find("[data-testid='stat-card-channels'] [data-testid='stat-value']").TextContent);
        Assert.Contains("Running", cut.Find("[data-testid='stat-card-gateway'] [data-testid='stat-value']").TextContent);

        // Legacy bridge table must not appear
        Assert.Empty(cut.FindAll("[data-testid='openclaw-bridge']"));

        Assert.NotNull(cut.Find("[data-testid='activity-empty']"));
    }

    [Fact]
    public void Home_WhenGatewayStatusRequestFails_ShowsErrorBoundaryAndNoBridgeTable()
    {
        var api = CreateApiClient(_ => throw new HttpRequestException("boom"));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Home>();

        // Error boundary must be visible, no bridge table
        Assert.NotNull(cut.Find("[data-testid='gateway-error-alert']"));
        Assert.Empty(cut.FindAll("[data-testid='openclaw-bridge']"));
    }

    [Fact]
    public void Home_WhenStatusLoads_DoesNotRenderLegacyStatCard()
    {
        var api = CreateApiClient(request =>
            JsonResponse("""{"status":"running","agents":[],"channels":[],"routes":{}}"""));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Home>();

        // Should not find a stat card with data-name="openclaw"
        var legacyCard = cut.FindAll("[data-testid='stat-card'][data-name='openclaw']");
        Assert.Empty(legacyCard);
        // Should not find the bridge management table
        Assert.Throws<Bunit.ElementNotFoundException>(() =>
            cut.Find("[data-testid='openclaw-bridge']"));
    }

    [Fact]
    public void Home_WhenStatusLoads_RendersGatewayStatusCard()
    {
        var api = CreateApiClient(request =>
            JsonResponse("""{"status":"running","agents":[],"channels":[],"routes":{}}"""));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Home>();

        Assert.NotNull(cut.Find("[data-testid='stat-card-gateway']"));
    }

    [Fact]
    public void Home_WhenGatewayDown_ShowsErrorBoundaryWithRetry()
    {
        var api = CreateApiClient(_ => throw new HttpRequestException("Connection refused"));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Home>();

        Assert.NotNull(cut.Find("[data-testid='gateway-error-alert']"));
        Assert.NotNull(cut.Find("[data-testid='gateway-retry-button']"));
    }
}
