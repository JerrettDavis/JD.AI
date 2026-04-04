using System.Net;
using Bunit;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using ChannelsPageComponent = JD.AI.Dashboard.Wasm.Pages.Channels;

namespace JD.AI.Tests.Dashboard;

public sealed class ChannelsPageBunitTests : DashboardBunitTestContext
{
    [Fact]
    public void Channels_WhenNoChannelsLoad_ShowsEmptyState()
    {
        var api = CreateApiClient(request =>
        {
            Assert.Equal("http://localhost/api/channels", request.RequestUri!.ToString());
            return JsonResponse("[]");
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<ChannelsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            var empty = cut.Find("[data-testid='channels-empty']");
            Assert.Contains("No channels configured. Check your Gateway configuration.", empty.TextContent);
        });
    }

    [Fact]
    public void Channels_WhenLoadFails_ShowsErrorStateAndSnackbar()
    {
        var api = CreateApiClient(_ => throw new HttpRequestException("gateway offline"));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<ChannelsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            var error = cut.Find("[data-testid='channels-load-error']");
            Assert.Contains("Failed to load channels: gateway offline", error.TextContent);
            Assert.Contains("Failed to load channels: gateway offline", cut.Markup);
        });
    }

    [Fact]
    public void Channels_WhenChannelsLoad_RendersCardsAndActionButtons()
    {
        var api = CreateApiClient(request =>
        {
            Assert.Equal("http://localhost/api/channels", request.RequestUri!.ToString());
            return JsonResponse(
                """
                [
                  { "channelType": "discord", "displayName": "Discord", "isConnected": false },
                  { "channelType": "slack", "displayName": "Slack", "isConnected": true }
                ]
                """);
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<ChannelsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, cut.FindAll("[data-testid='channel-card']").Count);
            Assert.Single(cut.FindAll("[data-testid='channel-connect-button']"));
            Assert.Single(cut.FindAll("[data-testid='channel-disconnect-button']"));
            Assert.Equal(2, cut.FindAll("[data-testid='channel-override-button']").Count);
        });
    }

    [Fact]
    public void Channels_WhenConnectSucceeds_ShowsSuccessAndReloads()
    {
        var connected = false;
        var listCalls = 0;

        var api = CreateApiClient(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/channels", StringComparison.Ordinal))
            {
                listCalls++;
                return JsonResponse(
                    connected
                        ? """[{ "channelType": "discord", "displayName": "Discord", "isConnected": true }]"""
                        : """[{ "channelType": "discord", "displayName": "Discord", "isConnected": false }]""");
            }

            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/channels/discord/connect", StringComparison.Ordinal)
                && request.Method == HttpMethod.Post)
            {
                connected = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            throw new Xunit.Sdk.XunitException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<ChannelsPageComponent>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='channel-connect-button']")));

        cut.Find("[data-testid='channel-connect-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("discord channel connected", cut.Markup);
            Assert.Contains("Online", cut.Find("[data-testid='channel-status']").TextContent);
            Assert.True(listCalls >= 2, $"Expected reload after connect, but list was requested {listCalls} time(s).");
        });
    }

    [Fact]
    public void Channels_WhenConnectFails_ShowsErrorAndDoesNotReload()
    {
        var listCalls = 0;

        var api = CreateApiClient(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/channels", StringComparison.Ordinal))
            {
                listCalls++;
                return JsonResponse("""[{ "channelType": "discord", "displayName": "Discord", "isConnected": false }]""");
            }

            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/channels/discord/connect", StringComparison.Ordinal)
                && request.Method == HttpMethod.Post)
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

        var cut = RenderWithMudProviders<ChannelsPageComponent>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='channel-connect-button']")));

        cut.Find("[data-testid='channel-connect-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed:", cut.Markup);
            Assert.Contains("Offline", cut.Find("[data-testid='channel-status']").TextContent);
            Assert.Equal(1, listCalls);
        });
    }

    [Fact]
    public void Channels_WhenOverrideSaved_ReloadsChannels()
    {
        var listCalls = 0;

        var api = CreateApiClient(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/channels", StringComparison.Ordinal))
            {
                listCalls++;
                return JsonResponse("""[{ "channelType": "discord", "displayName": "Discord", "isConnected": true }]""");
            }

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
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<ChannelsPageComponent>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='channel-override-button']")));

        cut.Find("[data-testid='channel-override-button']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Override: Discord (discord)", cut.Markup));

        cut.Find("[data-testid='override-agent-id']").Change("jdai-default");
        cut.Find("[data-testid='override-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Override saved for discord", cut.Markup);
            Assert.True(listCalls >= 2, $"Expected reload after override save, but list was requested {listCalls} time(s).");
        });
    }
}
