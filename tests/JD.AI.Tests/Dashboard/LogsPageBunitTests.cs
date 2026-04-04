using Bunit;
using JD.AI.Dashboard.Wasm.Pages;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Tests.Dashboard;

public sealed class LogsPageBunitTests : DashboardBunitTestContext
{
    [Fact]
    public void Logs_WhenAuditEventsLoad_RendersGridAndOpensDetailPanel()
    {
        var api = CreateApiClient(request =>
        {
            Assert.Equal("http://localhost/api/v1/audit/events?limit=1000", request.RequestUri!.ToString());
            return JsonResponse(SingleAuditEventResponse);
        });

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Logs>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='logs-grid']"));
            Assert.Single(cut.FindAll("[data-testid='log-level']"));
        });

        var grid = cut.Find("[data-testid='logs-grid']");
        Assert.Contains("tool.invoke", grid.TextContent);
        Assert.Contains("status=ok; args=path=README.md", grid.TextContent);

        cut.Find("[data-testid='log-detail-button']").Click();

        cut.WaitForAssertion(() =>
        {
            var detail = cut.Find("[data-testid='log-detail']");
            Assert.Contains("evt-1", detail.TextContent);
            Assert.Contains("tool.invoke", detail.TextContent);
        });
    }

    [Fact]
    public void Logs_WhenSearchFilterMisses_ShowsEmptyState()
    {
        var api = CreateApiClient(_ => JsonResponse(SingleAuditEventResponse));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Logs>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='logs-grid']")));

        cut.Find("[data-testid='search-filter']").Change("reqnroll-ui-no-match-zzzz");
        cut.Find("[data-testid='apply-filters']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='logs-empty']")));
    }

    [Fact]
    public void Logs_WhenAuditLoadFails_ShowsEmptyStateAndSnackbar()
    {
        var api = CreateApiClient(_ => throw new HttpRequestException("gateway offline"));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Logs>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='logs-empty']"));
            Assert.Contains("Failed to load audit events", cut.Markup);
        });
    }

    private const string SingleAuditEventResponse =
        """
        {
          "totalCount": 1,
          "count": 1,
          "events": [
            {
              "eventId": "evt-1",
              "timestamp": "2026-04-04T12:00:00Z",
              "sessionId": "sess-123",
              "action": "tool.invoke",
              "resource": "read_file",
              "detail": "status=ok; args=path=README.md",
              "severity": 2,
              "toolName": "read_file",
              "toolArguments": "path=README.md"
            }
          ]
        }
        """;
}
