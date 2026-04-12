# Control Overview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a dedicated `/control/overview` route that shows gateway connection status, uptime/snapshot cards, a gateway access form, recent sessions table, and operational alerts — replacing the current `/` Home page's OpenClaw-centric layout.

**Architecture:** A new `ControlOverview.razor` page registers at `@page "/control/overview"` and also captures `@page "/"` (with `Home.razor` losing its `@page "/"` claim). The page uses `GatewayApiClient.GetStatusAsync()` and `GatewayApiClient.GetSessionsAsync()` for initial data, refreshes via a 30-second `PeriodicTimer` loop, and wires into the existing `SignalRService.OnActivityEvent` for real-time overlay updates. A thin `ControlOverviewSnapshotModel` record carries the derived display fields so bunit tests can validate them without rendering full MudBlazor trees.

**Tech Stack:** Blazor WASM .NET 10, MudBlazor v8.5.1, xunit/bunit, Playwright/Reqnroll

---

## Step 1 — Add `ControlOverviewSnapshotModel` and extend `GatewayStatus`

### Why
The page needs derived display values (uptime formatted as "19h", status color, cost string, skills ratio). Centralising them in a record keeps the `.razor` code-behind thin and makes unit testing trivial without a full Blazor render.

### Files
- `src/JD.AI.Dashboard.Wasm/Models/ControlOverviewModels.cs` *(new)*

### Failing test first
`tests/JD.AI.Tests/Dashboard/ControlOverviewSnapshotModelTests.cs`

```csharp
using JD.AI.Dashboard.Wasm.Models;
using FluentAssertions;

namespace JD.AI.Tests.Dashboard;

public sealed class ControlOverviewSnapshotModelTests
{
    [Fact]
    public void FromGatewayStatus_WhenRunning_ReturnsOkStatusAndFormattedUptime()
    {
        var status = new GatewayStatus
        {
            Status = "running",
            Uptime = DateTimeOffset.UtcNow.AddHours(-19),
            Channels = [],
            Agents = [],
        };

        var snapshot = ControlOverviewSnapshotModel.From(status, sessions: []);

        snapshot.StatusText.Should().Be("OK");
        snapshot.StatusColor.Should().Be("success");
        snapshot.UptimeDisplay.Should().MatchRegex(@"^\d+h$|^\d+d \d+h$");
    }

    [Fact]
    public void FromGatewayStatus_WhenNull_ReturnsDisconnectedDefaults()
    {
        var snapshot = ControlOverviewSnapshotModel.From(null, sessions: []);

        snapshot.StatusText.Should().Be("Disconnected");
        snapshot.StatusColor.Should().Be("error");
        snapshot.UptimeDisplay.Should().Be("—");
    }

    [Fact]
    public void FromGatewayStatus_SessionCount_MatchesActiveSessionsInList()
    {
        var sessions = new[]
        {
            new SessionInfo { Id = "heartbeat", ModelId = "qwen", UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-58) },
            new SessionInfo { Id = "discord:xyz", ModelId = "gpt-5", UpdatedAt = DateTimeOffset.UtcNow.AddHours(-17) },
        };

        var snapshot = ControlOverviewSnapshotModel.From(new GatewayStatus { Status = "running" }, sessions);

        snapshot.SessionCount.Should().Be(2);
    }
}
```

Run (must fail):
```bash
cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "ControlOverviewSnapshotModelTests" --no-build 2>&1 | tail -10
```

### Implementation
- [ ] Create `src/JD.AI.Dashboard.Wasm/Models/ControlOverviewModels.cs`:

