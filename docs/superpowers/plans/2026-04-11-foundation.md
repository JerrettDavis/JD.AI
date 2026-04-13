# Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure the sidebar into 4 collapsible MVP groups, add a theme toggle with localStorage persistence, wire ISnackbar toast notifications, and build a gateway auth gate component shown when the gateway is disconnected.

**Architecture:** NavMenu.razor is replaced with a grouped `MudNavGroup`-based structure backed by a `NavState` service that persists expand/collapse state to localStorage via `IJSRuntime`. A new `ThemeService` wraps localStorage-backed light/dark/system preference and exposes a `MudTheme` instance; `MainLayout.razor` binds to it. A new `GatewayAuthGate.razor` component conditionally covers the `@Body` slot when `SignalRService.IsConnected` is false, presenting a WebSocket URL + token form that calls `SignalRService.ConnectAsync`.

**Tech Stack:** Blazor WASM .NET 10, MudBlazor v8.5.1, xunit/bunit, Playwright/Reqnroll

---

## Issue cross-reference

| Task group | GitHub issue |
|------------|-------------|
| Sidebar restructure | #469 |
| Theme system | #470 |
| Toast notifications | #470 |
| Gateway auth gate | #470 |

---

## Task 1 — NavState service (localStorage persistence)

### 1.1 — Write failing bunit test for NavState

- [ ] Create `tests/JD.AI.Tests/Dashboard/NavStateTests.cs`:

```csharp
using Bunit;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace JD.AI.Tests.Dashboard;

public sealed class NavStateTests : BunitContext
{
    public NavStateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task NavState_DefaultsAllGroupsExpanded()
    {
        JSInterop.SetupVoid("localStorage.setItem", _ => true);
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);

        var svc = new NavState(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();

        Assert.True(await svc.IsExpandedAsync("control"));
        Assert.True(await svc.IsExpandedAsync("agents"));
        Assert.True(await svc.IsExpandedAsync("settings"));
    }

    [Fact]
    public async Task NavState_PersistsToggleToLocalStorage()
    {
        var stored = new Dictionary<string, string?>();
        JSInterop.SetupVoid("localStorage.setItem", inv =>
        {
            stored[(string)inv.Arguments[0]!] = (string?)inv.Arguments[1];
        });
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);

        var svc = new NavState(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();
        await svc.ToggleAsync("control");

        Assert.False(await svc.IsExpandedAsync("control"));
        Assert.Contains("jd-nav-control", stored.Keys);
    }

    [Fact]
    public async Task NavState_ReadsPersistedStateOnInit()
    {
        JSInterop.Setup<string?>("localStorage.getItem",
            inv => (string)inv.Arguments[0]! == "jd-nav-agents")
            .SetResult("false");
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);

        var svc = new NavState(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();

        Assert.False(await svc.IsExpandedAsync("agents"));
        Assert.True(await svc.IsExpandedAsync("settings"));
    }
}
```

- [ ] Run tests to confirm RED:
  ```bash
  cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/Dashboard/NavStateTests.cs --no-build 2>&1 | tail -20
  ```
  Expected: compile error — `NavState` does not exist.

### 1.2 — Implement NavState service

- [ ] Create `src/JD.AI.Dashboard.Wasm/Services/NavState.cs`:

```csharp
using Microsoft.JSInterop;

namespace JD.AI.Dashboard.Wasm.Services;

public sealed class NavState(IJSRuntime js)
{
    private static readonly string[] Groups = ["control", "agents", "settings"];
    private readonly Dictionary<string, bool> _state = [];

    public async Task InitAsync()
    {
        foreach (var group in Groups)
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", $"jd-nav-{group}");
            _state[group] = raw is null || raw == "true";
        }
    }

    public Task<bool> IsExpandedAsync(string group) =>
        Task.FromResult(_state.GetValueOrDefault(group, true));

    public async Task ToggleAsync(string group)
    {
        var next = !_state.GetValueOrDefault(group, true);
        _state[group] = next;
        await js.InvokeVoidAsync("localStorage.setItem", $"jd-nav-{group}", next.ToString().ToLowerInvariant());
    }

    public async Task SetExpandedAsync(string group, bool expanded)
    {
        _state[group] = expanded;
        await js.InvokeVoidAsync("localStorage.setItem", $"jd-nav-{group}", expanded.ToString().ToLowerInvariant());
    }
}
```

- [ ] Register in `Program.cs` — add after existing service registrations:
  ```csharp
  builder.Services.AddScoped<NavState>();
  ```

- [ ] Run tests to confirm GREEN:
  ```bash
  cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "NavStateTests" 2>&1 | tail -10
  ```
  Expected: `3 passed, 0 failed`

---

## Task 2 — Sidebar restructure (#469)

### 2.1 — Write failing bunit test for grouped NavMenu

- [ ] Create `tests/JD.AI.Tests/Dashboard/NavMenuBunitTests.cs`:

```csharp
using Bunit;
using JD.AI.Dashboard.Wasm.Layout;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace JD.AI.Tests.Dashboard;

public sealed class NavMenuBunitTests : BunitContext
{
    public NavMenuBunitTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        JSInterop.SetupVoid("localStorage.setItem", _ => true);
        Services.AddScoped<NavState>();
        Services.AddMudBlazor();
    }

    [Fact]
    public void NavMenu_RendersAllFourSections()
    {
        var cut = RenderComponent<NavMenu>();

        Assert.NotNull(cut.Find("[data-testid='nav-section-chat']"));
        Assert.NotNull(cut.Find("[data-testid='nav-section-control']"));
        Assert.NotNull(cut.Find("[data-testid='nav-section-agents']"));
        Assert.NotNull(cut.Find("[data-testid='nav-section-settings']"));
    }

    [Fact]
    public void NavMenu_ChatLinkNavigatesToChatRoute()
    {
        var cut = RenderComponent<NavMenu>();
        var chatLink = cut.Find("[data-testid='nav-chat']");
        Assert.Equal("/chat", chatLink.GetAttribute("href"));
    }

    [Fact]
    public void NavMenu_ControlGroupContainsOverviewLink()
    {
        var cut = RenderComponent<NavMenu>();
        Assert.NotNull(cut.Find("[data-testid='nav-overview']"));
    }

    [Fact]
    public void NavMenu_AgentsGroupContainsAgentsAndSkillsLinks()
    {
        var cut = RenderComponent<NavMenu>();
        Assert.NotNull(cut.Find("[data-testid='nav-agents']"));
        Assert.NotNull(cut.Find("[data-testid='nav-skills']"));
    }

    [Fact]
    public void NavMenu_SettingsGroupContainsFourLinks()
    {
        var cut = RenderComponent<NavMenu>();
        Assert.NotNull(cut.Find("[data-testid='nav-settings-ai']"));
        Assert.NotNull(cut.Find("[data-testid='nav-settings-comms']"));
        Assert.NotNull(cut.Find("[data-testid='nav-settings-config']"));
        Assert.NotNull(cut.Find("[data-testid='nav-settings-logs']"));
    }
}
```

- [ ] Run tests to confirm RED:
  ```bash
  cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "NavMenuBunitTests" 2>&1 | tail -10
  ```
  Expected: compile error or test failures — `nav-section-*` testids missing.

### 2.2 — Rewrite NavMenu.razor

- [ ] Replace `src/JD.AI.Dashboard.Wasm/Layout/NavMenu.razor` entirely:

```razor
@inject NavState NavStateService
@inject NavigationManager Nav

<MudNavMenu Dense="true" Class="pt-2" data-testid="nav-menu">

    @* ── Chat (top-level, no group) ── *@
    <MudNavLink Href="/chat"
                Match="NavLinkMatch.Prefix"
                Icon="@Icons.Material.Filled.Chat"
                data-testid="nav-chat"
                data-section="nav-section-chat">
        Chat
    </MudNavLink>

    @* ── Control ── *@
    <div data-testid="nav-section-control">
        <MudNavGroup Title="Control"
                     Icon="@Icons.Material.Filled.Dashboard"
                     Expanded="_controlExpanded"
                     ExpandedChanged="@(v => OnGroupToggle("control", v))"
                     data-testid="nav-group-control">
            <MudNavLink Href="/"
                        Match="NavLinkMatch.All"
                        Icon="@Icons.Material.Filled.GridView"
                        data-testid="nav-overview">
                Overview
            </MudNavLink>
        </MudNavGroup>
    </div>

    @* ── Agents ── *@
    <div data-testid="nav-section-agents">
        <MudNavGroup Title="Agents"
                     Icon="@Icons.Material.Filled.SmartToy"
                     Expanded="_agentsExpanded"
                     ExpandedChanged="@(v => OnGroupToggle("agents", v))"
                     data-testid="nav-group-agents">
            <MudNavLink Href="/agents"
                        Match="NavLinkMatch.Prefix"
                        Icon="@Icons.Material.Filled.PrecisionManufacturing"
                        data-testid="nav-agents">
                Agents
            </MudNavLink>
            <MudNavLink Href="/skills"
                        Match="NavLinkMatch.Prefix"
                        Icon="@Icons.Material.Filled.Extension"
                        data-testid="nav-skills">
                Skills
            </MudNavLink>
        </MudNavGroup>
    </div>

    @* ── Settings ── *@
    <div data-testid="nav-section-settings">
        <MudNavGroup Title="Settings"
                     Icon="@Icons.Material.Filled.Settings"
                     Expanded="_settingsExpanded"
                     ExpandedChanged="@(v => OnGroupToggle("settings", v))"
                     data-testid="nav-group-settings">
            <MudNavLink Href="/settings/ai"
                        Match="NavLinkMatch.Prefix"
                        Icon="@Icons.Material.Filled.Psychology"
                        data-testid="nav-settings-ai">
                AI &amp; Agents
            </MudNavLink>
            <MudNavLink Href="/settings/comms"
                        Match="NavLinkMatch.Prefix"
                        Icon="@Icons.Material.Filled.Cable"
                        data-testid="nav-settings-comms">
                Communication
            </MudNavLink>
            <MudNavLink Href="/settings/config"
                        Match="NavLinkMatch.Prefix"
                        Icon="@Icons.Material.Filled.Tune"
                        data-testid="nav-settings-config">
                Config
            </MudNavLink>
            <MudNavLink Href="/settings/logs"
                        Match="NavLinkMatch.Prefix"
                        Icon="@Icons.Material.Filled.EventNote"
                        data-testid="nav-settings-logs">
                Logs
            </MudNavLink>
        </MudNavGroup>
    </div>

</MudNavMenu>

@code {
    private bool _controlExpanded = true;
    private bool _agentsExpanded = true;
    private bool _settingsExpanded = true;

    protected override async Task OnInitializedAsync()
    {
        await NavStateService.InitAsync();
        _controlExpanded = await NavStateService.IsExpandedAsync("control");
        _agentsExpanded  = await NavStateService.IsExpandedAsync("agents");
        _settingsExpanded = await NavStateService.IsExpandedAsync("settings");
    }

    private async Task OnGroupToggle(string group, bool expanded)
    {
        await NavStateService.SetExpandedAsync(group, expanded);
        switch (group)
        {
            case "control":  _controlExpanded  = expanded; break;
            case "agents":   _agentsExpanded   = expanded; break;
            case "settings": _settingsExpanded = expanded; break;
        }
    }
}
```

