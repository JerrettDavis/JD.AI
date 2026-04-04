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
        Assert.Contains("Connected", cut.Find("[data-testid='stat-card-openclaw'] [data-testid='stat-value']").TextContent);

        var bridge = cut.Find("[data-testid='openclaw-bridge']");
        Assert.Contains("Registered Agents", bridge.TextContent);
        Assert.Contains("jdai-default, jdai-research", bridge.TextContent);

        Assert.NotNull(cut.Find("[data-testid='activity-empty']"));
    }

    [Fact]
    public void Home_WhenGatewayStatusRequestFails_ShowsZeroedFallbackWithoutBridge()
    {
        var api = CreateApiClient(_ => throw new HttpRequestException("boom"));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Home>();

        var statValues = cut.FindAll("[data-testid='stat-value']").Select(x => x.TextContent.Trim()).ToArray();
        Assert.Contains("0", statValues);
        Assert.Contains("Offline", statValues);
        Assert.Empty(cut.FindAll("[data-testid='openclaw-bridge']"));
        Assert.NotNull(cut.Find("[data-testid='activity-empty']"));
    }
}