```csharp
using System.Text.Json.Serialization;

namespace JD.AI.Dashboard.Wasm.Models;

/// <summary>Derived display model for the Control > Overview page.</summary>
public sealed record ControlOverviewSnapshotModel
{
    public string StatusText { get; init; } = "Disconnected";
    public string StatusColor { get; init; } = "error";   // MudBlazor color name
    public string UptimeDisplay { get; init; } = "—";
    public string TickInterval { get; init; } = "—";
    public string LastChannelsRefresh { get; init; } = "—";
    public string CostDisplay { get; init; } = "$0.00";
    public int TokenCount { get; init; }
    public int MessageCount { get; init; }
    public int SessionCount { get; init; }
    public int ActiveAgents { get; init; }
    public int ActiveChannels { get; init; }

    public static ControlOverviewSnapshotModel From(GatewayStatus? status, SessionInfo[] sessions)
    {
        if (status is null)
            return new ControlOverviewSnapshotModel();

        var uptimeSpan = DateTimeOffset.UtcNow - status.Uptime;
        var uptimeDisplay = uptimeSpan.TotalDays >= 1
            ? $"{(int)uptimeSpan.TotalDays}d {uptimeSpan.Hours}h"
            : uptimeSpan.TotalHours >= 1
                ? $"{(int)uptimeSpan.TotalHours}h"
                : $"{(int)uptimeSpan.TotalMinutes}m";

        return new ControlOverviewSnapshotModel
        {
            StatusText = string.IsNullOrWhiteSpace(status.Status) ? "Unknown"
                : status.Status.Equals("running", StringComparison.OrdinalIgnoreCase) ? "OK"
                : status.Status,
            StatusColor = status.IsRunning ? "success" : "warning",
            UptimeDisplay = uptimeDisplay,
            TickInterval = "30s",       // gateway default; extend if API exposes it
            LastChannelsRefresh = "just now",
            CostDisplay = "$0.00",
            SessionCount = sessions.Length,
            ActiveAgents = status.ActiveAgents,
            ActiveChannels = status.ActiveChannels,
        };
    }
}

/// <summary>Recent session row for the overview table.</summary>
public sealed record RecentSessionRow
{
    public string SessionKey { get; init; } = "";
    public string Model { get; init; } = "";
    public string TimeAgo { get; init; } = "";

    public static RecentSessionRow From(SessionInfo s)
    {
        var elapsed = DateTimeOffset.UtcNow - s.UpdatedAt;
        var timeAgo = elapsed.TotalDays >= 1 ? $"{(int)elapsed.TotalDays}d ago"
            : elapsed.TotalHours >= 1 ? $"{(int)elapsed.TotalHours}h ago"
            : elapsed.TotalMinutes >= 1 ? $"{(int)elapsed.TotalMinutes}m ago"
            : "just now";

        return new RecentSessionRow
        {
            SessionKey = s.Id,
            Model = s.ModelId ?? "—",
            TimeAgo = timeAgo,
        };
    }
}
```

### Verify
```bash
cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "ControlOverviewSnapshotModelTests" 2>&1 | tail -15
```

---

## Step 2 — Gateway access settings model and localStorage persistence

### Why
The Connect form stores WebSocket URL, token, default session key, and language in `localStorage`. A typed `GatewayAccessSettings` record + a `LocalStorageService` wrapper keeps the page from scattering JS interop calls.

### Files
- `src/JD.AI.Dashboard.Wasm/Models/ControlOverviewModels.cs` *(extend)*
- `src/JD.AI.Dashboard.Wasm/Services/LocalStorageService.cs` *(new)*

### Failing test first
`tests/JD.AI.Tests/Dashboard/LocalStorageServiceTests.cs`

```csharp
using Bunit;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace JD.AI.Tests.Dashboard;

public sealed class LocalStorageServiceTests : DashboardBunitTestContext
{
    [Fact]
    public async Task GetAsync_WhenKeyAbsent_ReturnsNull()
    {
        JSInterop.SetupVoid("localStorage.setItem", _ => true);
        JSInterop.Setup<string?>("localStorage.getItem", "openclaw.control.gateway.url").SetResult(null);

        var svc = new LocalStorageService(Services.GetRequiredService<IJSRuntime>());
        var result = await svc.GetAsync("openclaw.control.gateway.url");

        result.Should().BeNull();
    }
}
```

