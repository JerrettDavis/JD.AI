using Bunit;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using SessionsPageComponent = JD.AI.Dashboard.Wasm.Pages.Sessions;

namespace JD.AI.Tests.Dashboard;

public sealed class SessionsPageBunitTests : DashboardBunitTestContext
{
    [Fact]
    public void Sessions_WhenNoSessionsLoad_ShowsEmptyState()
    {
        var api = CreateApiClient(request =>
        {
            Assert.Equal("http://localhost/api/sessions?limit=50", request.RequestUri!.ToString());
            return JsonResponse("[]");
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<SessionsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            var empty = cut.Find("[data-testid='sessions-empty']");
            Assert.Contains("No sessions found.", empty.TextContent);
        });
    }

    [Fact]
    public void Sessions_WhenViewingSession_RendersDetailPanelAndRespectsActiveCloseButtons()
    {
        var api = CreateApiClient(request => request.RequestUri!.ToString() switch
        {
            "http://localhost/api/sessions?limit=50" => JsonResponse(
                """
                [
                  {
                    "id": "s-active",
                    "modelId": "gpt-5",
                    "providerName": "openai",
                    "channelType": "web",
                    "messageCount": 4,
                    "totalTokens": 128,
                    "isActive": true,
                    "createdAt": "2026-04-04T12:00:00Z",
                    "updatedAt": "2026-04-04T12:05:00Z",
                    "turns": []
                  },
                  {
                    "id": "s-closed",
                    "modelId": "claude-opus",
                    "providerName": "anthropic",
                    "channelType": "slack",
                    "messageCount": 2,
                    "totalTokens": 64,
                    "isActive": false,
                    "createdAt": "2026-04-03T09:00:00Z",
                    "updatedAt": "2026-04-03T09:05:00Z",
                    "turns": []
                  }
                ]
                """),
            "http://localhost/api/sessions/s-active" => JsonResponse(
                """
                {
                  "id": "s-active",
                  "modelId": "gpt-5",
                  "providerName": "openai",
                  "channelType": "web",
                  "messageCount": 4,
                  "totalTokens": 128,
                  "isActive": true,
                  "createdAt": "2026-04-04T12:00:00Z",
                  "updatedAt": "2026-04-04T12:05:00Z",
                  "turns": [
                    {
                      "id": "t1",
                      "turnIndex": 0,
                      "role": "user",
                      "content": "hello there",
                      "tokensIn": 12,
                      "tokensOut": 0,
                      "durationMs": 0,
                      "createdAt": "2026-04-04T12:00:00Z"
                    },
                    {
                      "id": "t2",
                      "turnIndex": 1,
                      "role": "assistant",
                      "content": "Hi!",
                      "tokensIn": 12,
                      "tokensOut": 24,
                      "durationMs": 320,
                      "createdAt": "2026-04-04T12:00:01Z"
                    }
                  ]
                }
                """),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected request: {request.RequestUri}")
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<SessionsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='sessions-grid']"));
            Assert.Equal(2, cut.FindAll("[data-testid='session-status']").Count);
            Assert.Single(cut.FindAll("[data-testid='session-close-button']"));
        });

        cut.Find("[data-testid='session-view-button']").Click();

        cut.WaitForAssertion(() =>
        {
            var detail = cut.Find("[data-testid='session-detail']");
            Assert.Contains("Conversation: s-active", detail.TextContent);
            Assert.Contains("hello there", detail.TextContent);
            Assert.Contains("Hi!", detail.TextContent);
            Assert.Contains("12/24 tokens", detail.TextContent);
            Assert.Contains("320ms", detail.TextContent);
        });
    }

    [Fact]
    public void Sessions_WhenClosingActiveSession_ShowsSuccessAndReloadsGrid()
    {
        var sessionClosed = false;
        var listCalls = 0;

        var api = CreateApiClient(request => request.RequestUri!.ToString() switch
        {
            "http://localhost/api/sessions?limit=50" => BuildSessionsListResponse(),
            "http://localhost/api/sessions/s-active/close" when request.Method == HttpMethod.Post => CloseSessionResponse(),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected request: {request.RequestUri}")
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<SessionsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='sessions-grid']"));
            Assert.Single(cut.FindAll("[data-testid='session-close-button']"));
        });

        cut.Find("[data-testid='session-close-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='session-close-button']"));
            Assert.Contains("Session closed", cut.Markup);
            Assert.True(listCalls >= 2, $"Expected sessions to reload after close, but list was requested {listCalls} time(s).");
        });

        HttpResponseMessage BuildSessionsListResponse()
        {
            listCalls++;
            return JsonResponse(
                sessionClosed
                    ? """
                      [
                        {
                          "id": "s-active",
                          "modelId": "gpt-5",
                          "providerName": "openai",
                          "channelType": "web",
                          "messageCount": 4,
                          "totalTokens": 128,
                          "isActive": false,
                          "createdAt": "2026-04-04T12:00:00Z",
                          "updatedAt": "2026-04-04T12:05:00Z",
                          "turns": []
                        }
                      ]
                      """
                    : """
                      [
                        {
                          "id": "s-active",
                          "modelId": "gpt-5",
                          "providerName": "openai",
                          "channelType": "web",
                          "messageCount": 4,
                          "totalTokens": 128,
                          "isActive": true,
                          "createdAt": "2026-04-04T12:00:00Z",
                          "updatedAt": "2026-04-04T12:05:00Z",
                          "turns": []
                        }
                      ]
                      """);
        }

        HttpResponseMessage CloseSessionResponse()
        {
            sessionClosed = true;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }
}
