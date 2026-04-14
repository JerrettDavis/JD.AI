using Bunit;
using FluentAssertions;
using JD.AI.Dashboard.Wasm.Models;
using JD.AI.Dashboard.Wasm.Pages;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace JD.AI.Tests.Dashboard;

public sealed class ControlOverviewBunitTests : DashboardBunitTestContext
{
    private void RegisterServices(
        Func<HttpRequestMessage, HttpResponseMessage>? statusResponder = null,
        Func<HttpRequestMessage, HttpResponseMessage>? sessionsResponder = null)
    {
        var api = CreateApiClient(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/api/gateway/status"))
                return statusResponder?.Invoke(req) ?? JsonResponse(RunningStatusJson);
            if (req.RequestUri.AbsolutePath.Contains("/api/sessions"))
                return sessionsResponder?.Invoke(req) ?? JsonResponse("[]");
            return JsonResponse("null");
        });
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        JSInterop.SetupVoid("localStorage.setItem", _ => true);
        Services.AddScoped<LocalStorageService>();
    }

    private const string RunningStatusJson = """
        {
          "status": "running",
          "uptime": "2026-04-11T00:00:00Z",
          "channels": [
            { "channelType": "discord", "displayName": "Discord", "isConnected": true }
          ],
          "agents": [
            { "id": "jdai-default", "provider": "openai", "model": "gpt-5" }
          ],
          "routes": {},
          "openClaw": null
        }
        """;

    // ── Page structure ──────────────────────────────────────────

    [Fact]
    public void ControlOverview_RendersPageTitle()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='page-title']").TextContent.Trim().Should().Be("Overview");
    }

    [Fact]
    public void ControlOverview_RendersPageSubtitle()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='page-subtitle']").TextContent.Trim()
            .Should().Be("System health, gateway status, and live counters.");
    }

    // ── Snapshot cards ──────────────────────────────────────────

    [Fact]
    public void ControlOverview_WhenRunning_ShowsOkStatus()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='snapshot-status']").TextContent.Should().Contain("OK");
    }

    [Fact]
    public void ControlOverview_WhenRunning_ShowsUptimeCard()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='snapshot-uptime']").TextContent.Trim().Should().NotBeEmpty();
    }

    [Fact]
    public void ControlOverview_WhenRunning_ShowsTickInterval()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='snapshot-tick']").TextContent.Should().Contain("30s");
    }

    [Fact]
    public void ControlOverview_ShowsLastChannelsRefresh()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='snapshot-last-refresh']").Should().NotBeNull();
    }

    // ── Gateway access form ─────────────────────────────────────

    [Fact]
    public void ControlOverview_RendersWebSocketUrlInput()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='input-websocket-url']").Should().NotBeNull();
    }

    [Fact]
    public void ControlOverview_RendersGatewayTokenInput()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='input-gateway-token']").Should().NotBeNull();
    }

    [Fact]
    public void ControlOverview_RendersPasswordInput()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='input-password']").Should().NotBeNull();
    }

    [Fact]
    public void ControlOverview_RendersLanguageSelector()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='select-language']").Should().NotBeNull();
    }

    [Fact]
    public void ControlOverview_RendersConnectButton()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='btn-connect']").Should().NotBeNull();
    }

    [Fact]
    public void ControlOverview_RendersRefreshButton()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='btn-refresh']").Should().NotBeNull();
    }

    // ── Sessions table ──────────────────────────────────────────

    [Fact]
    public void ControlOverview_WhenSessionsExist_RendersSessionRows()
    {
        RegisterServices(sessionsResponder: _ => JsonResponse("""
            [
              { "id": "heartbeat", "modelId": "qwen3.5b:9b", "updatedAt": "2026-04-11T13:00:00Z" },
              { "id": "discord:123", "modelId": "gpt-5-codex", "updatedAt": "2026-04-10T20:00:00Z" }
            ]
            """));

        var cut = RenderWithMudProviders<ControlOverview>();
        cut.FindAll("[data-testid='session-row']").Should().HaveCount(2);
    }

    [Fact]
    public void ControlOverview_WhenNoSessions_RendersEmptyState()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='sessions-empty']").Should().NotBeNull();
    }

    // ── Alerts ──────────────────────────────────────────────────

    [Fact]
    public void ControlOverview_WhenApiUnavailable_ShowsDisconnectedStatus()
    {
        var api = CreateApiClient(_ => throw new HttpRequestException("unreachable"));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        JSInterop.SetupVoid("localStorage.setItem", _ => true);
        Services.AddScoped<LocalStorageService>();

        var cut = RenderWithMudProviders<ControlOverview>();
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='gateway-error-alert']").Should().NotBeNull());
    }

    // ── Operational counters ────────────────────────────────────

    [Fact]
    public void ControlOverview_ShowsAgentCount()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='stat-agents']").TextContent.Trim().Should().Be("1"));
    }

    [Fact]
    public void ControlOverview_ShowsChannelCount()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='stat-channels']").TextContent.Trim().Should().Be("1"));
    }
}
