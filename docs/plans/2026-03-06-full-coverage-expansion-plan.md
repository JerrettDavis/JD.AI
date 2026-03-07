# Full Coverage Expansion Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Achieve comprehensive test coverage for all untested areas: 5 channel adapters, DiffRenderer, HistoryViewer, 2 CLI handlers, GenAiAttributes, TelemetryConfig, and 6 dashboard settings tab components.

**Architecture:** Unit tests with NSubstitute mocking for channel SDKs and OS interactions. Reqnroll BDD specs for channel behavior documentation. Playwright BDD for settings tab UI. Stdout capture via `StringWriter` + `Console.SetOut` for TUI renderer tests. All tests follow existing xUnit + FluentAssertions + `sealed class` conventions.

**Tech Stack:** xUnit, NSubstitute, FluentAssertions, Reqnroll, Playwright, Console.SetOut for output capture

**Existing coverage verified as adequate (DO NOT re-test):**
- Rendering: ChatRenderer, SpectreAgentOutput, TeamProgressPanel, TurnProgress, TurnSpinner, QuestionnaireSession, MarkdownRenderer, FileMentionExpander (9 test files, ~1900 lines)
- MultiTenancy: HeaderTenantResolver, TenantDataFilter, TenantQuota (3 test files, 21 tests)
- PromptCaching: BDD spec with 8 scenarios covering all logic paths
- Telemetry: ActivitySources, Meters, all 4 health checks (6 test files, 25 tests)
- Daemon: DaemonServiceTests (18 tests) + UpdatePrompterBddTests
- OpenClaw channel: 22 tests across 3 files

---

## Task 1: Discord + Slack Channel Unit Tests

**Files:**
- Create: `tests/JD.AI.Tests/Channels/DiscordChannelTests.cs`
- Create: `tests/JD.AI.Tests/Channels/SlackChannelTests.cs`
- Reference: `src/JD.AI.Channels.Discord/DiscordChannel.cs`
- Reference: `src/JD.AI.Channels.Slack/SlackChannel.cs`

**What to test (DiscordChannel):**
- `ChannelType` returns `"discord"`
- `DisplayName` returns `"Discord"`
- `IsConnected` defaults to `false`
- `IAsyncDisposable` implementation doesn't throw when not connected
- Constructor stores bot token (verified indirectly via connect behavior)

**What to test (SlackChannel):**
- `ChannelType` returns `"slack"`
- `DisplayName` returns `"Slack"`
- `IsConnected` defaults to `false`
- `IAsyncDisposable` implementation doesn't throw when not connected
- `RegisterCommandsAsync` stores registry without error

**Pattern to follow (from OpenClawBridgeChannelTests.cs):**
```csharp
public sealed class DiscordChannelTests
{
    [Fact]
    public void ChannelType_IsDiscord()
    {
        var channel = new DiscordChannel("fake-bot-token");
        channel.ChannelType.Should().Be("discord");
    }
    // ... etc
}
```

**Testing notes:**
- Discord and Slack channels create SDK clients in `ConnectAsync`, NOT in constructor — so constructing them is safe without real tokens
- DO NOT test `ConnectAsync` (requires real SDK client connection)
- DO NOT test `SendMessageAsync` (requires connected SDK client)
- Focus on property values, initialization state, and disposal safety

