using Bunit;
using JD.AI.Dashboard.Wasm.Pages;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Tests.Dashboard;

public sealed class SettingsPageBunitTests : DashboardBunitTestContext
{
    private GatewayApiClient BuildFullConfigApiClient() =>
        CreateApiClient(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("api/gateway/config/raw"))
                return JsonResponse(
                    """
                    {
                      "server": { "host": "127.0.0.1", "port": 15790, "verbose": true },
                      "auth": { "enabled": true, "apiKeys": [] },
                      "rateLimit": { "enabled": true, "maxRequestsPerMinute": 120 },
                      "providers": [
                        { "name": "openai", "enabled": true, "settings": {} }
                      ],
                      "agents": [
                        { "id": "jdai-default", "provider": "openai", "model": "gpt-5",
                          "autoSpawn": true, "maxTurns": 0, "tools": [],
                          "parameters": { "stopSequences": [] } }
                      ],
                      "channels": [
                        { "type": "discord", "name": "Discord", "enabled": true,
                          "autoConnect": true, "settings": {} }
                      ],
                      "routing": { "defaultAgentId": "jdai-default", "rules": [] },
                      "openClaw": { "enabled": false, "webSocketUrl": "", "autoConnect": false,
                                    "defaultMode": "Sidecar", "channels": {}, "registerAgents": [] }
                    }
                    """);
            if (url.Contains("api/providers/openai/models"))
                return JsonResponse("[{\"id\":\"gpt-5\",\"displayName\":\"GPT-5\",\"provider\":\"openai\"}]");
            if (url.Contains("api/config/schema"))
                return JsonResponse("{\"sections\":[]}");
            if (url.Contains("api/config/current"))
                return JsonResponse("{}");
            return JsonResponse("[]");
        });

    [Fact]
    public void Settings_WhenConfigLoadFails_ShowsErrorState()
    {
        var api = CreateApiClient(_ => throw new HttpRequestException("gateway offline"));
        Services.AddSingleton(api);

        var cut = RenderWithMudProviders<Settings>();

        var error = cut.Find("[data-testid='settings-error']");
        Assert.Contains("Unable to load gateway configuration", error.TextContent);
    }

    [Fact]
    public void Settings_WhenConfigLoads_RendersSettingsTabs()
    {
        var api = BuildFullConfigApiClient();
        Services.AddSingleton(api);

        var cut = RenderWithMudProviders<Settings>();

        var tabs = cut.Find("[data-testid='settings-tabs']");
        Assert.Contains("AI & Agents", tabs.TextContent);
        Assert.Contains("Communication", tabs.TextContent);
        Assert.Contains("Config", tabs.TextContent);
        Assert.Contains("Logs", tabs.TextContent);
    }

    [Fact]
    public void Settings_WhenConfigLoads_ShowsAiAgentsTab()
    {
        var api = BuildFullConfigApiClient();
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<Settings>();
        cut.WaitForAssertion(() =>
            Assert.Contains("AI & Agents", cut.Find("[data-testid='settings-tabs']").TextContent));
    }

    [Fact]
    public void Settings_WhenConfigLoads_ShowsCommunicationTab()
    {
        var api = BuildFullConfigApiClient();
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<Settings>();
        cut.WaitForAssertion(() =>
            Assert.Contains("Communication", cut.Find("[data-testid='settings-tabs']").TextContent));
    }

    [Fact]
    public void Settings_WhenConfigLoads_ShowsConfigTab()
    {
        var api = BuildFullConfigApiClient();
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<Settings>();
        cut.WaitForAssertion(() =>
            Assert.Contains("Config", cut.Find("[data-testid='settings-tabs']").TextContent));
    }

    [Fact]
    public void Settings_WhenConfigLoads_ShowsLogsTab()
    {
        var api = BuildFullConfigApiClient();
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<Settings>();
        cut.WaitForAssertion(() =>
            Assert.Contains("Logs", cut.Find("[data-testid='settings-tabs']").TextContent));
    }

    [Fact]
    public void Settings_WhenConfigLoads_ShowsSkeletonPanel()
    {
        // Api that never completes
        var tcs = new TaskCompletionSource<HttpResponseMessage>();
        var api = CreateApiClient(_ => tcs.Task.GetAwaiter().GetResult());
        Services.AddSingleton(api);

        // Render without awaiting load completion
        var cut = RenderWithMudProviders<Settings>();

        Assert.NotNull(cut.Find("[data-testid='skeleton-panel']"));
    }

    [Fact]
    public void Settings_WhenConfigLoadFails_ShowsRetryButton()
    {
        var api = CreateApiClient(_ => throw new HttpRequestException("gateway offline"));
        Services.AddSingleton(api);

        var cut = RenderWithMudProviders<Settings>();

        Assert.NotNull(cut.Find("[data-testid='gateway-retry-button']"));
    }

    [Fact]
    public void Settings_AiAgentsTab_ContainsProviderSubTab()
    {
        // AI & Agents is the first tab — active by default, no click needed
        var api = BuildFullConfigApiClient();
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<Settings>();
        cut.WaitForAssertion(() =>
        {
            var subTabs = cut.Find("[data-testid='ai-agents-subtabs']");
            Assert.Contains("Providers", subTabs.TextContent);
            Assert.Contains("Agents", subTabs.TextContent);
        });
    }
}