- [ ] Run tests to confirm GREEN:
  ```bash
  cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "NavMenuBunitTests" 2>&1 | tail -10
  ```
  Expected: `5 passed, 0 failed`

### 2.3 — Write BDD feature for sidebar

- [ ] Create `tests/JD.AI.Specs.UI/Features/Dashboard/NavMenu.feature`:

```gherkin
@ui
Feature: Navigation Menu — Grouped Sidebar
    As a gateway operator
    I want a grouped sidebar with collapsible sections
    So that I can quickly navigate to relevant pages

    Background:
        Given I am on the home page

    @smoke
    Scenario: Chat link is always visible at top level
        Then I should see a nav link labeled "Chat" with href "/chat"

    @smoke
    Scenario: Control group is visible and expanded by default
        Then I should see the nav group "Control"
        And the "Control" group should be expanded

    @smoke
    Scenario: Agents group contains Agents and Skills links
        Then I should see the nav group "Agents"
        And I should see a nav link labeled "Agents"
        And I should see a nav link labeled "Skills"

    @smoke
    Scenario: Settings group contains four sub-links
        Then I should see the nav group "Settings"
        And I should see a nav link labeled "AI & Agents"
        And I should see a nav link labeled "Communication"
        And I should see a nav link labeled "Config"
        And I should see a nav link labeled "Logs"

    Scenario: Collapsing a group hides its links
        When I click the "Agents" nav group toggle
        Then the "Agents" group should be collapsed
        And I should not see a nav link labeled "Skills"

    Scenario: Expand/collapse state persists on reload
        When I click the "Settings" nav group toggle
        And I reload the page
        Then the "Settings" group should be collapsed
```

- [ ] Create `tests/JD.AI.Specs.UI/Features/Dashboard/NavMenu.feature.cs` (Reqnroll auto-generates; ensure the `.feature.cs` file is present and the project builds):
  ```bash
  cd /c/git/JD.AI && dotnet build tests/JD.AI.Specs.UI/ 2>&1 | tail -20
  ```

### 2.4 — Commit sidebar restructure

- [ ] Verify build is clean:
  ```bash
  cd /c/git/JD.AI && dotnet build src/JD.AI.Dashboard.Wasm/ 2>&1 | tail -10
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] Commit:
  ```bash
  cd /c/git/JD.AI && rtk git add src/JD.AI.Dashboard.Wasm/Layout/NavMenu.razor src/JD.AI.Dashboard.Wasm/Services/NavState.cs src/JD.AI.Dashboard.Wasm/Program.cs tests/JD.AI.Tests/Dashboard/NavStateTests.cs tests/JD.AI.Tests/Dashboard/NavMenuBunitTests.cs tests/JD.AI.Specs.UI/Features/Dashboard/NavMenu.feature && rtk git commit -m "feat(#469): grouped collapsible sidebar with localStorage persistence"
  ```

---

## Task 3 — Theme system (#470)

### 3.1 — Write failing bunit test for ThemeService

- [ ] Create `tests/JD.AI.Tests/Dashboard/ThemeServiceTests.cs`:

```csharp
using Bunit;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;

namespace JD.AI.Tests.Dashboard;

public sealed class ThemeServiceTests : BunitContext
{
    public ThemeServiceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task ThemeService_DefaultsToSystemPreference()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        JSInterop.Setup<bool>("window.matchMedia('(prefers-color-scheme: dark)').matches", _ => true).SetResult(true);