**Build & verify:**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~DiscordChannelTests|FullyQualifiedName~SlackChannelTests"
```

**Commit:**
```bash
git add tests/JD.AI.Tests/Channels/DiscordChannelTests.cs tests/JD.AI.Tests/Channels/SlackChannelTests.cs
git commit -m "test: add Discord and Slack channel unit tests"
```

---

## Task 2: Telegram + Signal + Web Channel Unit Tests

**Files:**
- Create: `tests/JD.AI.Tests/Channels/TelegramChannelTests.cs`
- Create: `tests/JD.AI.Tests/Channels/SignalChannelTests.cs`
- Create: `tests/JD.AI.Tests/Channels/WebChannelTests.cs`
- Reference: `src/JD.AI.Channels.Telegram/TelegramChannel.cs`
- Reference: `src/JD.AI.Channels.Signal/SignalChannel.cs`
- Reference: `src/JD.AI.Channels.Web/WebChannel.cs`

**What to test (TelegramChannel):**
- `ChannelType` returns `"telegram"`
- `DisplayName` returns `"Telegram"`
- `IsConnected` defaults to `false`
- Disposal safety

**What to test (SignalChannel):**
- `ChannelType` returns `"signal"`
- `DisplayName` returns `"Signal"`
- `IsConnected` defaults to `false`
- `RegisterCommandsAsync` stores registry without error
- Disposal safety

**What to test (WebChannel — richest testable surface):**
- `ChannelType` returns `"web"`
- `DisplayName` returns `"Web Chat"`
- `IsConnected` defaults to `false`
- `ConnectAsync` sets `IsConnected` to `true`
- `DisconnectAsync` sets `IsConnected` to `false`
- `IngestMessageAsync` raises `MessageReceived` event with correct `ChannelMessage` fields
- `IngestMessageAsync` generates unique message IDs
- `SendMessageAsync` stores message in conversation dictionary (audit trail)
- Thread safety: concurrent `IngestMessageAsync` calls don't lose messages

**Testing notes:**
- WebChannel is the ONLY channel that can be fully tested without external dependencies
- For Telegram/Signal: only test constructor and properties (ConnectAsync starts real processes/connections)

**Build & verify:**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~TelegramChannelTests|FullyQualifiedName~SignalChannelTests|FullyQualifiedName~WebChannelTests"
```

**Commit:**
```bash
git add tests/JD.AI.Tests/Channels/TelegramChannelTests.cs tests/JD.AI.Tests/Channels/SignalChannelTests.cs tests/JD.AI.Tests/Channels/WebChannelTests.cs
git commit -m "test: add Telegram, Signal, and Web channel unit tests"
```

---

## Task 3: Channel Adapter BDD Specs

**Files:**
- Create: `tests/JD.AI.Specs/Features/Core/Channels/ChannelAdapters.feature`
- Create: `tests/JD.AI.Specs/StepDefinitions/Core/ChannelAdapterSteps.cs`
- Reference: `tests/JD.AI.Specs/Features/Core/Channels/ChannelRegistry.feature` (existing pattern)
- Reference: `tests/JD.AI.Specs/StepDefinitions/Core/ChannelRegistrySteps.cs` (existing pattern)

**Feature file structure:**
```gherkin
@channels
Feature: Channel Adapters
  As the platform
  I need messaging channel adapters for multiple platforms
  So that I can receive and send messages across Discord, Slack, Telegram, Signal, and Web

  # ── Identity ─────────────────────────────────────────
  Scenario Outline: Each channel adapter reports correct type
    Given a <channel> channel adapter
    Then the channel type should be "<type>"
    And the display name should be "<name>"

    Examples:
      | channel   | type     | name      |
      | discord   | discord  | Discord   |
      | slack     | slack    | Slack     |
      | telegram  | telegram | Telegram  |
      | signal    | signal   | Signal    |
      | web       | web      | Web Chat  |

  # ── Initial state ───────────────────────────────────
  Scenario Outline: Channel adapters start disconnected
    Given a <channel> channel adapter
    Then the channel should not be connected

    Examples:
      | channel   |
      | discord   |
      | slack     |
      | telegram  |
      | signal    |
      | web       |

  # ── Web channel (fully testable) ─────────────────────
  Scenario: Web channel connects and disconnects
    Given a web channel adapter
    When I connect the channel
    Then the channel should be connected
    When I disconnect the channel
    Then the channel should not be connected

  Scenario: Web channel ingests messages
    Given a web channel adapter
    And the channel is connected
    When a message "Hello" arrives from user "user-1" on connection "conn-1"
    Then the message received event should fire
    And the message content should be "Hello"
    And the message sender ID should be "user-1"

  Scenario: Web channel stores sent messages
    Given a web channel adapter
    And the channel is connected
    When I send "Reply" to conversation "conv-1"
    Then the conversation "conv-1" should have 1 stored message

  # ── ICommandAwareChannel ────────────────────────────
  Scenario Outline: Command-aware channels accept command registration
    Given a <channel> channel adapter that supports commands
    When I register a command registry
    Then no error should occur

    Examples:
      | channel |
      | discord |
      | slack   |
      | signal  |

  # ── Disposal safety ────────────────────────────────
  Scenario Outline: Channel disposal is safe when not connected
    Given a <channel> channel adapter
    When I dispose the channel
    Then no error should occur

    Examples:
      | channel   |
      | discord   |
      | slack     |
      | telegram  |
      | signal    |
      | web       |
```

