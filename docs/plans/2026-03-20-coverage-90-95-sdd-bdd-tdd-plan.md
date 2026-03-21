# Coverage 90-95 Plan (SDD/BDD/TDD)

Date: 2026-03-20
Owner: JD.AI maintainers
Status: Active

## Baseline (measured)

Measured with:

```bash
dotnet test JD.AI.slnx --configuration Release --filter "Category!=Integration" --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include="[JD.AI*]*" DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude="[*Tests]*"
reportgenerator -reports:"tests/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"TextSummary;Cobertura" -assemblyfilters:"+JD.AI*;-*Tests*"
```

Current aggregate:

- Line coverage: `71.7%`
- Branch coverage: `59.9%`

Lowest assemblies by line coverage:

1. `JD.AI.Dashboard.Wasm` (`10.6%`)
2. `JD.AI.Channels.Discord` (`12.1%`)
3. `JD.AI.Daemon` (`19.7%`)
4. `JD.AI.Workflows.Distributed` (`44.1%`)
5. `JD.AI.Channels.Telegram` (`53.2%`)
6. `JD.AI.Channels.OpenClaw` (`54.9%`)
7. `JD.AI` (`58.4%`)

## Target

1. Move aggregate line coverage from `71.7%` to `90%` minimum.
2. Reach `95%` for Tier-1 runtime logic (Core, Gateway, Providers, Channel routing, Session orchestration).
3. Raise branch coverage to `>=80%` for Tier-1 runtime logic.

## Scope and policy

- Test behavior, contracts, and invariants; do not test private implementation details.
- Prefer black-box tests at boundaries, then add focused unit tests for branches/edge-cases.
- Keep all integration tests secret-gated and skip when env/secrets are missing.
- Generated code and startup shell/bootstrap glue may be excluded only when no business logic exists.

## Workstreams (priority order)

## Wave 1 (fastest coverage gain, low refactor risk)

1. `JD.AI.Daemon`:
- Extract `Program` command logic into injectable services (`BridgeCommandService`, `ServiceLifecycleService`).
- Add unit tests for all bridge actions and error paths.

2. `JD.AI` CLI startup/rendering:
- Add tests for `SlashCommandRouter` edge branches, `ChatRenderer`, `HistoryViewer`, `QuestionnaireSession`, `SessionTurnOrchestrator`.
- Add scenario tests for startup orchestration decisions (provider/auth/session mode).

3. Channels:
- Add high-value tests for `DiscordChannel` and `TelegramChannel` command parsing, retry/timeout, auth/token failure paths.

Expected aggregate after Wave 1: `78-82%`.

## Wave 2 (mid complexity, high ROI)

1. `JD.AI.Workflows.Distributed`:
- Transport error handling, ack/nack behavior, idempotency tests.

2. `JD.AI.Channels.OpenClaw`:
- `OpenClawRoutingService` event routing branches and fallback paths.
- `OpenClawAgentRegistrar` lifecycle error paths and reconnect behavior.

3. Providers in `JD.AI.Core`:
- Low-coverage detectors (`CopilotDetector`, `OpenAICodexDetector`, `OpenAIDetector`, `AzureOpenAIDetector`, `AnthropicDetector`).
- Cover cache hit/miss/invalid JSON/network exception branches.

Expected aggregate after Wave 2: `84-88%`.

## Wave 3 (structural and UI coverage)

1. `JD.AI.Dashboard.Wasm`:
- Add bUnit tests for settings pages/tabs and API client call paths.
- Add Playwright smoke paths only for workflow confidence, not line coverage.

2. Installation and local-model modules:
- `GitHubReleaseStrategy`, `PackageManagerStrategy`, downloader/source adapters.

3. Coverage ratchet:
- Introduce per-assembly minimums and ratchet up each week.

Expected aggregate after Wave 3: `90-95%`.

## SDD flow (Spec-Driven Development)

For each feature slice:

1. Write a short spec in `docs/specs/<area>/`:
- Problem statement
- Inputs/outputs
- Invariants
- Error semantics

2. Define executable acceptance table:
- Preconditions
- Action
- Observable result

3. Implement minimum design that satisfies spec.

4. Link tests to spec IDs in test names/comments.

SDD done criteria:

- Spec exists and is reviewed.
- Acceptance examples are implemented in tests.
- All error branches listed in spec are covered.

## BDD flow (behavior-first)

Use `tests/JD.AI.Specs` for black-box behavior and policy/routing flows.

Template:

```gherkin
Feature: <Capability>
  Scenario: <Primary behavior>
    Given <system state>
    And <external dependency state>
    When <user/system action>
    Then <observable output>
    And <side effects>

  Scenario: <Failure mode>
    Given <fault condition>
    When <same action>
    Then <safe failure behavior>
```

BDD done criteria:

- Happy path + at least 2 failure scenarios.
- One scenario verifies side effects (events/session state/audit).
- Step definitions assert externally observable outcomes only.

## TDD flow (unit-level)

For each branch-heavy class:

1. Red: add failing unit test for one behavior branch.
2. Green: implement smallest change.
3. Refactor: remove duplication, keep public behavior unchanged.
4. Repeat for:
- null/empty inputs
- timeout/cancellation
- transient fault/retry
- invalid payload/auth failure

TDD done criteria:

- Decision branches covered.
- Boundary values covered.
- Failure messages/contracts asserted.

## CI and governance updates

1. Keep existing PR gate (source changes require test changes).
2. Add coverage ratchet file (`coverage-ratchet.json`) with per-assembly minimums.
3. Fail PR if any covered assembly drops below its ratchet baseline.
4. Raise ratchet weekly until target is reached.

## First sprint backlog (immediate)

1. Daemon bridge/service command extraction + tests.
2. Discord/Telegram channel behavior tests.
3. OpenClaw routing and registrar branch tests.
4. Slash router + startup orchestration branch tests.
5. Add dashboard bUnit baseline for settings pages.

## Checkpoint (2026-03-20)

- Coverage swarm progress recorded before OpenClaw side-mission:
- `JD.AI.Tests` line coverage: `66.2%`
- `JD.AI.Tests` branch coverage: `57.1%`
- `JD.AI.Commands.SlashCommandRouter` line coverage: `53.8%`
- Artifacts:
- `coverage-report/jdai-tests-after-wave2/Summary.txt`
- `coverage-report/jdai-tests-after-wave2/uncovered-classes-top200.txt`
- `coverage-report/jdai-tests-after-wave2/uncovered-lines-top500.txt`