        var svc = new ThemeService(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();

        Assert.Equal(ThemeMode.System, svc.Mode);
        Assert.True(svc.IsDarkMode);
    }

    [Fact]
    public async Task ThemeService_LoadsPersistedLightMode()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult("light");

        var svc = new ThemeService(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();

        Assert.Equal(ThemeMode.Light, svc.Mode);
        Assert.False(svc.IsDarkMode);
    }

    [Fact]
    public async Task ThemeService_LoadsPersistedDarkMode()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult("dark");

        var svc = new ThemeService(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();

        Assert.Equal(ThemeMode.Dark, svc.Mode);
        Assert.True(svc.IsDarkMode);
    }

    [Fact]
    public async Task ThemeService_SetModePersistsToLocalStorage()
    {
        var stored = new Dictionary<string, string?>();
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        JSInterop.Setup<bool>("jdMatchesDark", _ => true).SetResult(false);
        JSInterop.SetupVoid("localStorage.setItem", inv =>
        {
            stored[(string)inv.Arguments[0]!] = (string?)inv.Arguments[1];
        });

        var svc = new ThemeService(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();
        await svc.SetModeAsync(ThemeMode.Dark);

        Assert.Equal("dark", stored.GetValueOrDefault("jd-theme"));
        Assert.True(svc.IsDarkMode);
    }

    [Fact]
    public async Task ThemeService_RaisesChangedEventOnModeSwitch()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult("light");
        JSInterop.SetupVoid("localStorage.setItem", _ => true);

        var svc = new ThemeService(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();

        var raised = false;
        svc.OnChanged += (_, _) => raised = true;
        await svc.SetModeAsync(ThemeMode.Dark);

        Assert.True(raised);
    }
}
```

- [ ] Run tests to confirm RED:
  ```bash
  cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "ThemeServiceTests" 2>&1 | tail -10
  ```
  Expected: compile error — `ThemeService` / `ThemeMode` do not exist.

### 3.2 — Implement ThemeService

- [ ] Create `src/JD.AI.Dashboard.Wasm/Services/ThemeService.cs`:

```csharp
using Microsoft.JSInterop;

namespace JD.AI.Dashboard.Wasm.Services;

public enum ThemeMode { System, Light, Dark }

public sealed class ThemeService(IJSRuntime js)
{
    public ThemeMode Mode { get; private set; } = ThemeMode.System;
    public bool IsDarkMode { get; private set; } = true;
    public event EventHandler? OnChanged;

    public async Task InitAsync()
    {
        var raw = await js.InvokeAsync<string?>("localStorage.getItem", "jd-theme");
        Mode = raw switch
        {
            "light"  => ThemeMode.Light,
            "dark"   => ThemeMode.Dark,
            _        => ThemeMode.System,
        };
        IsDarkMode = Mode switch
        {
            ThemeMode.Dark  => true,
            ThemeMode.Light => false,
            _               => await js.InvokeAsync<bool>("jdMatchesDark"),
        };
    }

