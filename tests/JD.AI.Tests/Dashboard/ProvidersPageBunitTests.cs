using System.Net;
using System.Reflection;
using Bunit;
using JD.AI.Dashboard.Wasm.Models;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using ProvidersPageComponent = JD.AI.Dashboard.Wasm.Pages.Providers;

namespace JD.AI.Tests.Dashboard;

public sealed class ProvidersPageBunitTests : DashboardBunitTestContext
{
    [Fact]
    public void Providers_WhenNoProvidersLoad_ShowsEmptyState()
    {
        var api = CreateApiClient(request =>
        {
            Assert.Equal("http://localhost/api/providers", request.RequestUri!.ToString());
            return JsonResponse("[]");
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<ProvidersPageComponent>();

        cut.WaitForAssertion(() =>
        {
            var empty = cut.Find("[data-testid='providers-empty']");
            Assert.Contains("No providers configured.", empty.TextContent);
        });
    }

    [Fact]
    public void Providers_WhenLoadFails_ShowsErrorStateAndSnackbar()
    {
        var api = CreateApiClient(_ => throw new HttpRequestException("catalog offline"));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<ProvidersPageComponent>();

        cut.WaitForAssertion(() =>
        {
            var error = cut.Find("[data-testid='providers-load-error']");
            Assert.Contains("Failed to load providers: catalog offline", error.TextContent);

            var snackbar = cut.Find(".mud-snackbar");
            Assert.Contains("Failed to load providers: catalog offline", snackbar.TextContent);
        });
    }

    [Fact]
    public void Providers_WhenProvidersLoad_RendersCardsStatusesAndModels()
    {
        var api = CreateApiClient(request =>
        {
            Assert.Equal("http://localhost/api/providers", request.RequestUri!.ToString());
            return JsonResponse(
                """
                [
                  {
                    "name": "OpenAI",
                    "isAvailable": true,
                    "statusMessage": null,
                    "models": [
                      { "id": "gpt-4.1", "displayName": "GPT-4.1", "providerName": "OpenAI" }
                    ]
                  },
                  {
                    "name": "Ollama",
                    "isAvailable": false,
                    "statusMessage": "offline",
                    "models": []
                  }
                ]
                """);
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<ProvidersPageComponent>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, cut.FindAll("[data-testid='provider-card']").Count);
            Assert.Equal(2, cut.FindAll("[data-testid='provider-status']").Count);

            var cards = cut.FindAll("[data-testid='provider-card']");
            Assert.Contains("OpenAI", cards[0].TextContent);
            Assert.Contains("GPT-4.1", cards[0].TextContent);
            Assert.Contains("Online", cards[0].TextContent);

            Assert.Contains("Ollama", cards[1].TextContent);
            Assert.Contains("offline", cards[1].TextContent);
            Assert.Contains("Offline", cards[1].TextContent);
        });
    }

    [Fact]
    public void Providers_WhenProviderActivityArrives_ReloadsAndShowsSnackbar()
    {
        var listCalls = 0;
        var signalR = new SignalRService("http://localhost");

        var api = CreateApiClient(request =>
        {
            Assert.Equal("http://localhost/api/providers", request.RequestUri!.ToString());
            listCalls++;

            return JsonResponse(
                listCalls == 1
                    ? """
                      [
                        {
                          "name": "OpenAI",
                          "isAvailable": true,
                          "statusMessage": null,
                          "models": []
                        }
                      ]
                      """
                    : """
                      [
                        {
                          "name": "OpenAI",
                          "isAvailable": true,
                          "statusMessage": null,
                          "models": []
                        },
                        {
                          "name": "Ollama",
                          "isAvailable": true,
                          "statusMessage": null,
                          "models": [
                            { "id": "qwen3.5:9b", "displayName": "Qwen 3.5 9B", "providerName": "Ollama" }
                          ]
                        }
                      ]
                      """);
        });

        Services.AddSingleton(api);
        Services.AddSingleton(signalR);

        var cut = RenderWithMudProviders<ProvidersPageComponent>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='provider-card']")));

        RaiseActivityEvent(signalR, new ActivityEvent
        {
            EventType = "provider.updated",
            SourceId = "ollama",
            Message = "Ollama provider detected",
        });

        cut.WaitForAssertion(() =>
        {
            var cards = cut.FindAll("[data-testid='provider-card']");
            Assert.Equal(2, cards.Count);
            Assert.Contains("Qwen 3.5 9B", cards[^1].TextContent);

            var snackbar = cut.Find(".mud-snackbar");
            Assert.Contains("Provider update: Ollama provider detected", snackbar.TextContent);
            Assert.True(listCalls >= 2, $"Expected reload after provider activity, but list was requested {listCalls} time(s).");
        });
    }

    [Fact]
    public void Providers_WhenEarlierLoadCompletesLast_IgnoresStaleResponse()
    {
        var signalR = new SignalRService("http://localhost");
        var firstResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;

        var http = new HttpClient(new AsyncStubHandler(request =>
        {
            Assert.Equal("http://localhost/api/providers", request.RequestUri!.ToString());
            requestCount++;

            if (requestCount == 1)
                return firstResponse.Task;

            return Task.FromResult(JsonResponse(
                """
                [
                  {
                    "name": "Ollama",
                    "isAvailable": true,
                    "statusMessage": null,
                    "models": [
                      { "id": "qwen3.5:9b", "displayName": "Qwen 3.5 9B", "providerName": "Ollama" }
                    ]
                  }
                ]
                """));
        }))
        {
            BaseAddress = new Uri("http://localhost/"),
        };

        Services.AddSingleton(new GatewayApiClient(http));
        Services.AddSingleton(signalR);

        var cut = RenderWithMudProviders<ProvidersPageComponent>();

        cut.Find("[data-testid='refresh-button']").Click();

        cut.WaitForAssertion(() =>
        {
            var cards = cut.FindAll("[data-testid='provider-card']");
            Assert.Single(cards);
            Assert.Contains("Qwen 3.5 9B", cards[0].TextContent);
        });

        firstResponse.SetResult(JsonResponse(
            """
            [
              {
                "name": "OpenAI",
                "isAvailable": true,
                "statusMessage": null,
                "models": [
                  { "id": "gpt-4.1", "displayName": "GPT-4.1", "providerName": "OpenAI" }
                ]
              }
            ]
            """));

        cut.WaitForAssertion(() =>
        {
            var cards = cut.FindAll("[data-testid='provider-card']");
            Assert.Single(cards);
            Assert.Contains("Qwen 3.5 9B", cards[0].TextContent);
            Assert.DoesNotContain("GPT-4.1", cards[0].TextContent);
        });
    }

    private static void RaiseActivityEvent(SignalRService service, ActivityEvent activity)
    {
        var field = typeof(SignalRService).GetField("OnActivityEvent", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var handler = (EventHandler<ActivityEventArgs>?)field!.GetValue(service);
        Assert.NotNull(handler);

        handler!(service, new ActivityEventArgs(activity));
    }

    private sealed class AsyncStubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request);
    }
}