Run (must fail):
```bash
cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "LocalStorageServiceTests" --no-build 2>&1 | tail -10
```

### Implementation
- [ ] Append to `ControlOverviewModels.cs`:

```csharp
public sealed class GatewayAccessSettings
{
    public string WebSocketUrl { get; set; } = "";
    public string GatewayToken { get; set; } = "";
    public string DefaultSessionKey { get; set; } = "";
    public string Language { get; set; } = "en";

    public static readonly string[] SupportedLanguages =
    [
        "en", "zh-Hans", "zh-Hant", "pt", "de", "es", "ja", "ko", "fr", "tr", "uk", "id", "pl"
    ];

    public static readonly string[] LanguageDisplayNames =
    [
        "English", "简体中文", "繁體中文", "Português", "Deutsch",
        "Español", "日本語", "한국어", "Français", "Türkçe",
        "Українська", "Bahasa Indonesia", "Polski"
    ];
}
```

- [ ] Create `src/JD.AI.Dashboard.Wasm/Services/LocalStorageService.cs`:

```csharp
using Microsoft.JSInterop;

namespace JD.AI.Dashboard.Wasm.Services;

public sealed class LocalStorageService(IJSRuntime js)
{
    public ValueTask<string?> GetAsync(string key) =>
        js.InvokeAsync<string?>("localStorage.getItem", key);

    public ValueTask SetAsync(string key, string value) =>
        js.InvokeVoidAsync("localStorage.setItem", key, value);
}
```

- [ ] Register in `Program.cs`:

```csharp
builder.Services.AddScoped<LocalStorageService>();
```

### Verify
```bash
cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "LocalStorageServiceTests" 2>&1 | tail -15
```

---

## Step 3 — bunit tests for `ControlOverview` page rendering

### Why
Write the bunit component tests before the page exists — TDD gate that defines exactly what `data-testid` attributes must be present and what state transitions must work.

### Files
- `tests/JD.AI.Tests/Dashboard/ControlOverviewBunitTests.cs` *(new)*

### Failing test first (all tests in this file fail — page doesn't exist yet)
```csharp
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
            .Should().Be("Status, entry points, health.");
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
        cut.Find("[data-testid='snapshot-status']").TextContent.Should().Contain("Disconnected");
    }

    // ── Operational counters ────────────────────────────────────

    [Fact]
    public void ControlOverview_ShowsAgentCount()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='counter-agents']").TextContent.Trim().Should().Be("1");
    }

    [Fact]
    public void ControlOverview_ShowsChannelCount()
    {
        RegisterServices();
        var cut = RenderWithMudProviders<ControlOverview>();
        cut.Find("[data-testid='counter-channels']").TextContent.Trim().Should().Be("1");
    }
}
```

Run (must fail — `ControlOverview` type does not exist):
```bash
cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "ControlOverviewBunitTests" --no-build 2>&1 | tail -10
```

---

## Step 4 — Implement `ControlOverview.razor` page

### Files
- `src/JD.AI.Dashboard.Wasm/Pages/ControlOverview.razor` *(new)*
- `src/JD.AI.Dashboard.Wasm/Pages/Home.razor` *(remove `@page "/"` directive — replace with redirect)*

### Implementation
- [ ] Create `src/JD.AI.Dashboard.Wasm/Pages/ControlOverview.razor`:

