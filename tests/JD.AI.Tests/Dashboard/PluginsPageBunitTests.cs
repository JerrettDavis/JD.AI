using System.Net;
using Bunit;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using PluginsPageComponent = JD.AI.Dashboard.Wasm.Pages.Plugins;

namespace JD.AI.Tests.Dashboard;

public sealed class PluginsPageBunitTests : DashboardBunitTestContext
{
    [Fact]
    public void Plugins_WhenNoPluginsLoad_ShowsEmptyState()
    {
        var api = CreateApiClient(request =>
        {
            Assert.Equal("http://localhost/api/plugins", request.RequestUri!.ToString());
            return JsonResponse("[]");
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<PluginsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            var empty = cut.Find("[data-testid='plugins-empty']");
            Assert.Contains("No plugins installed.", empty.TextContent);
        });
    }

    [Fact]
    public void Plugins_WhenPluginsLoad_RendersGridAndStatusChips()
    {
        var api = CreateApiClient(request =>
        {
            Assert.Equal("http://localhost/api/plugins", request.RequestUri!.ToString());
            return JsonResponse(
                """
                [
                  { "id": "weather", "name": "Weather", "version": "1.0.0", "author": "JD", "enabled": true },
                  { "id": "jira", "name": "Jira", "version": "2.1.0", "author": "JD", "enabled": false }
                ]
                """);
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<PluginsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            var grid = cut.Find("[data-testid='plugins-grid']");
            Assert.Contains("Weather", grid.TextContent);
            Assert.Contains("Jira", grid.TextContent);
            Assert.Equal(2, cut.FindAll("[data-testid='plugin-status']").Count);
            Assert.Equal(2, cut.FindAll("[data-testid='plugin-toggle']").Count);
            Assert.Equal(2, cut.FindAll("[data-testid='plugin-uninstall']").Count);
        });
    }

    [Fact]
    public void Plugins_WhenInstallDialogSubmitted_InstallsPluginAndReloads()
    {
        var listCalls = 0;

        var api = CreateApiClient(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/plugins", StringComparison.Ordinal))
            {
                listCalls++;
                return JsonResponse(
                    listCalls == 1
                        ? "[]"
                        : """[{ "id": "weather", "name": "Weather", "version": "1.0.0", "author": "JD", "enabled": true }]""");
            }

            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/plugins/install", StringComparison.Ordinal)
                && request.Method == HttpMethod.Post)
            {
                var body = request.Content!.ReadAsStringAsync().Result;
                Assert.Contains("\"pluginId\":\"weather\"", body, StringComparison.Ordinal);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            throw new Xunit.Sdk.XunitException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<PluginsPageComponent>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='plugins-empty']")));

        cut.Find("[data-testid='install-plugin-button']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='plugin-id-input']")));

        cut.Find("[data-testid='plugin-id-input']").Change("weather");
        cut.Find("[data-testid='plugin-install-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            var grid = cut.Find("[data-testid='plugins-grid']");
            Assert.Contains("Weather", grid.TextContent);
            Assert.Contains("Plugin installed", cut.Markup);
            Assert.True(listCalls >= 2, $"Expected reload after install, but list was requested {listCalls} time(s).");
        });
    }

    [Fact]
    public void Plugins_WhenToggleSucceeds_UpdatesPluginState()
    {
        var enabled = false;
        var listCalls = 0;

        var api = CreateApiClient(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/plugins", StringComparison.Ordinal))
            {
                listCalls++;
                return JsonResponse(
                    enabled
                        ? """[{ "id": "jira", "name": "Jira", "version": "2.1.0", "author": "JD", "enabled": true }]"""
                        : """[{ "id": "jira", "name": "Jira", "version": "2.1.0", "author": "JD", "enabled": false }]""");
            }

            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/plugins/jira/enable", StringComparison.Ordinal)
                && request.Method == HttpMethod.Post)
            {
                enabled = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            throw new Xunit.Sdk.XunitException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<PluginsPageComponent>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='plugin-toggle']")));

        cut.Find("[data-testid='plugin-toggle']").Change(true);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Plugin Jira enabled", cut.Markup);
            Assert.Contains("Enabled", cut.Find("[data-testid='plugin-status']").TextContent);
            Assert.True(listCalls >= 2, $"Expected reload after enable, but list was requested {listCalls} time(s).");
        });
    }

    [Fact]
    public void Plugins_WhenUninstallFails_ShowsErrorAndKeepsGrid()
    {
        var listCalls = 0;

        var api = CreateApiClient(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/plugins", StringComparison.Ordinal))
            {
                listCalls++;
                return JsonResponse("""[{ "id": "weather", "name": "Weather", "version": "1.0.0", "author": "JD", "enabled": true }]""");
            }

            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/plugins/weather", StringComparison.Ordinal)
                && request.Method == HttpMethod.Delete)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    ReasonPhrase = "boom",
                };
            }

            throw new Xunit.Sdk.XunitException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<PluginsPageComponent>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='plugin-uninstall']")));

        cut.Find("[data-testid='plugin-uninstall']").Click();

        cut.WaitForAssertion(() =>
        {
            var grid = cut.Find("[data-testid='plugins-grid']");
            Assert.Contains("Weather", grid.TextContent);
            Assert.Contains("Failed:", cut.Markup);
            Assert.Equal(1, listCalls);
        });
    }
}