    public async Task SetModeAsync(ThemeMode mode)
    {
        Mode = mode;
        IsDarkMode = mode switch
        {
            ThemeMode.Dark  => true,
            ThemeMode.Light => false,
            _               => await js.InvokeAsync<bool>("jdMatchesDark"),
        };
        var value = mode switch
        {
            ThemeMode.Light => "light",
            ThemeMode.Dark  => "dark",
            _               => "system",
        };
        await js.InvokeVoidAsync("localStorage.setItem", "jd-theme", value);
        OnChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] Add JS helper to `src/JD.AI.Dashboard.Wasm/wwwroot/index.html` just before `</body>`:
  ```html
  <script>
    window.jdMatchesDark = () => window.matchMedia('(prefers-color-scheme: dark)').matches;
  </script>
  ```

- [ ] Register in `Program.cs`:
  ```csharp
  builder.Services.AddScoped<ThemeService>();
  ```

- [ ] Run tests to confirm GREEN:
  ```bash
  cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "ThemeServiceTests" 2>&1 | tail -10
  ```
  Expected: `5 passed, 0 failed`

### 3.3 — Wire ThemeService into MainLayout

- [ ] Update `src/JD.AI.Dashboard.Wasm/Layout/MainLayout.razor`:
  - Inject `ThemeService ThemeService`
  - Change `<MudThemeProvider Theme="_theme" IsDarkMode="true" />` to `<MudThemeProvider Theme="_theme" IsDarkMode="@ThemeService.IsDarkMode" />`
  - In `OnInitializedAsync`, after existing SignalR setup:
    ```csharp
    ThemeService.OnChanged += (_, _) => InvokeAsync(StateHasChanged);
    await ThemeService.InitAsync();
    ```
  - Add theme toggle icon button to the app bar (between `MudSpacer` and the status chip):
    ```razor
    <MudIconButton Icon="@ThemeToggleIcon"
                   Color="Color.Inherit"
                   Size="Size.Small"
                   OnClick="@CycleTheme"
                   data-testid="theme-toggle"
                   Title="Toggle theme" />
    ```
  - Add supporting code block members:
    ```csharp
    private string ThemeToggleIcon => ThemeService.Mode switch
    {
        ThemeMode.Light  => Icons.Material.Filled.LightMode,
        ThemeMode.Dark   => Icons.Material.Filled.DarkMode,
        _                => Icons.Material.Filled.BrightnessAuto,
    };

    private async Task CycleTheme()
    {
        var next = ThemeService.Mode switch
        {
            ThemeMode.System => ThemeMode.Light,
            ThemeMode.Light  => ThemeMode.Dark,
            _                => ThemeMode.System,
        };
        await ThemeService.SetModeAsync(next);
    }
    ```

### 3.4 — Theme palette CSS variables (no flash on load)

- [ ] Append to `src/JD.AI.Dashboard.Wasm/wwwroot/css/app.css`:

```css
/* ── Theme: pre-load dark to prevent flash ── */
html[data-theme="light"] {
    color-scheme: light;
}
html[data-theme="dark"],
html:not([data-theme]) {
    color-scheme: dark;
}
```

- [ ] Add anti-flash script to `wwwroot/index.html` before any stylesheet links:
  ```html
  <script>
    (function() {
      var t = localStorage.getItem('jd-theme');
      if (t === 'light') document.documentElement.setAttribute('data-theme','light');
      else document.documentElement.setAttribute('data-theme','dark');
    })();
  </script>
  ```

### 3.5 — Commit theme system

- [ ] Build check:
  ```bash
  cd /c/git/JD.AI && dotnet build src/JD.AI.Dashboard.Wasm/ 2>&1 | tail -10
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] Commit:
  ```bash
  cd /c/git/JD.AI && rtk git add src/JD.AI.Dashboard.Wasm/Services/ThemeService.cs src/JD.AI.Dashboard.Wasm/Layout/MainLayout.razor src/JD.AI.Dashboard.Wasm/Program.cs src/JD.AI.Dashboard.Wasm/wwwroot/css/app.css src/JD.AI.Dashboard.Wasm/wwwroot/index.html tests/JD.AI.Tests/Dashboard/ThemeServiceTests.cs && rtk git commit -m "feat(#470): theme system with light/dark/system modes, localStorage persistence, no-flash"
  ```

---

## Task 4 — Toast notification wiring (#470)

### 4.1 — Audit existing ISnackbar usage

- [ ] Check which pages already inject `ISnackbar` and which do not:
  ```bash
  cd /c/git/JD.AI && rtk grep "ISnackbar" src/JD.AI.Dashboard.Wasm/Pages/
  ```
  Note pages that are missing `@inject ISnackbar Snackbar`.

### 4.2 — Write bunit test verifying snackbar fires on error

The pattern already exists in `Settings.razor`. Verify the Chat page (a core loop page) surfaces errors through ISnackbar rather than inline alerts.

- [ ] In `tests/JD.AI.Tests/Dashboard/ChatPageBunitTests.cs` (create if absent), add:

```csharp
using Bunit;
using JD.AI.Dashboard.Wasm.Pages;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace JD.AI.Tests.Dashboard;

public sealed class ChatPageSnackbarTests : DashboardBunitTestContext
{
    [Fact]
    public void Chat_WhenAgentListFails_ShowsErrorSnackbar()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var api = CreateApiClient(_ => throw new HttpRequestException("unreachable"));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));
        Services.AddSingleton(snackbar);

        var cut = RenderWithMudProviders<Chat>();