```razor
@page "/control/overview"
@page "/"
@inject GatewayApiClient Api
@inject SignalRService SignalR
@inject LocalStorageService Storage
@inject ISnackbar Snackbar
@implements IAsyncDisposable

<PageTitle>Overview — JD.AI</PageTitle>

<div class="jd-page-header">
    <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
        <MudStack Spacing="0">
            <MudText Typo="Typo.h4" data-testid="page-title">Overview</MudText>
            <MudText Typo="Typo.body2" Color="Color.Secondary" data-testid="page-subtitle">Status, entry points, health.</MudText>
        </MudStack>
        <MudIconButton Icon="@Icons.Material.Filled.Refresh"
                       OnClick="RefreshAsync"
                       Size="Size.Small"
                       data-testid="btn-refresh" />
    </MudStack>
</div>

@* ── Gateway Access Card ─────────────────────────────────────── *@
<MudPaper Class="pa-4 mb-4" Elevation="0" data-testid="gateway-access-card">
    <MudText Typo="Typo.h6" Class="mb-3">Gateway Access</MudText>
    <MudGrid>
        <MudItem xs="12" md="6">
            <MudTextField @bind-Value="_settings.WebSocketUrl"
                          Label="WebSocket URL"
                          Variant="Variant.Outlined"
                          Adornment="Adornment.Start"
                          AdornmentIcon="@Icons.Material.Filled.Link"
                          data-testid="input-websocket-url" />
        </MudItem>
        <MudItem xs="12" md="6">
            <MudTextField @bind-Value="_settings.GatewayToken"
                          Label="Gateway Token"
                          InputType="@(_showToken ? InputType.Text : InputType.Password)"
                          Variant="Variant.Outlined"
                          Adornment="Adornment.End"
                          AdornmentIcon="@(_showToken ? Icons.Material.Filled.VisibilityOff : Icons.Material.Filled.Visibility)"
                          OnAdornmentClick="@(() => _showToken = !_showToken)"
                          data-testid="input-gateway-token" />
        </MudItem>
        <MudItem xs="12" md="6">
            <MudTextField @bind-Value="_settings.DefaultSessionKey"
                          Label="Default Session Key"
                          Variant="Variant.Outlined"
                          data-testid="input-session-key" />
        </MudItem>
        <MudItem xs="12" md="3">
            <MudTextField Label="Password (not stored)"
                          InputType="InputType.Password"
                          Variant="Variant.Outlined"
                          @bind-Value="_password"
                          data-testid="input-password" />
        </MudItem>
        <MudItem xs="12" md="3">
            <MudSelect T="string"
                       @bind-Value="_settings.Language"
                       Label="Language"
                       Variant="Variant.Outlined"
                       data-testid="select-language">
                @for (var i = 0; i < GatewayAccessSettings.SupportedLanguages.Length; i++)
                {
                    var code = GatewayAccessSettings.SupportedLanguages[i];
                    var name = GatewayAccessSettings.LanguageDisplayNames[i];
                    <MudSelectItem T="string" Value="@code">@name</MudSelectItem>
                }
            </MudSelect>
        </MudItem>
        <MudItem xs="12">
            <MudStack Row="true" Spacing="2">
                <MudButton Variant="Variant.Filled"
                           Color="Color.Primary"
                           OnClick="ConnectAsync"
                           data-testid="btn-connect">Connect</MudButton>
                <MudButton Variant="Variant.Outlined"
                           OnClick="SaveSettingsAsync"
                           data-testid="btn-save-settings">Save</MudButton>
            </MudStack>
        </MudItem>
    </MudGrid>
</MudPaper>

@* ── Snapshot Cards ──────────────────────────────────────────── *@
@if (!_loaded)
{
    <MudGrid Class="mb-4">
        @for (var i = 0; i < 4; i++)
        {
            <MudItem xs="6" sm="3">
                <MudPaper Class="pa-3" Elevation="0" data-testid="skeleton-snapshot">
                    <MudSkeleton Width="60px" Height="12px" Class="mb-1" />
                    <MudSkeleton Width="40px" Height="24px" />
                </MudPaper>
            </MudItem>
        }
    </MudGrid>
}
else
{
    <MudGrid Class="mb-4">
        <MudItem xs="6" sm="3">
            <MudPaper Class="pa-3" Elevation="0" data-testid="snapshot-card-status">
                <MudText Typo="Typo.caption" Class="mud-text-secondary" Style="text-transform:uppercase;letter-spacing:0.06em;font-weight:600;">STATUS</MudText>
                <MudText Typo="Typo.h5" Style="font-weight:700;" Color="@SnapshotStatusColor" data-testid="snapshot-status">@_snapshot.StatusText</MudText>
            </MudPaper>
        </MudItem>
        <MudItem xs="6" sm="3">
            <MudPaper Class="pa-3" Elevation="0" data-testid="snapshot-card-uptime">
                <MudText Typo="Typo.caption" Class="mud-text-secondary" Style="text-transform:uppercase;letter-spacing:0.06em;font-weight:600;">UPTIME</MudText>
                <MudText Typo="Typo.h5" Style="font-weight:700;" data-testid="snapshot-uptime">@_snapshot.UptimeDisplay</MudText>
            </MudPaper>
        </MudItem>
        <MudItem xs="6" sm="3">
            <MudPaper Class="pa-3" Elevation="0" data-testid="snapshot-card-tick">
                <MudText Typo="Typo.caption" Class="mud-text-secondary" Style="text-transform:uppercase;letter-spacing:0.06em;font-weight:600;">TICK INTERVAL</MudText>
                <MudText Typo="Typo.h5" Style="font-weight:700;" data-testid="snapshot-tick">@_snapshot.TickInterval</MudText>
            </MudPaper>
        </MudItem>
        <MudItem xs="6" sm="3">
            <MudPaper Class="pa-3" Elevation="0" data-testid="snapshot-card-refresh">
                <MudText Typo="Typo.caption" Class="mud-text-secondary" Style="text-transform:uppercase;letter-spacing:0.06em;font-weight:600;">LAST CHANNELS REFRESH</MudText>
                <MudText Typo="Typo.h5" Style="font-weight:700;" data-testid="snapshot-last-refresh">@_snapshot.LastChannelsRefresh</MudText>
            </MudPaper>
        </MudItem>
    </MudGrid>

    @* ── Operational Counters ──────────────────────────────────── *@
    <MudGrid Class="mb-4">
        <MudItem xs="6" sm="4">
            <MudPaper Class="pa-4 jd-stat-card" Elevation="0" data-testid="counter-card-sessions">
                <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="3">
                    <MudAvatar Color="Color.Tertiary" Variant="Variant.Outlined" Size="Size.Medium">
                        <MudIcon Icon="@Icons.Material.Filled.Forum" />
                    </MudAvatar>
                    <MudStack Spacing="0">
                        <MudText Typo="Typo.caption" Class="mud-text-secondary" Style="text-transform:uppercase;letter-spacing:0.06em;font-weight:600;">SESSIONS</MudText>
                        <MudText Typo="Typo.h4" Style="font-weight:700;" data-testid="counter-sessions">@_snapshot.SessionCount</MudText>
                    </MudStack>
                </MudStack>
            </MudPaper>
        </MudItem>
        <MudItem xs="6" sm="4">
            <MudPaper Class="pa-4 jd-stat-card" Elevation="0" data-testid="counter-card-agents">
                <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="3">
                    <MudAvatar Color="Color.Primary" Variant="Variant.Outlined" Size="Size.Medium">
                        <MudIcon Icon="@Icons.Material.Filled.SmartToy" />
                    </MudAvatar>
                    <MudStack Spacing="0">
                        <MudText Typo="Typo.caption" Class="mud-text-secondary" Style="text-transform:uppercase;letter-spacing:0.06em;font-weight:600;">AGENTS</MudText>
                        <MudText Typo="Typo.h4" Style="font-weight:700;" data-testid="counter-agents">@_snapshot.ActiveAgents</MudText>
                    </MudStack>
                </MudStack>
            </MudPaper>
        </MudItem>
        <MudItem xs="6" sm="4">
            <MudPaper Class="pa-4 jd-stat-card" Elevation="0" data-testid="counter-card-channels">
                <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="3">
                    <MudAvatar Color="Color.Secondary" Variant="Variant.Outlined" Size="Size.Medium">
                        <MudIcon Icon="@Icons.Material.Filled.Cable" />
                    </MudAvatar>
                    <MudStack Spacing="0">
                        <MudText Typo="Typo.caption" Class="mud-text-secondary" Style="text-transform:uppercase;letter-spacing:0.06em;font-weight:600;">CHANNELS</MudText>
                        <MudText Typo="Typo.h4" Style="font-weight:700;" data-testid="counter-channels">@_snapshot.ActiveChannels</MudText>
                    </MudStack>
                </MudStack>
            </MudPaper>
        </MudItem>
    </MudGrid>

    @* ── Alerts ────────────────────────────────────────────────── *@
    @if (_alerts.Count > 0)
    {
        <div class="mb-4" data-testid="alerts-section">
            @foreach (var alert in _alerts)
            {
                <MudAlert Severity="Severity.Warning" Class="mb-2" data-testid="alert-item">@alert</MudAlert>
            }
        </div>
    }

    @* ── Recent Sessions ───────────────────────────────────────── *@
    <MudPaper Class="pa-4" Elevation="0" data-testid="recent-sessions-card">
        <MudText Typo="Typo.h6" Class="mb-3">Recent Sessions</MudText>
        @if (_recentSessions.Count == 0)
        {
            <MudStack AlignItems="AlignItems.Center" Spacing="2" Class="pa-4" data-testid="sessions-empty">
                <MudIcon Icon="@Icons.Material.Filled.Inbox" Size="Size.Large" Color="Color.Default" Style="opacity:0.3" />
                <MudText Color="Color.Secondary" Typo="Typo.body2">No recent sessions.</MudText>
            </MudStack>
        }
        else
        {
            <MudSimpleTable Dense="true" Hover="true" data-testid="sessions-table">
                <thead>
                    <tr>
                        <th>Session Key</th>
                        <th>Model</th>
                        <th>Last Active</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var row in _recentSessions)
                    {
                        <tr data-testid="session-row">
                            <td data-testid="session-key"><MudText Typo="Typo.body2">@row.SessionKey</MudText></td>
                            <td data-testid="session-model"><MudText Typo="Typo.body2">@row.Model</MudText></td>
                            <td data-testid="session-time-ago"><MudText Typo="Typo.body2" Color="Color.Secondary">@row.TimeAgo</MudText></td>
                        </tr>
                    }
                </tbody>
            </MudSimpleTable>
        }
    </MudPaper>
}

@code {
    private GatewayAccessSettings _settings = new();
    private string _password = "";
    private bool _showToken;
    private bool _loaded;
    private ControlOverviewSnapshotModel _snapshot = new();
    private List<RecentSessionRow> _recentSessions = [];
    private List<string> _alerts = [];

    private CancellationTokenSource? _timerCts;
    private EventHandler<ActivityEventArgs>? _activityHandler;

    private Color SnapshotStatusColor => _snapshot.StatusColor switch
    {
        "success" => Color.Success,
        "warning" => Color.Warning,
        _ => Color.Error,
    };

    protected override async Task OnInitializedAsync()
    {
        await LoadSettingsAsync();

        _activityHandler = (_, _) => InvokeAsync(StateHasChanged);
        SignalR.OnActivityEvent += _activityHandler;

        await RefreshAsync();
        StartAutoRefresh();
    }

    private void StartAutoRefresh()
    {
        _timerCts = new CancellationTokenSource();
        var token = _timerCts.Token;
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (!token.IsCancellationRequested && await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                await InvokeAsync(RefreshAsync).ConfigureAwait(false);
            }
        }, token);
    }

    private async Task RefreshAsync()
    {
        GatewayStatus? status = null;
        SessionInfo[] sessions = [];

        try { status = await Api.GetStatusAsync(); }
        catch { /* gateway unreachable — snapshot shows Disconnected */ }

        try { sessions = await Api.GetSessionsAsync(limit: 10); }
        catch { /* non-fatal */ }

        _snapshot = ControlOverviewSnapshotModel.From(status, sessions);
        _recentSessions = sessions.Select(RecentSessionRow.From).ToList();
        _alerts = BuildAlerts(status);
        _loaded = true;

        await InvokeAsync(StateHasChanged);
    }

    private static List<string> BuildAlerts(GatewayStatus? status)
    {
        var alerts = new List<string>();
        if (status is null)
            alerts.Add("Gateway is unreachable. Check connection settings.");
        return alerts;
    }

    private async Task ConnectAsync()
    {
        await SaveSettingsAsync();
        Snackbar.Add("Reconnecting…", Severity.Info);
        await RefreshAsync();
    }

    private async Task SaveSettingsAsync()
    {
        await Storage.SetAsync("openclaw.control.gateway.url", _settings.WebSocketUrl);
        await Storage.SetAsync("openclaw.control.gateway.token", _settings.GatewayToken);
        await Storage.SetAsync("openclaw.control.gateway.sessionKey", _settings.DefaultSessionKey);
        await Storage.SetAsync("openclaw.control.gateway.language", _settings.Language);
    }

    private async Task LoadSettingsAsync()
    {
        _settings.WebSocketUrl = await Storage.GetAsync("openclaw.control.gateway.url") ?? "";
        _settings.GatewayToken = await Storage.GetAsync("openclaw.control.gateway.token") ?? "";
        _settings.DefaultSessionKey = await Storage.GetAsync("openclaw.control.gateway.sessionKey") ?? "";
        _settings.Language = await Storage.GetAsync("openclaw.control.gateway.language") ?? "en";
    }

    public async ValueTask DisposeAsync()
    {
        if (_activityHandler is not null)
            SignalR.OnActivityEvent -= _activityHandler;

        if (_timerCts is not null)
        {
            await _timerCts.CancelAsync();
            _timerCts.Dispose();
        }
    }
}
```