**Step definitions pattern:**
- Create each channel by type string using a factory method
- Store in ScenarioContext
- For web channel: test full connect/disconnect/ingest/send lifecycle
- For others: test properties and disposal only
- For command-aware: test RegisterCommandsAsync with a substitute ICommandRegistry

**Build & verify:**
```bash
dotnet test tests/JD.AI.Specs/JD.AI.Specs.csproj --filter "FullyQualifiedName~ChannelAdapter"
```

**Commit:**
```bash
git add tests/JD.AI.Specs/Features/Core/Channels/ChannelAdapters.feature tests/JD.AI.Specs/StepDefinitions/Core/ChannelAdapterSteps.cs
git commit -m "test: add BDD specs for all 5 channel adapters"
```

---

## Task 4: DiffRenderer Unit Tests

**Files:**
- Create: `tests/JD.AI.Tests/Rendering/DiffRendererTests.cs`
- Reference: `src/JD.AI/Rendering/DiffRenderer.cs` (~77 lines)

**What to test:**
- `IsDiff` returns `true` for valid unified diff (with `---` and `+++` markers)
- `IsDiff` returns `true` for diff with preamble lines (`diff --git`, `index ...`)
- `IsDiff` returns `false` for plain text
- `IsDiff` returns `false` for empty string
- `IsDiff` returns `false` for null
- `Render` colorizes `+` lines (green markup)
- `Render` colorizes `-` lines (red markup)
- `Render` colorizes `@@` lines (cyan markup)
- `Render` applies dim to context lines
- `Render` handles CRLF line endings (TrimEnd `\r`)
- `Render` escapes Spectre markup characters in content

**Pattern:**
```csharp
public sealed class DiffRendererTests
{
    [Fact]
    public void IsDiff_WithUnifiedDiffHeaders_ReturnsTrue()
    {
        var diff = "--- a/file.cs\n+++ b/file.cs\n@@ -1,3 +1,3 @@\n context\n-old\n+new";
        DiffRenderer.IsDiff(diff).Should().BeTrue();
    }

    [Fact]
    public void IsDiff_WithPlainText_ReturnsFalse()
    {
        DiffRenderer.IsDiff("just some text").Should().BeFalse();
    }

    [Fact]
    public void Render_ColorizesAddedLines()
    {
        var diff = "--- a/f.cs\n+++ b/f.cs\n@@ -1 +1 @@\n+added line";
        var result = DiffRenderer.Render(diff);
        result.Should().Contain("[bold green]");
    }
    // etc.
}
```

