using Bunit;
using JD.AI.Dashboard.Wasm.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Tests.Dashboard;

public sealed class AnalyticsPageBunitTests : DashboardBunitTestContext
{
    [Fact]
    public void Analytics_WhenDataLoads_RendersAggregatesAndTopTables()
    {
        var api = CreateApiClient(request => request.RequestUri!.ToString() switch
        {
            "http://localhost/api/sessions?limit=50" => JsonResponse(
                """
                [
                  {
                    "id": "s1",
                    "modelId": "gpt-5",
                    "totalTokens": 200,
                    "turns": [
                      { "id": "t1", "role": "user", "content": "hi", "tokensIn": 10, "tokensOut": 20, "createdAt": "2026-04-02T12:00:00Z" },
                      { "id": "t2", "role": "assistant", "content": "hello", "tokensIn": 5, "tokensOut": 15, "createdAt": "2026-04-02T12:01:00Z" }
                    ],
                    "createdAt": "2026-04-02T12:00:00Z",
                    "updatedAt": "2026-04-02T12:01:00Z"
                  },
                  {
                    "id": "s2",
                    "modelId": "claude-opus",
                    "totalTokens": 300,
                    "turns": [
                      { "id": "t3", "role": "user", "content": "yo", "tokensIn": 30, "tokensOut": 40, "createdAt": "2026-04-02T13:00:00Z" }
                    ],
                    "createdAt": "2026-04-02T13:00:00Z",
                    "updatedAt": "2026-04-02T13:01:00Z"
                  },
                  {
                    "id": "s3",
                    "modelId": "gpt-5",
                    "totalTokens": 150,
                    "turns": [
                      { "id": "t4", "role": "user", "content": "again", "tokensIn": 7, "tokensOut": 8, "createdAt": "2026-04-02T14:00:00Z" }
                    ],
                    "createdAt": "2026-04-02T14:00:00Z",
                    "updatedAt": "2026-04-02T14:01:00Z"
                  }
                ]
                """),
            "http://localhost/api/v1/agents" => JsonResponse(
                """
                [
                  { "id": "jdai-research", "provider": "anthropic", "model": "claude-opus", "turnCount": 12, "createdAt": "2026-04-01T10:00:00Z" },
                  { "id": "jdai-default", "provider": "openai", "model": "gpt-5", "turnCount": 20, "createdAt": "2026-04-01T09:00:00Z" }
                ]
                """),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected request: {request.RequestUri}")
        });

        Services.AddSingleton(api);

        var cut = RenderComponent<Analytics>();

        var statCards = cut.FindAll("[data-testid='stat-card']");
        Assert.Equal(4, statCards.Count);
        Assert.Contains("3", cut.Find("[data-name='total-sessions'] [data-testid='stat-value']").TextContent);
        Assert.Contains("52", cut.Find("[data-name='total-tokens-in'] [data-testid='stat-value']").TextContent);
        Assert.Contains("83", cut.Find("[data-name='total-tokens-out'] [data-testid='stat-value']").TextContent);
        Assert.Contains("2", cut.Find("[data-name='active-agents'] [data-testid='stat-value']").TextContent);

        var topAgentsTable = cut.Find("[data-testid='top-agents-table']");
        Assert.Contains("jdai-default", topAgentsTable.TextContent);
        Assert.Contains("20", topAgentsTable.TextContent);
        Assert.Contains("jdai-research", topAgentsTable.TextContent);

        var topModelsTable = cut.Find("[data-testid='top-models-table']");
        Assert.Contains("gpt-5", topModelsTable.TextContent);
        Assert.Contains("2", topModelsTable.TextContent);
        Assert.Contains("350", topModelsTable.TextContent);
        Assert.Contains("claude-opus", topModelsTable.TextContent);
    }

    [Fact]
    public void Analytics_WhenSessionsAreEmpty_ShowsEmptyState()
    {
        var api = CreateApiClient(request => request.RequestUri!.ToString() switch
        {
            "http://localhost/api/sessions?limit=50" => JsonResponse("[]"),
            "http://localhost/api/v1/agents" => JsonResponse("[]"),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected request: {request.RequestUri}")
        });

        Services.AddSingleton(api);

        var cut = RenderComponent<Analytics>();

        Assert.NotNull(cut.Find("[data-testid='analytics-empty']"));
        Assert.Empty(cut.FindAll("[data-testid='top-agents-table']"));
        Assert.Empty(cut.FindAll("[data-testid='top-models-table']"));
    }
}