- [ ] Edit `src/JD.AI.Dashboard.Wasm/Pages/Home.razor` — remove `@page "/"` (keep the file as a legacy stub or redirect; since `ControlOverview` now owns `/`, simply delete the `@page "/"` directive from `Home.razor` and optionally rename the file — but do NOT delete `Home.razor` until the feature file test suite is updated in Step 7).

### Verify bunit tests pass
```bash
cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "ControlOverviewBunitTests" 2>&1 | tail -20
```

---

## Step 5 — Update NavMenu to add Control > Overview entry

### Files
- `src/JD.AI.Dashboard.Wasm/Layout/NavMenu.razor`

### Implementation
- [ ] Replace the existing flat "Overview" link with a grouped Control section:

```razor
<MudNavGroup Title="Control" Icon="@Icons.Material.Filled.Tune" Expanded="true" data-testid="nav-group-control">
    <MudNavLink Href="/control/overview" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.Dashboard" data-testid="nav-control-overview">
        Overview
    </MudNavLink>
</MudNavGroup>
```

Keep all other `MudNavLink` entries unchanged below the group.

### Verify build
```bash
cd /c/git/JD.AI && dotnet build src/JD.AI.Dashboard.Wasm/ 2>&1 | tail -15
```

---

## Step 6 — Playwright / Reqnroll E2E feature file