        snackbar.Received().Add(
            Arg.Is<string>(s => s.Contains("agent") || s.Contains("error") || s.Contains("connect")),
            Severity.Error,
            Arg.Any<Action<SnackbarOptions>?>(),
            Arg.Any<string?>());
    }
}
```

- [ ] Run tests to confirm RED (Chat does not yet use ISnackbar, or test compile fails):
  ```bash
  cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "ChatPageSnackbarTests" 2>&1 | tail -15
  ```

### 4.3 — Wire ISnackbar into Chat page

- [ ] Open `src/JD.AI.Dashboard.Wasm/Pages/Chat.razor`
- [ ] Add `@inject ISnackbar Snackbar` if missing
- [ ] In the agent list load error handler, replace any inline error state with:
  ```csharp
  Snackbar.Add("Unable to load agents. Is the gateway running?", Severity.Error);
  ```
- [ ] In the send message error handler:
  ```csharp
  Snackbar.Add($"Send failed: {ex.Message}", Severity.Error);
  ```

### 4.4 — Global snackbar defaults in Program.cs

- [ ] In `Program.cs`, after `AddMudServices()`, configure snackbar defaults:
  ```csharp
  builder.Services.AddMudServices(config =>
  {
      config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
      config.SnackbarConfiguration.ShowCloseIcon = true;
      config.SnackbarConfiguration.VisibleStateDuration = 5000;
      config.SnackbarConfiguration.HideTransitionDuration = 300;
      config.SnackbarConfiguration.ShowTransitionDuration = 200;
      config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
  });
  ```
  Note: remove the existing bare `AddMudServices()` call — replace it with the above.

- [ ] Run tests to confirm GREEN:
  ```bash
  cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "ChatPageSnackbarTests" 2>&1 | tail -10
  ```
  Expected: `1 passed, 0 failed`

### 4.5 — Commit toast wiring

- [ ] Build check:
  ```bash
  cd /c/git/JD.AI && dotnet build src/JD.AI.Dashboard.Wasm/ 2>&1 | tail -10
  ```

- [ ] Commit:
  ```bash
  cd /c/git/JD.AI && rtk git add src/JD.AI.Dashboard.Wasm/Program.cs src/JD.AI.Dashboard.Wasm/Pages/Chat.razor tests/JD.AI.Tests/Dashboard/ChatPageBunitTests.cs && rtk git commit -m "feat(#470): wire ISnackbar toast notifications, global bottom-right defaults"
  ```

---

## Task 5 — Gateway auth gate (#470)

### 5.1 — Write failing bunit test for GatewayAuthGate

- [ ] Create `tests/JD.AI.Tests/Dashboard/GatewayAuthGateTests.cs`:

```csharp
using Bunit;
using JD.AI.Dashboard.Wasm.Components;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSubstitute;

namespace JD.AI.Tests.Dashboard;

public sealed class GatewayAuthGateTests : BunitContext
{
    public GatewayAuthGateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        JSInterop.SetupVoid("localStorage.setItem", _ => true);
        Services.AddMudServices();
    }

    [Fact]
    public void GatewayAuthGate_WhenDisconnected_ShowsForm()
    {
        var signalR = Substitute.For<ISignalRService>();
        signalR.IsConnected.Returns(false);
        Services.AddSingleton(signalR);

        var cut = RenderComponent<GatewayAuthGate>();

        Assert.NotNull(cut.Find("[data-testid='auth-gate']"));
        Assert.NotNull(cut.Find("[data-testid='gateway-url-input']"));
        Assert.NotNull(cut.Find("[data-testid='gateway-token-input']"));
        Assert.NotNull(cut.Find("[data-testid='connect-button']"));
    }

    [Fact]
    public void GatewayAuthGate_WhenConnected_HidesForm()
    {
        var signalR = Substitute.For<ISignalRService>();
        signalR.IsConnected.Returns(true);
        Services.AddSingleton(signalR);

        var cut = RenderComponent<GatewayAuthGate>();

        Assert.Empty(cut.FindAll("[data-testid='auth-gate']"));
    }

    [Fact]
    public void GatewayAuthGate_PreFillsDefaultUrl()
    {
        var signalR = Substitute.For<ISignalRService>();
        signalR.IsConnected.Returns(false);
        Services.AddSingleton(signalR);

        var cut = RenderComponent<GatewayAuthGate>();

        var urlInput = cut.Find("[data-testid='gateway-url-input'] input");
        Assert.Equal("ws://127.0.0.1:18789", urlInput.GetAttribute("value"));
    }

    [Fact]
    public void GatewayAuthGate_PreFillsUrlFromLocalStorage()
    {
        JSInterop.Setup<string?>("localStorage.getItem",
            inv => (string)inv.Arguments[0]! == "jd-gateway-url")
            .SetResult("ws://192.168.1.10:18789");

        var signalR = Substitute.For<ISignalRService>();
        signalR.IsConnected.Returns(false);
        Services.AddSingleton(signalR);

        var cut = RenderComponent<GatewayAuthGate>();

        var urlInput = cut.Find("[data-testid='gateway-url-input'] input");
        Assert.Equal("ws://192.168.1.10:18789", urlInput.GetAttribute("value"));
    }

    [Fact]
    public async Task GatewayAuthGate_ConnectButtonCallsConnectAsync()
    {
        var signalR = Substitute.For<ISignalRService>();
        signalR.IsConnected.Returns(false);
        Services.AddSingleton(signalR);

        var cut = RenderComponent<GatewayAuthGate>();

        await cut.Find("[data-testid='connect-button']").ClickAsync(new());

        await signalR.Received().ConnectAsync(
            Arg.Is<string>(s => s.StartsWith("ws://")),
            Arg.Any<string?>());
    }
}
```

- [ ] Run tests to confirm RED:
  ```bash
  cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "GatewayAuthGateTests" 2>&1 | tail -15
  ```
  Expected: compile error — `GatewayAuthGate` component / `ISignalRService` do not exist.

### 5.2 — Extract ISignalRService interface

The tests mock `ISignalRService`. Introduce the interface so the component and tests can use it.

- [ ] Add to `src/JD.AI.Dashboard.Wasm/Services/SignalRService.cs` (before the class declaration):

```csharp
public interface ISignalRService
{
    bool IsConnected { get; }
    string ConnectionError { get; }
    event EventHandler? OnStateChanged;
    Task ConnectAsync(string baseUrl, string? token = null);
    Task ConnectAsync();
}
```

- [ ] Make `SignalRService` implement `ISignalRService`:
  ```csharp
  public sealed class SignalRService : ISignalRService, IAsyncDisposable
  ```

- [ ] Add the overload `ConnectAsync(string baseUrl, string? token)` to `SignalRService`:
  ```csharp
  public async Task ConnectAsync(string baseUrl, string? token)
  {
      _baseUrl = baseUrl.TrimEnd('/');
      // optionally store token for authenticated endpoints
      await ConnectAsync();
  }
  ```

- [ ] Update `Program.cs` to register both the concrete type and the interface:
  ```csharp
  builder.Services.AddSingleton<SignalRService>(sp => new SignalRService(gatewayUrl));
  builder.Services.AddSingleton<ISignalRService>(sp => sp.GetRequiredService<SignalRService>());
  ```

### 5.3 — Implement GatewayAuthGate component

- [ ] Create `src/JD.AI.Dashboard.Wasm/Components/GatewayAuthGate.razor`:

```razor
@inject ISignalRService SignalR
@inject IJSRuntime JS
@inject ISnackbar Snackbar

