# Coverage Acceleration Plan (2026-03-05)

## Goals

- Raise practical confidence by covering user-facing startup flows and non-subscription UX.
- Enforce deterministic e2e checks in CI for surfaces that do not require paid credentials.
- Keep provider-auth/model-execution paths covered with unit/integration tests using fakes and mocks.

## Baseline Review

- Existing CI runs unit/spec tests but does not enforce a deterministic UI e2e lane.
- `JD.AI.Specs.UI` was not CI-executed by default and had hook/step issues.
- Startup orchestration classes under `src/JD.AI/Startup` had low direct coverage.
- `JD.AI.Tests` line coverage baseline: `51.9%` (3/5/2026 evening run).

## TODO (Iterative)

- [x] Fix UI spec hook ordering and ambiguous step bindings.
- [x] Add deterministic `@smoke` Reqnroll+Playwright dashboard scenarios.
- [x] Add CI workflow lane to run UI smoke specs (`Category=smoke`) against a local dashboard host.
- [x] Add deterministic VHS smoke tape for non-subscription CLI commands.
- [x] Add CI workflow lane to render VHS artifacts.
- [x] Add UI smoke e2e coverage collection into main CI coverage aggregation.
- [x] Fix CI coverage include/report filters to include all `JD.AI*` assemblies (not only `JD.AI`).
- [x] Add startup-focused BDD/unit tests for:
  - [x] `SystemPromptBuilder`
  - [x] `ToolRegistrar`
  - [x] `PrintModeRunner`
- [x] Add deterministic CLI behavior tests for:
  - [x] `PluginCliHandler`
  - [x] `TerminalPluginContext`
- [x] Add rendering/governance startup behavior tests for:
  - [x] `HistoryViewer` (non-interactive paths)
  - [x] `GovernanceInitializer`
  - [x] `TurnMonitorBox`
- [ ] Expand UI coverage from smoke to data-backed scenarios via a deterministic test API host.
- [ ] Refactor startup provider selection to support testable dependency injection:
  - [x] `ProviderOrchestrator`
  - [x] `OnboardingCliHandler`
- [ ] Optimize provider refresh path to avoid duplicate full-detector probes during startup full refresh.
- [ ] Add a coverage threshold gate once flaky paths are stabilized.

## Validation Strategy

- UI e2e uses local dashboard hosting and tag-filtered stable scenarios.
- VHS e2e uses CLI subcommands (`mcp`, `plugin`) that do not require provider auth.
- Startup tests use fake chat services and mocked provider registries to avoid external dependencies.

## Iteration Notes

- Iteration 2 (current):
  - `JD.AI.Tests` line coverage improved from `51.9%` to `52.8%`.
  - `JD.AI` assembly class coverage increased in key user-facing classes:
    - `PluginCliHandler`: `0%` -> `72.8%`
    - `TerminalPluginContext`: `0%` -> `100%`
    - `HistoryViewer`: `0%` -> `50%`
    - `GovernanceInitializer`: `0%` -> `80%`
    - `TurnMonitorBox`: `0%` -> `100%`
- Iteration 3 (current):
  - `JD.AI.Tests` line coverage improved from `52.8%` to `53.3%`.
  - Startup-selection coverage increased with test seams + BDD behavior tests:
    - `OnboardingCliHandler`: `0%` -> `73.3%`
    - `ProviderOrchestrator`: `0%` -> `57.2%`
- Iteration 4 (current):
  - CI coverage pipeline now aggregates deterministic Playwright smoke e2e coverage.
  - Coverage include/report patterns corrected from `JD.AI` to `JD.AI*` so summary is no longer artificially undercounted to a single assembly.
  - Local CI-equivalent dry run (`dotnet test JD.AI.slnx --filter "Category!=Integration"` with corrected filters) produced `63.8%` line coverage across 14 assemblies in aggregated report.