**Build & verify:**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~DiffRendererTests"
```

**Commit:**
```bash
git add tests/JD.AI.Tests/Rendering/DiffRendererTests.cs
git commit -m "test: add DiffRenderer unit tests"
```

---

## Task 5: HistoryViewer Unit Tests

**Files:**
- Create: `tests/JD.AI.Tests/Rendering/HistoryViewerTests.cs`
- Reference: `src/JD.AI/Rendering/HistoryViewer.cs` (~164 lines)

**What to test:**
- `Show` returns `null` when `Console.IsInputRedirected` is `true` (testable via environment)
- `Render` produces output with turn entries
- `Render` highlights selected turn with `▸` prefix
- `RenderDetail` includes turn ID, role, model, token counts
- `RenderDetail` truncates long content (>300 chars)
- `RenderDetail` truncates thinking preview (>200 chars)
- `RenderDetail` shows tool/file touch summaries

**Testing approach:**
- `Render` and `RenderDetail` are static methods that return renderable Spectre objects
- Capture output via `AnsiConsole.Record()` or test the returned IRenderable structure
- For `Show` (interactive): only test the input-redirected early return; skip keyboard interaction tests

**Build & verify:**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~HistoryViewerTests"
```

**Commit:**
```bash
git add tests/JD.AI.Tests/Rendering/HistoryViewerTests.cs
git commit -m "test: add HistoryViewer unit tests"
```

---

## Task 6: PluginCliHandler + UpdateCliHandler Unit Tests

**Files:**
- Create: `tests/JD.AI.Tests/Commands/PluginCliHandlerTests.cs`
- Create: `tests/JD.AI.Tests/Commands/UpdateCliHandlerTests.cs`
- Reference: `src/JD.AI/Commands/PluginCliHandler.cs` (~213 lines)
- Reference: `src/JD.AI/Commands/UpdateCliHandler.cs` (~216 lines)

**What to test (PluginCliHandler):**
- `RunAsync(["help"])` returns 0 and prints help text
- `RunAsync([])` returns 0 and prints help text (no args = help)
- `RunAsync(["unknown-cmd"])` returns 1 and prints error
- `RunAsync(["list"])` executes without error (may return empty list)
- Argument parsing for install/enable/disable/uninstall subcommands

**What to test (UpdateCliHandler):**
- `RunAsync("update", ["--help"])` returns 0 and prints help
- `RunAsync("install", ["--help"])` returns 0 and prints help
- `RunAsync("update", ["--check"])` performs check-only (no apply)

**Testing approach:**
- Redirect `Console.Out` to capture output
- These handlers are static methods that bootstrap their own DI — test the argument parsing and help paths
- The actual plugin/update operations require real file system state, so focus on argument dispatch and error handling

**Build & verify:**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~PluginCliHandlerTests|FullyQualifiedName~UpdateCliHandlerTests"
```

**Commit:**
```bash
git add tests/JD.AI.Tests/Commands/PluginCliHandlerTests.cs tests/JD.AI.Tests/Commands/UpdateCliHandlerTests.cs
git commit -m "test: add PluginCliHandler and UpdateCliHandler unit tests"
```

---

## Task 7: GenAiAttributes + TelemetryConfig Unit Tests

**Files:**
- Create: `tests/JD.AI.Tests/Telemetry/GenAiAttributesTests.cs`
- Create: `tests/JD.AI.Tests/Telemetry/TelemetryConfigTests.cs`
- Reference: `src/JD.AI.Telemetry/GenAiAttributes.cs` (~81 lines)
- Reference: `src/JD.AI.Telemetry/TelemetryConfig.cs` (~54 lines)

**What to test (GenAiAttributes):**
- `SetGenAiRequestAttributes` sets all expected tags on Activity
- `SetGenAiRequestAttributes` handles null Activity gracefully (no throw)
- `SetGenAiResponseAttributes` sets response model, tokens, finish reason
- `SetGenAiResponseAttributes` skips null optional parameters
- Finish reason is wrapped in array (`new[] { finishReason }`)

**What to test (TelemetryConfig):**
- Default values: Enabled=true, ServiceName="jdai", Exporter="console", OtlpProtocol="grpc"
- Endpoint defaults to null when OTEL_EXPORTER_OTLP_ENDPOINT not set
- Config binds correctly from dictionary

**Pattern:**
```csharp
public sealed class GenAiAttributesTests
{
    [Fact]
    public void SetRequestAttributes_NullActivity_DoesNotThrow()
    {
        var act = () => GenAiAttributes.SetGenAiRequestAttributes(
            null, "anthropic", "claude-3", "chat", maxTokens: 1000);
        act.Should().NotThrow();
    }

