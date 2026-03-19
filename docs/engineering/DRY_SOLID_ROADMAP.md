# DRY/SOLID Roadmap

## Scope
This roadmap focuses on reducing duplication and tightening responsibility boundaries without overfitting tests to implementation details.

## Principles
- Test behavior flows, not UI rendering internals.
- Centralize shared setup/teardown and provider bootstrap logic.
- Keep orchestration logic host-agnostic (TUI, print, dashboard, desktop, gateway).
- Prefer small composable services over large multi-purpose classes.

## Completed in Current Iteration
- Extracted shared turn execution to `SessionTurnOrchestrator`.
- Wired TUI + print/headless paths through the same orchestrator.
- Added headless integration harness for provider/agent integration tests.
- Consolidated provider test utilities:
  - Temporary credential store/config setup.
  - Preferred model selection helper.
- Standardized integration test gating semantics with `IntegrationTestGuard` (legacy alias preserved).

## Review Findings
1. Integration guard naming still references legacy TUI in call sites (`TuiIntegrationGuard` alias).
2. Workflow integration tests currently build kernels directly rather than reusing test harness patterns.
3. `InteractiveLoop` still contains mixed responsibilities:
   - UI rendering/input
   - command routing
   - shell/file mention preprocessing
4. Provider integration tests still perform repeated detector bootstrapping patterns that can be table-driven.

## Next Work Plan
### Phase 1: Integration Test DRY Cleanup
1. Replace `TuiIntegrationGuard` references with `IntegrationTestGuard` in integration tests.
2. Convert provider feature tests to data-driven theory-style matrix to reduce per-provider method duplication.
3. Migrate workflow integration setup to shared test bootstrap helpers where practical.

### Phase 2: Orchestration SOLID Refactor
1. Extract input preprocessing from `InteractiveLoop` into:
   - slash command dispatcher adapter
   - shell command adapter
   - file mention expansion adapter
2. Keep `InteractiveLoop` as a thin host adapter coordinating input/output only.
3. Introduce an orchestration interface used by gateway/dashboard entrypoints.

### Phase 3: Cross-Host Runtime Consistency
1. Add gateway/desktop-compatible turn endpoint/service that directly uses `SessionTurnOrchestrator`.
2. Add integration tests that run identical prompts through:
   - print/headless path
   - gateway API path
   and assert equivalent behavior guarantees (tools/auth/session effects).

## Guardrails
- Keep env-gated skip behavior for external providers/secrets.
- Do not introduce abstractions without at least two real call sites.
- Prefer additive refactors that preserve current test coverage.
