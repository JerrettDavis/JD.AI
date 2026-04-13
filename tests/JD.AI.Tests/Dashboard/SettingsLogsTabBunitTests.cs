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
}