    [Fact]
    public void SetRequestAttributes_SetsSystemTag()
    {
        using var source = new ActivitySource("test");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("test-op");
        GenAiAttributes.SetGenAiRequestAttributes(
            activity, "anthropic", "claude-3-opus", "chat");

        activity!.GetTagItem("gen_ai.system").Should().Be("anthropic");
        activity.GetTagItem("gen_ai.request.model").Should().Be("claude-3-opus");
    }
}
```

**Build & verify:**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~GenAiAttributesTests|FullyQualifiedName~TelemetryConfigTests"
```

**Commit:**
```bash
git add tests/JD.AI.Tests/Telemetry/GenAiAttributesTests.cs tests/JD.AI.Tests/Telemetry/TelemetryConfigTests.cs
git commit -m "test: add GenAiAttributes and TelemetryConfig unit tests"
```

---

## Task 8: Settings Agents + Server Tab UI Specs

**Files:**
- Modify: `tests/JD.AI.Specs.UI/Features/Dashboard/SettingsPage.feature` (extend)
- Modify: `tests/JD.AI.Specs.UI/StepDefinitions/SettingsPageSteps.cs` (extend)
- Modify: `tests/JD.AI.Specs.UI/PageObjects/SettingsPage.cs` (extend)
- Modify: `src/JD.AI.Dashboard.Wasm/Components/SettingsAgentsTab.razor` (add data-testid)
- Modify: `src/JD.AI.Dashboard.Wasm/Components/SettingsServerTab.razor` (add data-testid)

**New scenarios to add to SettingsPage.feature:**
```gherkin
  # ── Server Tab Content ──────────────────────────────
  @smoke
  Scenario: Server tab shows network configuration
    Given I am on the settings page
    When I click the "Server" tab
    Then the server settings panel should be visible
    And I should see a "Host" input field
    And I should see a "Port" input field

  Scenario: Server tab shows authentication toggle
    Given I am on the settings page
    When I click the "Server" tab
    Then I should see an "Authentication" toggle
    And I should see a "Rate Limiting" toggle

  Scenario: Server tab has save button
    Given I am on the settings page
    When I click the "Server" tab
    Then a save server button should be available

  # ── Agents Tab Content ──────────────────────────────
  @smoke
  Scenario: Agents tab shows agent definitions list
    Given I am on the settings page
    When I click the "Agents" tab
    Then the agents settings panel should be visible
    And I should see the add agent button

  Scenario: Agents tab shows agent configuration fields
    Given I am on the settings page
    When I click the "Agents" tab
    And there are configured agents
    Then each agent should have an "Agent ID" field
    And each agent should have a "Provider" select
    And each agent should have a "Model" select

  Scenario: Agents tab shows model parameters expansion
    Given I am on the settings page
    When I click the "Agents" tab
    And there are configured agents
    Then each agent should have a "Model Parameters" expansion panel
```

**data-testid attributes to add:**
- `SettingsAgentsTab.razor`: `data-testid="add-agent-button"`, `data-testid="agent-entry"`, `data-testid="agent-id-field"`, `data-testid="agent-provider-select"`, `data-testid="agent-model-select"`, `data-testid="agent-params-panel"`
- `SettingsServerTab.razor`: `data-testid="host-input"`, `data-testid="port-input"`, `data-testid="auth-toggle"`, `data-testid="ratelimit-toggle"`, `data-testid="save-server-button"`

**Build & verify:**
```bash
dotnet build tests/JD.AI.Specs.UI/JD.AI.Specs.UI.csproj
```

