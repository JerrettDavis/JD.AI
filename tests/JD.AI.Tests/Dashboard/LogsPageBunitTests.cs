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
            Assert.Single(cut.FindAll("[data-testid='log-level-chip-evt-1']"));
        });

        var grid = cut.Find("[data-testid='logs-grid']");
        Assert.Contains("tool.invoke", grid.TextContent);
        Assert.Contains("status=ok; args=path=README.md", grid.TextContent);

        cut.Find("[data-testid='log-row-expand-evt-1']").Click();

        cut.WaitForAssertion(() =>
        {
            var detail = cut.Find("[data-testid='log-row-detail-evt-1']");
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

    [Fact]
    public void Logs_ErrorRow_ShowsChevronExpandButton()
    {
        var api = CreateApiClient(_ => JsonResponse(SampleEventsJson));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Logs>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='log-row-expand-evt-1']"));
        });
    }

    [Fact]
    public void Logs_ClickChevron_ShowsDetailPanel()
    {
        var api = CreateApiClient(_ => JsonResponse(SampleEventsJson));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Logs>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='logs-grid']")));

        cut.Find("[data-testid='log-row-expand-evt-1']").Click();

        cut.WaitForAssertion(() =>
        {
            var detail = cut.Find("[data-testid='log-row-detail-evt-1']");
            Assert.Contains("evt-1", detail.TextContent);
            Assert.Contains("policy.deny", detail.TextContent);
        });
    }

    [Fact]
    public void Logs_ClickChevronTwice_CollapseDetail()
    {
        var api = CreateApiClient(_ => JsonResponse(SampleEventsJson));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Logs>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='logs-grid']")));

        var button = cut.Find("[data-testid='log-row-expand-evt-1']");
        button.Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='log-row-detail-evt-1']")));

        button.Click();
        cut.WaitForAssertion(() =>
        {
            var items = cut.FindAll("[data-testid='log-row-detail-evt-1']");
            Assert.Empty(items);
        });
    }

    [Fact]
    public void Logs_BothRows_HaveExpandButtons()
    {
        var api = CreateApiClient(_ => JsonResponse(SampleEventsJson));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Logs>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='log-row-expand-evt-1']"));
            Assert.NotNull(cut.Find("[data-testid='log-row-expand-evt-2']"));
        });
    }

    [Fact]
    public void Logs_ErrorLevel_RendersRedChip()
    {
        var api = CreateApiClient(_ => JsonResponse(SampleEventsJson));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Logs>();

        cut.WaitForAssertion(() =>
        {
            var chip = cut.Find("[data-testid='log-level-chip-evt-1']");
            Assert.Contains("mud-chip-color-error", chip.ClassList);
        });
    }

    [Fact]
    public void Logs_InfoLevel_RendersBlueChip()
    {
        var api = CreateApiClient(_ => JsonResponse(SampleEventsJson));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Logs>();

        cut.WaitForAssertion(() =>
        {
            var chip = cut.Find("[data-testid='log-level-chip-evt-2']");
            Assert.Contains("mud-chip-color-info", chip.ClassList);
        });
    }

    private const string SampleEventsJson =
        """
        {
          "totalCount": 2,
          "count": 2,
          "events": [
            {
              "eventId": "evt-1",
              "timestamp": "2026-04-04T12:00:00Z",
              "sessionId": "sess-123",
              "action": "policy.deny",
              "resource": "gateway_access",
              "detail": "Access denied",
              "severity": 3,
              "payload": "{\"error\": \"permission denied\", \"context\": \"sensitive_operation\"}"
            },
            {
              "eventId": "evt-2",
              "timestamp": "2026-04-04T11:00:00Z",
              "sessionId": "sess-124",
              "action": "session.create",
              "resource": "user_session",
              "detail": "User session created",
              "severity": 1
            }
          ]
        }
        """;

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
