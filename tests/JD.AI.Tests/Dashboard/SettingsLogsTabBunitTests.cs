using Bunit;
using JD.AI.Dashboard.Wasm.Components;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Tests.Dashboard;

public sealed class SettingsLogsTabBunitTests : DashboardBunitTestContext
{
    private const string EmptyAuditResponse = """{"totalCount":0,"count":0,"events":[]}""";

    [Fact]
    public void LogsTab_RendersGrid()
    {
        var signalR = new SignalRService("http://localhost");
        Services.AddSingleton(signalR);
        var api = CreateApiClient(_ => JsonResponse(EmptyAuditResponse));
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<SettingsLogsTab>(p => p.Add(c => c.Api, api));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='logs-empty']")));
    }

    [Fact]
    public void LogsTab_RendersSeverityFilter()
    {
        var signalR = new SignalRService("http://localhost");
        Services.AddSingleton(signalR);
        var api = CreateApiClient(_ => JsonResponse(EmptyAuditResponse));
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<SettingsLogsTab>(p => p.Add(c => c.Api, api));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='severity-filter']")));
    }

    [Fact]
    public void LogsTab_ClickChevron_ShowsDetailPanel()
    {
        var signalR = new SignalRService("http://localhost");
        Services.AddSingleton(signalR);
        var api = CreateApiClient(_ => JsonResponse(SampleEventsJson));
        Services.AddSingleton(api);

        var cut = RenderWithMudProviders<SettingsLogsTab>(p => p.Add(c => c.Api, api));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='log-row-expand-evt-1']")));

        cut.Find("[data-testid='log-row-expand-evt-1']").Click();

        cut.WaitForAssertion(() =>
        {
            var detail = cut.Find("[data-testid='log-row-detail-evt-1']");
            Assert.Contains("evt-1", detail.TextContent);
            Assert.Contains("policy.deny", detail.TextContent);
        });
    }

    [Fact]
    public void LogsTab_LevelColumn_RendersSeverityChip()
    {
        var signalR = new SignalRService("http://localhost");
        Services.AddSingleton(signalR);
        var api = CreateApiClient(_ => JsonResponse(SampleEventsJson));
        Services.AddSingleton(api);

        var cut = RenderWithMudProviders<SettingsLogsTab>(p => p.Add(c => c.Api, api));

        cut.WaitForAssertion(() =>
        {
            var chip = cut.Find("[data-testid='log-level-chip-evt-1']");
            Assert.Contains("mud-chip-color-error", chip.ClassList);
        });
    }

    private const string SampleEventsJson =
        """
        {
          "totalCount": 1,
          "count": 1,
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
            }
          ]
        }
        """;
}