**Commit:**
```bash
git add tests/JD.AI.Specs.UI/ src/JD.AI.Dashboard.Wasm/Components/SettingsAgentsTab.razor src/JD.AI.Dashboard.Wasm/Components/SettingsServerTab.razor
git commit -m "test: add settings Agents and Server tab UI specs"
```

---

## Task 9: Settings Channels + Providers Tab UI Specs

**Files:**
- Modify: `tests/JD.AI.Specs.UI/Features/Dashboard/SettingsPage.feature` (extend)
- Modify: `tests/JD.AI.Specs.UI/StepDefinitions/SettingsPageSteps.cs` (extend)
- Modify: `tests/JD.AI.Specs.UI/PageObjects/SettingsPage.cs` (extend)
- Modify: `src/JD.AI.Dashboard.Wasm/Components/SettingsChannelsTab.razor` (add data-testid)
- Modify: `src/JD.AI.Dashboard.Wasm/Components/SettingsProvidersTab.razor` (add data-testid)

**New scenarios:**
```gherkin
  # ── Channels Tab Content ────────────────────────────
  @smoke
  Scenario: Channels tab shows channel configuration list
    Given I am on the settings page
    When I click the "Channels" tab
    Then the channels settings panel should be visible
    And I should see channel entries with type labels

  Scenario: Channels tab has enabled toggles per channel
    Given I am on the settings page
    When I click the "Channels" tab
    Then each channel entry should have an "Enabled" toggle

  Scenario: Channels tab masks secret settings
    Given I am on the settings page
    When I click the "Channels" tab
    And a channel has a setting containing "token" in its key
    Then that setting field should be a password input

  # ── Providers Tab Content ───────────────────────────
  @smoke
  Scenario: Providers tab shows provider list
    Given I am on the settings page
    When I click the "Providers" tab
    Then the providers settings panel should be visible
    And I should see provider entries

  Scenario: Providers tab has test buttons
    Given I am on the settings page
    When I click the "Providers" tab
    Then each provider entry should have a "Test" button

  Scenario: Provider test shows model count on success
    Given I am on the settings page
    When I click the "Providers" tab
    And I click the "Test" button on a provider
    Then the provider should show a model count or error
```

**data-testid attributes to add:**
- `SettingsChannelsTab.razor`: `data-testid="channel-entry"`, `data-testid="channel-enabled-toggle"`, `data-testid="channel-setting"`, `data-testid="save-channels-button"`
- `SettingsProvidersTab.razor`: `data-testid="provider-entry"`, `data-testid="provider-enabled-toggle"`, `data-testid="provider-test-button"`, `data-testid="provider-test-result"`, `data-testid="save-providers-button"`

**Build & verify:**
```bash
dotnet build tests/JD.AI.Specs.UI/JD.AI.Specs.UI.csproj
```

**Commit:**
```bash
git add tests/JD.AI.Specs.UI/ src/JD.AI.Dashboard.Wasm/Components/SettingsChannelsTab.razor src/JD.AI.Dashboard.Wasm/Components/SettingsProvidersTab.razor
git commit -m "test: add settings Channels and Providers tab UI specs"
```

---

## Task 10: Settings OpenClaw + Routing Tab UI Specs

**Files:**
- Modify: `tests/JD.AI.Specs.UI/Features/Dashboard/SettingsPage.feature` (extend)
- Modify: `tests/JD.AI.Specs.UI/StepDefinitions/SettingsPageSteps.cs` (extend)
- Modify: `tests/JD.AI.Specs.UI/PageObjects/SettingsPage.cs` (extend)
- Modify: `src/JD.AI.Dashboard.Wasm/Components/SettingsOpenClawTab.razor` (add data-testid)
- Modify: `src/JD.AI.Dashboard.Wasm/Components/SettingsRoutingTab.razor` (add data-testid)