@if (!SignalR.IsConnected)
{
    <div class="jd-auth-gate-overlay" data-testid="auth-gate">
        <MudPaper Elevation="4" Class="jd-auth-gate-card pa-8">
            <MudStack Spacing="4" AlignItems="AlignItems.Center">

                <MudIcon Icon="@Icons.Material.Filled.CloudOff" Color="Color.Error" Size="Size.Large" />
                <MudText Typo="Typo.h5" Align="Align.Center">Connect to Gateway</MudText>
                <MudText Typo="Typo.body2" Color="Color.Secondary" Align="Align.Center">
                    Enter your JD.AI Gateway URL and token to continue.
                </MudText>

                <MudTextField @bind-Value="_gatewayUrl"
                              Label="Gateway WebSocket URL"
                              Placeholder="ws://127.0.0.1:18789"
                              Variant="Variant.Outlined"
                              data-testid="gateway-url-input"
                              FullWidth="true"
                              Class="mt-2" />

                <MudTextField @bind-Value="_token"
                              Label="Auth Token (optional)"
                              Placeholder="Leave blank if not required"
                              InputType="InputType.Password"
                              Variant="Variant.Outlined"
                              data-testid="gateway-token-input"
                              FullWidth="true" />

                <MudButton Variant="Variant.Filled"
                           Color="Color.Primary"
                           Size="Size.Large"
                           FullWidth="true"
                           Disabled="_connecting"
                           OnClick="ConnectAsync"
                           data-testid="connect-button">
                    @(_connecting ? "Connecting…" : "Connect")
                </MudButton>

                @if (!string.IsNullOrEmpty(SignalR.ConnectionError))
                {
                    <MudAlert Severity="Severity.Error" Variant="Variant.Text" data-testid="auth-gate-error">
                        @SignalR.ConnectionError
                    </MudAlert>
                }

            </MudStack>
        </MudPaper>
    </div>
}

@code {
    private string _gatewayUrl = "ws://127.0.0.1:18789";
    private string? _token;
    private bool _connecting;

    protected override async Task OnInitializedAsync()
    {
        SignalR.OnStateChanged += OnStateChanged;
        var saved = await JS.InvokeAsync<string?>("localStorage.getItem", "jd-gateway-url");
        if (!string.IsNullOrEmpty(saved))
            _gatewayUrl = saved;
    }

    private async Task ConnectAsync()
    {
        _connecting = true;
        StateHasChanged();
        try
        {
            await JS.InvokeVoidAsync("localStorage.setItem", "jd-gateway-url", _gatewayUrl);
            await SignalR.ConnectAsync(_gatewayUrl, string.IsNullOrWhiteSpace(_token) ? null : _token);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Connection failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _connecting = false;
            StateHasChanged();
        }
    }

    private void OnStateChanged(object? sender, EventArgs e) => InvokeAsync(StateHasChanged);

    public void Dispose() => SignalR.OnStateChanged -= OnStateChanged;
}
```

### 5.4 — Add auth gate CSS

- [ ] Append to `src/JD.AI.Dashboard.Wasm/wwwroot/css/app.css`:

```css
/* ── Gateway Auth Gate ── */
.jd-auth-gate-overlay {
    position: fixed;
    inset: 0;
    z-index: 9999;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(15, 15, 26, 0.92);
    backdrop-filter: blur(8px);
}

