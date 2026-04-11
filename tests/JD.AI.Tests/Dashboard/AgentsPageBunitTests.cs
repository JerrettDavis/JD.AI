using System.Net;
using Bunit;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using AgentsPageComponent = JD.AI.Dashboard.Wasm.Pages.Agents;

namespace JD.AI.Tests.Dashboard;

public sealed class AgentsPageBunitTests : DashboardBunitTestContext
{
    [Fact]
    public void Agents_WhenNoAgentsLoad_ShowsEmptyState()
    {
        var api = CreateApiClient(request =>
        {
            Assert.Equal("http://localhost/api/v1/agents", request.RequestUri!.ToString());
            return JsonResponse("[]");
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<AgentsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            var empty = cut.Find("[data-testid='agents-empty']");
            Assert.Contains("No active agents. Spawn one to get started.", empty.TextContent);
        });
    }

    [Fact]
    public void Agents_WhenAgentsLoad_RendersGridAndDeleteButtons()
    {
        var api = CreateApiClient(request =>
        {
            Assert.Equal("http://localhost/api/v1/agents", request.RequestUri!.ToString());
            return JsonResponse(
                """
                [
                  {
                    "id": "agent-1",
                    "provider": "openai",
                    "model": "gpt-5",
                    "turnCount": 4,
                    "createdAt": "2026-04-04T12:00:00Z"
                  },
                  {
                    "id": "agent-2",
                    "provider": "ollama",
                    "model": "qwen3",
                    "turnCount": 1,
                    "createdAt": "2026-04-04T12:05:00Z"
                  }
                ]
                """);
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<AgentsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            var grid = cut.Find("[data-testid='agents-grid']");
            Assert.Contains("agent-1", grid.TextContent);
            Assert.Contains("agent-2", grid.TextContent);
            Assert.Equal(2, cut.FindAll("[data-testid='delete-agent-button']").Count);
        });
    }

    [Fact]
    public void Agents_WhenSpawnConfirmed_PostsAgentAndReloadsGrid()
    {
        var listCalls = 0;

        var api = CreateApiClient(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/v1/agents", StringComparison.Ordinal))
            {
                if (request.Method == HttpMethod.Get)
                {
                    listCalls++;
                    return JsonResponse(
                        listCalls == 1
                            ? "[]"
                            : """
                              [
                                {
                                  "id": "srv-worker-001",
                                  "provider": "ollama",
                                  "model": "qwen3",
                                  "turnCount": 0,
                                  "createdAt": "2026-04-04T12:10:00Z"
                                }
                              ]
                              """);
                }

                if (request.Method == HttpMethod.Post)
                {
                    var body = request.Content!.ReadAsStringAsync().Result;
                    Assert.DoesNotContain("\"id\":", body, StringComparison.Ordinal);
                    Assert.Contains("\"provider\":\"ollama\"", body, StringComparison.Ordinal);
                    Assert.Contains("\"model\":\"qwen3\"", body, StringComparison.Ordinal);
                    return JsonResponse("""{"id":"srv-worker-001"}""",
                        HttpStatusCode.OK);
                }
            }

            throw new Xunit.Sdk.XunitException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<AgentsPageComponent>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='agents-empty']")));

        cut.Find("[data-testid='spawn-agent-button']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Spawn New Agent", cut.Markup));

        cut.Find("[data-testid='agent-provider-input']").Change("ollama");
        cut.Find("[data-testid='agent-model-input']").Change("qwen3");
        cut.Find("[data-testid='confirm-spawn-button']").Click();