**New scenarios:**
```gherkin
  # ── Routing Tab Content ─────────────────────────────
  @smoke
  Scenario: Routing tab shows default agent selector
    Given I am on the settings page
    When I click the "Routing" tab
    Then the routing settings panel should be visible
    And I should see a "Default Agent" select

  Scenario: Routing tab shows routing rules list
    Given I am on the settings page
    When I click the "Routing" tab
    Then I should see routing rule entries
    And each rule should show a channel type and agent ID

  Scenario: Routing tab has add rule button
    Given I am on the settings page
    When I click the "Routing" tab
    Then I should see an "Add Rule" button

  # ── OpenClaw Tab Content ────────────────────────────
  @smoke
  Scenario: OpenClaw tab shows bridge configuration
    Given I am on the settings page
    When I click the "OpenClaw" tab
    Then the OpenClaw settings panel should be visible
    And I should see an "Enabled" toggle for the bridge

  Scenario: OpenClaw tab shows WebSocket URL field
    Given I am on the settings page
    When I click the "OpenClaw" tab
    Then I should see a "WebSocket URL" input

  Scenario: OpenClaw tab shows registered agents section
    Given I am on the settings page
    When I click the "OpenClaw" tab
    Then I should see a "Registered Agents" section
```

**data-testid attributes to add:**
- `SettingsRoutingTab.razor`: `data-testid="default-agent-select"`, `data-testid="routing-rule"`, `data-testid="add-rule-button"`, `data-testid="save-routing-button"`
- `SettingsOpenClawTab.razor`: `data-testid="openclaw-enabled-toggle"`, `data-testid="openclaw-ws-url"`, `data-testid="openclaw-agents-section"`, `data-testid="save-openclaw-button"`

**Build & verify:**
```bash
dotnet build tests/JD.AI.Specs.UI/JD.AI.Specs.UI.csproj
```

**Commit:**
```bash
git add tests/JD.AI.Specs.UI/ src/JD.AI.Dashboard.Wasm/Components/SettingsOpenClawTab.razor src/JD.AI.Dashboard.Wasm/Components/SettingsRoutingTab.razor
git commit -m "test: add settings OpenClaw and Routing tab UI specs"
```

---

## Task 11: Build Verification + Push + CI Watch

**Step 1: Full solution build**
```bash
dotnet build
```
Expected: 0 errors, 0 warnings

**Step 2: Run all unit tests**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj
```
Expected: All pass

**Step 3: Run all BDD specs**
```bash
dotnet test tests/JD.AI.Specs/JD.AI.Specs.csproj
```
Expected: All pass

**Step 4: Build UI specs (don't run — requires live gateway)**
```bash
dotnet build tests/JD.AI.Specs.UI/JD.AI.Specs.UI.csproj
```
Expected: 0 errors

**Step 5: Run Gateway tests**
```bash
dotnet test tests/JD.AI.Gateway.Tests/JD.AI.Gateway.Tests.csproj
```
Expected: All pass

**Step 6: Push and watch CI**
```bash
git push
gh run watch <run-id>
```
Expected: All green

---

## Summary

| Task | Area | New Tests | Test Type |
|------|------|-----------|-----------|
| 1 | Discord + Slack channels | ~10 | Unit |
| 2 | Telegram + Signal + Web channels | ~15 | Unit |
| 3 | All 5 channel adapters | ~20 scenarios | BDD/Reqnroll |
| 4 | DiffRenderer | ~10 | Unit |
| 5 | HistoryViewer | ~8 | Unit |
| 6 | PluginCliHandler + UpdateCliHandler | ~10 | Unit |
| 7 | GenAiAttributes + TelemetryConfig | ~10 | Unit |
| 8 | Settings Agents + Server tabs | ~6 scenarios | UI/Playwright |
| 9 | Settings Channels + Providers tabs | ~6 scenarios | UI/Playwright |
| 10 | Settings OpenClaw + Routing tabs | ~6 scenarios | UI/Playwright |
| 11 | Build + Push + CI | — | Verification |

**Total new coverage: ~100 tests/scenarios across all untested areas**
