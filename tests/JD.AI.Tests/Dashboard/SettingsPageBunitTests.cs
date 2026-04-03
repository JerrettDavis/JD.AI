using Bunit;
using JD.AI.Dashboard.Wasm.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Tests.Dashboard;

public sealed class SettingsPageBunitTests : DashboardBunitTestContext
{
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
        var api = CreateApiClient(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/gateway/config/raw", StringComparison.Ordinal))
            {
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
                        { "id": "jdai-default", "provider": "openai", "model": "gpt-5", "autoSpawn": true, "maxTurns": 0, "tools": [], "parameters": { "stopSequences": [] } }
                      ],
                      "channels": [
                        { "type": "discord", "name": "Discord", "enabled": true, "autoConnect": true, "settings": {} }
                      ],
                      "routing": { "defaultAgentId": "jdai-default", "rules": [] },
                      "openClaw": { "enabled": true, "webSocketUrl": "ws://127.0.0.1/ws", "autoConnect": true, "defaultMode": "Sidecar", "channels": {}, "registerAgents": [] }
                    }
                    """);
            }

            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/providers/openai/models", StringComparison.Ordinal))
            {
                return JsonResponse("[{\"id\":\"gpt-5\",\"displayName\":\"GPT-5\",\"provider\":\"openai\"}]");
            }

            throw new Xunit.Sdk.XunitException($"Unexpected request: {request.RequestUri}");
        });

        Services.AddSingleton(api);

        var cut = RenderWithMudProviders<Settings>();

        var tabs = cut.Find("[data-testid='settings-tabs']");
        Assert.Contains("Server", tabs.TextContent);
        Assert.Contains("Providers", tabs.TextContent);
        Assert.Contains("Agents", tabs.TextContent);
        Assert.Contains("Channels", tabs.TextContent);
        Assert.Contains("Routing", tabs.TextContent);
        Assert.Contains("OpenClaw", tabs.TextContent);
    }
}