        cut.WaitForAssertion(() =>
        {
            var grid = cut.Find("[data-testid='agents-grid']");
            Assert.Contains("srv-worker-001", grid.TextContent);
            Assert.Contains("Agent 'srv-worker-001' spawned", cut.Markup);
            Assert.True(listCalls >= 2, $"Expected reload after spawn, but list was requested {listCalls} time(s).");
        });
    }

    [Fact]
    public void Agents_WhenDeleteConfirmed_StopsAgentAndReloadsGrid()
    {
        var deleted = false;
        var listCalls = 0;

        var api = CreateApiClient(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/v1/agents", StringComparison.Ordinal))
            {
                listCalls++;
                return JsonResponse(
                    deleted
                        ? "[]"
                        : """
                          [
                            {
                              "id": "agent-1",
                              "provider": "openai",
                              "model": "gpt-5",
                              "turnCount": 4,
                              "createdAt": "2026-04-04T12:00:00Z"
                            }
                          ]
                          """);
            }

            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/v1/agents/agent-1", StringComparison.Ordinal)
                && request.Method == HttpMethod.Delete)
            {
                deleted = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            throw new Xunit.Sdk.XunitException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<AgentsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            var grid = cut.Find("[data-testid='agents-grid']");
            Assert.Contains("agent-1", grid.TextContent);
            Assert.Single(cut.FindAll("[data-testid='delete-agent-button']"));
        });

        cut.Find("[data-testid='delete-agent-button']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Stop agent 'agent-1'?", cut.Markup));

        cut.FindAll("button").First(button => button.TextContent.Contains("Stop", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='agents-empty']"));
            Assert.Contains("Agent 'agent-1' stopped", cut.Markup);
            Assert.True(listCalls >= 2, $"Expected reload after delete, but list was requested {listCalls} time(s).");
        });
    }

    [Fact]
    public void Agents_WhenAgentSelected_ShowsDetailDrawer()
    {
        var api = CreateApiClient(request =>
        {
            if (request.RequestUri!.ToString().Contains("/api/v1/agents/agent-1") && request.Method == HttpMethod.Get)
                return JsonResponse("""
                    {"id":"agent-1","provider":"openai","model":"gpt-5",
                     "systemPrompt":"Be helpful","isDefault":false,
                     "tools":[],"assignedSkills":[]}
                    """);
            return JsonResponse("""
                [{"id":"agent-1","provider":"openai","model":"gpt-5","turnCount":4,"createdAt":"2026-04-04T12:00:00Z"}]
                """);
        });
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<AgentsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='agent-row-agent-1']").Click();
        });

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='agent-detail-panel']"));
            Assert.Contains("agent-1", cut.Find("[data-testid='agent-detail-panel']").TextContent);
        });
    }

    [Fact]
    public void Agents_Toolbar_CopyIdButtonPresent()
    {
        var api = CreateApiClient(_ => JsonResponse("[]"));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<AgentsPageComponent>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='copy-id-button']")));
    }

    [Fact]
    public void Agents_Toolbar_SetDefaultButtonPresent()
    {
        var api = CreateApiClient(_ => JsonResponse("[]"));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<AgentsPageComponent>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='set-default-button']")));
    }

    [Fact]
    public void Agents_WhenDeleteFails_ShowsErrorAndKeepsGrid()
    {
        var listCalls = 0;

        var api = CreateApiClient(request =>
        {
            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/v1/agents", StringComparison.Ordinal))
            {
                listCalls++;
                return JsonResponse(
                    """
                    [
                      {
                        "id": "agent-1",
                        "provider": "openai",
                        "model": "gpt-5",
                        "turnCount": 4,
                        "createdAt": "2026-04-04T12:00:00Z"
                      }
                    ]
                    """);
            }

            if (string.Equals(request.RequestUri!.ToString(), "http://localhost/api/v1/agents/agent-1", StringComparison.Ordinal)
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

        var cut = RenderWithMudProviders<AgentsPageComponent>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='agents-grid']")));

        cut.Find("[data-testid='delete-agent-button']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Stop agent 'agent-1'?", cut.Markup));

        cut.FindAll("button").First(button => button.TextContent.Contains("Stop", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() =>
        {
            var grid = cut.Find("[data-testid='agents-grid']");
            Assert.Contains("agent-1", grid.TextContent);
            Assert.DoesNotContain("Agent 'agent-1' stopped", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Failed:", cut.Markup);
            Assert.Equal(1, listCalls);
        });
    }
}