### Files
- `tests/JD.AI.Specs.UI/Features/Dashboard/ControlOverviewPage.feature` *(new)*

### Implementation
- [ ] Create the feature file:

```gherkin
@ui
Feature: Control > Overview Page
    As a gateway operator
    I want a dedicated /control/overview route
    So that I can immediately assess gateway health on arrival

    Background:
        Given I navigate to "/control/overview"

    @smoke
    Scenario: Page renders title and subtitle
        Then I should see the heading "Overview"
        And I should see the text "Status, entry points, health."

    @smoke
    Scenario: Snapshot cards are visible
        Then I should see element "[data-testid='snapshot-card-status']"
        And I should see element "[data-testid='snapshot-card-uptime']"
        And I should see element "[data-testid='snapshot-card-tick']"
        And I should see element "[data-testid='snapshot-card-refresh']"

    Scenario: Gateway access form is visible
        Then I should see element "[data-testid='input-websocket-url']"
        And I should see element "[data-testid='input-gateway-token']"
        And I should see element "[data-testid='input-password']"
        And I should see element "[data-testid='select-language']"
        And I should see element "[data-testid='btn-connect']"
        And I should see element "[data-testid='btn-refresh']"

    Scenario: Refresh button triggers data reload
        When I click "[data-testid='btn-refresh']"
        Then the page should not show an error state

    Scenario: Home route redirects to Control Overview
        Given I navigate to "/"
        Then I should see the heading "Overview"

    Scenario: Sidebar Control > Overview link is active on this route
        Then the nav link "[data-testid='nav-control-overview']" should be active

    Scenario: Recent sessions table shows session key column
        Then I should see element "[data-testid='recent-sessions-card']"

    Scenario: Counter cards show agent and channel counts
        Then I should see element "[data-testid='counter-card-agents']"
        And I should see element "[data-testid='counter-card-channels']"
        And I should see element "[data-testid='counter-card-sessions']"
```