.jd-auth-gate-card {
    width: 100%;
    max-width: 440px;
    border: 1px solid rgba(129, 140, 248, 0.2) !important;
}
```

### 5.5 — Mount GatewayAuthGate in MainLayout

- [ ] In `src/JD.AI.Dashboard.Wasm/Layout/MainLayout.razor`, add inside `<MudMainContent>`, before `@Body`:
  ```razor
  <GatewayAuthGate />
  ```
- [ ] Add `@using JD.AI.Dashboard.Wasm.Components` to `_Imports.razor` if not already present.

### 5.6 — Run tests to confirm GREEN

- [ ] Build:
  ```bash
  cd /c/git/JD.AI && dotnet build src/JD.AI.Dashboard.Wasm/ 2>&1 | tail -10
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] Unit tests:
  ```bash
  cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "GatewayAuthGateTests" 2>&1 | tail -10
  ```
  Expected: `5 passed, 0 failed`

### 5.7 — BDD feature for auth gate

- [ ] Create `tests/JD.AI.Specs.UI/Features/Dashboard/GatewayAuthGate.feature`:

```gherkin
@ui
Feature: Gateway Auth Gate
    As a gateway operator
    I want to see a connection form when the gateway is unreachable
    So that I can enter my credentials and connect

    @smoke
    Scenario: Auth gate shown when gateway is disconnected
        Given the gateway is disconnected
        When I navigate to the home page
        Then I should see the auth gate overlay
        And I should see a URL input prefilled with "ws://127.0.0.1:18789"
        And I should see a Connect button

    Scenario: Auth gate hidden when gateway is connected
        Given the gateway is connected
        When I navigate to the home page
        Then I should not see the auth gate overlay

    Scenario: URL pre-filled from localStorage
        Given localStorage has "jd-gateway-url" set to "ws://192.168.1.10:18789"
        When I navigate to the home page
        Then the URL input should show "ws://192.168.1.10:18789"

    Scenario: Successful connect hides the auth gate
        Given the gateway is disconnected
        When I navigate to the home page
        And I enter "ws://127.0.0.1:18789" in the URL input
        And I click the Connect button
        And the gateway connects successfully
        Then I should not see the auth gate overlay

    Scenario: Failed connect shows error message
        Given the gateway is disconnected
        When I navigate to the home page
        And I click the Connect button with an invalid URL
        Then I should see a toast error notification
```

### 5.8 — Commit auth gate

- [ ] Commit:
  ```bash
  cd /c/git/JD.AI && rtk git add src/JD.AI.Dashboard.Wasm/Components/GatewayAuthGate.razor src/JD.AI.Dashboard.Wasm/Services/SignalRService.cs src/JD.AI.Dashboard.Wasm/Layout/MainLayout.razor src/JD.AI.Dashboard.Wasm/wwwroot/css/app.css src/JD.AI.Dashboard.Wasm/Program.cs tests/JD.AI.Tests/Dashboard/GatewayAuthGateTests.cs tests/JD.AI.Specs.UI/Features/Dashboard/GatewayAuthGate.feature && rtk git commit -m "feat(#470): gateway auth gate component with localStorage URL persistence"
  ```

---

## Task 6 — Full test suite run and self-review

### 6.1 — Run all Dashboard unit tests

- [ ] Run:
  ```bash
  cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ --filter "FullyQualifiedName~Dashboard" 2>&1 | tail -20
  ```
  Expected: all pass, 0 failed.

### 6.2 — Run full unit test suite (regression check)

- [ ] Run:
  ```bash
  cd /c/git/JD.AI && dotnet test tests/JD.AI.Tests/ 2>&1 | tail -10
  ```
  Expected: no regressions from previous passing count.

### 6.3 — Build check

- [ ] Run:
  ```bash
  cd /c/git/JD.AI && dotnet build src/JD.AI.Dashboard.Wasm/ 2>&1 | tail -10
  ```
  Expected: `Build succeeded. 0 Error(s)`

### 6.4 — Self-review checklist

- [ ] Every interactive element added has a `data-testid` attribute
- [ ] No `TBD`, `TODO`, or placeholder strings in implementation files
- [ ] `NavMenu.razor` contains no legacy flat-list links (Channels, Sessions, Providers, Routing, Plugins, Monitor, Analytics, Knowledge, API Keys)
- [ ] `ThemeService` handles all three modes: System, Light, Dark
- [ ] `GatewayAuthGate` renders when `IsConnected == false` and hides when `IsConnected == true`
- [ ] `ISnackbar` injected in Chat page and fires on agent load error
- [ ] `Program.cs` registers: `NavState`, `ThemeService`, `ISignalRService`
- [ ] MudBlazor snackbar configured with position BottomRight, 5s auto-dismiss

---

## Dependency map

```
Task 1 (NavState)
    └── Task 2 (NavMenu restructure)   → closes #469

Task 3 (ThemeService)
    └── Task 3.3 (MainLayout wiring)

Task 4 (Toast wiring)                  → closes #470 (partial)

Task 5 (GatewayAuthGate)
    ├── Task 5.2 (ISignalRService interface, required by tests)
    └── Task 5.5 (mount in MainLayout)  → closes #470 (full)

Task 6 (full pass)
    └── depends on Tasks 1–5 all green
```
