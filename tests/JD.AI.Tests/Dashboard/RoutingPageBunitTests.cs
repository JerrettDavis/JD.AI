using System.Net;
using Bunit;
using JD.AI.Dashboard.Wasm.Models;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RoutingPageComponent = JD.AI.Dashboard.Wasm.Pages.Routing;

namespace JD.AI.Tests.Dashboard;

public sealed class RoutingPageBunitTests : DashboardBunitTestContext
{
    [Fact]
    public void Routing_WhenMappingsLoad_RendersGridStatusAndDiagram()
    {
        var api = CreateApiClient(request =>
        {
            Assert.Equal("http://localhost/api/routing/mappings", request.RequestUri!.ToString());
            return JsonResponse("""{"discord":"jdai-default","web":""}""");
        });

        Services.AddSingleton(api);

        var cut = RenderWithMudProviders<RoutingPageComponent>();

        cut.WaitForAssertion(() =>
        {
            var grid = cut.Find("[data-testid='routing-grid']");
            Assert.Contains("discord", grid.TextContent);
            Assert.Contains("web", grid.TextContent);
            Assert.Equal(2, cut.FindAll("[data-testid='routing-status']").Count);
            Assert.Contains("Override", cut.Markup);
            Assert.Contains("Default", cut.Markup);

            var diagram = cut.Find("[data-testid='routing-diagram']");
            Assert.Contains("jdai-default", diagram.TextContent);
            Assert.Contains("OpenClaw Default", diagram.TextContent);
        });
    }

    [Fact]
    public void Routing_WhenLoadFails_ShowsErrorStateAndSnackbar()
    {
        var api = CreateApiClient(_ => throw new HttpRequestException("gateway offline"));

        Services.AddSingleton(api);

        var cut = RenderWithMudProviders<RoutingPageComponent>();

        cut.WaitForAssertion(() =>
        {
            var error = cut.Find("[data-testid='routing-load-error']");
            Assert.Contains("Failed to load routing mappings: gateway offline", error.TextContent);
            var snackbar = cut.Find(".mud-snackbar");
            Assert.Contains("Failed to load routing mappings: gateway offline", snackbar.TextContent);
        });
    }

    [Fact]
    public void Routing_WhenSyncSucceeds_ShowsSuccessAndReloadsMappings()
    {
        var synced = false;
        var listCalls = 0;

        var api = CreateApiClient(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/routing/mappings", StringComparison.Ordinal))
            {
                listCalls++;
                return JsonResponse(
                    synced
                        ? """{"discord":"jdai-default","web":"jdai-web"}"""
                        : """{"discord":"jdai-default","web":""}""");
            }

            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/gateway/openclaw/agents/sync", StringComparison.Ordinal)
                && request.Method == HttpMethod.Post)
            {
                synced = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            throw new Xunit.Sdk.XunitException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        Services.AddSingleton(api);

        var cut = RenderWithMudProviders<RoutingPageComponent>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='sync-openclaw-button']")));

        cut.Find("[data-testid='sync-openclaw-button']").Click();

        cut.WaitForAssertion(() =>
        {
            var diagram = cut.Find("[data-testid='routing-diagram']");
            Assert.Contains("jdai-web", diagram.TextContent);
            Assert.Contains("OpenClaw sync complete", cut.Markup);
            Assert.True(listCalls >= 2, $"Expected reload after sync, but list was requested {listCalls} time(s).");
        });
    }

    [Fact]
    public async Task Routing_WhenMappingChanges_PostsRouteUpdateAndShowsSuccess()
    {
        var api = CreateApiClient(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/routing/mappings", StringComparison.Ordinal))
                return JsonResponse("""{"discord":""}""");

            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/routing/map", StringComparison.Ordinal)
                && request.Method == HttpMethod.Post)
            {
                var body = request.Content!.ReadAsStringAsync().Result;
                Assert.Contains("\"channelId\":\"discord\"", body, StringComparison.Ordinal);
                Assert.Contains("\"agentId\":\"jdai-default\"", body, StringComparison.Ordinal);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            throw new Xunit.Sdk.XunitException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        Services.AddSingleton(api);

        var cut = RenderWithMudProviders<RoutingPageComponent>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='routing-grid']")));

        var grid = cut.FindComponent<MudDataGrid<RoutingMapping>>();
        var updated = new RoutingMapping
        {
            ChannelType = "discord",
            AgentId = "jdai-default",
        };

        await cut.InvokeAsync(() => grid.Instance.CommittedItemChanges.InvokeAsync(updated));

        cut.WaitForAssertion(() => Assert.Contains("Routing updated: discord → jdai-default", cut.Markup));
    }

    [Fact]
    public async Task Routing_WhenMappingSaveFails_ReloadsServerStateAndShowsError()
    {
        var listCalls = 0;

        var api = CreateApiClient(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/routing/mappings", StringComparison.Ordinal))
            {
                listCalls++;
                return JsonResponse("""{"discord":""}""");
            }

            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/routing/map", StringComparison.Ordinal)
                && request.Method == HttpMethod.Post)
            {
                return JsonResponse("""{"error":"denied"}""", HttpStatusCode.InternalServerError);
            }

            throw new Xunit.Sdk.XunitException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        Services.AddSingleton(api);

        var cut = RenderWithMudProviders<RoutingPageComponent>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='routing-grid']")));

        var grid = cut.FindComponent<MudDataGrid<RoutingMapping>>();
        var updated = new RoutingMapping
        {
            ChannelType = "discord",
            AgentId = "jdai-default",
        };

        await cut.InvokeAsync(() => grid.Instance.CommittedItemChanges.InvokeAsync(updated));

        cut.WaitForAssertion(() =>
        {
            var snackbar = cut.Find(".mud-snackbar");
            Assert.Contains("Failed:", snackbar.TextContent);

            var diagram = cut.Find("[data-testid='routing-diagram']");
            Assert.DoesNotContain("jdai-default", diagram.TextContent);
            Assert.Contains("OpenClaw Default", diagram.TextContent);
            Assert.True(listCalls >= 2, $"Expected reload after failed save, but list was requested {listCalls} time(s).");
        });
    }
}