Note: The step definitions reuse the existing `CommonStepDefinitions.cs` pattern from `HomePage.feature`. If a `Given I navigate to` step is missing, add it to `CommonStepDefinitions.cs` (do not create a new step definition file for this).

### Verify feature file is discovered
```bash
cd /c/git/JD.AI && dotnet test tests/JD.AI.Specs.UI/ --list-tests 2>&1 | grep -i "Overview"
```

---

## Step 7 — Update `HomePage.feature` to reflect route change

### Files
- `tests/JD.AI.Specs.UI/Features/Dashboard/HomePage.feature`

### Implementation
- [ ] Change the Background step from `Given I am on the home page` to `Given I navigate to "/"` (matches the redirect behavior).
- [ ] Update the "Displays overview heading" scenario — the heading is now rendered by `ControlOverview`, but the text displayed at `/` is still "Overview". Adjust assertions accordingly.
- [ ] Remove the OpenClaw-specific scenarios (`stat card labeled "OpenClaw"`, `OpenClaw bridge table shown`) — they are no longer on the Overview page.

### Verify
```bash
cd /c/git/JD.AI && dotnet build tests/JD.AI.Specs.UI/ 2>&1 | tail -10
```

---

## Step 8 — Full test suite verification

### Run all dashboard unit tests
```bash
cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "Category=Dashboard|FullyQualifiedName~Dashboard" 2>&1 | tail -20
```

### Run all unit tests
```bash
cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ 2>&1 | tail -20
```

### Run Playwright E2E smoke suite (requires running gateway)
```bash
cd /c/git/JD.AI && dotnet test tests/JD.AI.Specs.UI/ --filter "Category=smoke" 2>&1 | tail -20
```

---

## Self-Review Checklist

- [ ] `/control/overview` route registers correctly; `/` redirects without a double-render
- [ ] All `data-testid` attributes present on every interactive element and data display
- [ ] `ControlOverviewSnapshotModel.From(null, [])` returns safe defaults (no NullReferenceException)
- [ ] `LocalStorageService` gracefully handles null returns from JS interop
- [ ] `PeriodicTimer` is cancelled and disposed in `DisposeAsync` — no fire-and-forget leaks
- [ ] `Home.razor` no longer declares `@page "/"` (no duplicate route conflict)
- [ ] NavMenu group "Control" contains the Overview link with correct `data-testid`
- [ ] bunit tests: all pass with no skips
- [ ] Feature file: all scenarios map to existing or extended step definitions
- [ ] No OpenClaw-specific cards remain on the Overview page
- [ ] Build produces zero warnings related to new files (`dotnet build src/JD.AI.Dashboard.Wasm/`)
