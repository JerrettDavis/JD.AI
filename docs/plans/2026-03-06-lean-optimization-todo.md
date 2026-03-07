# Lean Optimization TODO (DRY / SOLID / SSOT)

## Goals

- Reduce duplicate implementations and configuration drift.
- Replace stringly/hardcoded logic with typed, centralized sources of truth.
- Improve startup performance without losing observability or UX clarity.
- Use analyzers/generators/patterns to keep future code lean by default.

## Optimization Gates (Done Criteria)

- [ ] No multi-source command drift: routing/help/completions generated from one descriptor source.
- [ ] No hardcoded budget price constants in runtime budgeting path.
- [ ] Shared runtime constants (gateway defaults/service identifiers) centralized and reused everywhere.
- [ ] Startup avoids unnecessary probes/fetches when corresponding welcome sections are disabled.
- [ ] New analyzer/generator checks prevent reintroducing duplicated command/config mappings.

## Parallel Tracking

### Workstream Board

| Workstream | Owner | Issue | Branch | Worktree | PR | Status | Last Update |
|---|---|---|---|---|---|---|---|
| Phase 1: Budget/cost correctness | codex | TBD | `swarm/p1-budget-cost-correctness` | `C:\git\tmp1\JD.AI.worktrees\p1-budget-cost-correctness` | https://github.com/JerrettDavis/JD.AI/pull/194 | in_review | 2026-03-06 |
| Phase 1: Welcome probe short-circuiting | unassigned | TBD | `swarm/p1-welcome-probe-shortcircuit` | `C:\git\tmp1\JD.AI.worktrees\p1-welcome-probe-shortcircuit` | TBD | ready | 2026-03-06 |
| Phase 2: Command SSOT generation | unassigned | TBD | `swarm/p2-command-ssot-generation` | `C:\git\tmp1\JD.AI.worktrees\p2-command-ssot-generation` | TBD | ready | 2026-03-06 |
| Phase 2: Config key descriptor generation | unassigned | TBD | `swarm/p2-config-key-descriptor-generation` | `C:\git\tmp1\JD.AI.worktrees\p2-config-key-descriptor-generation` | TBD | ready | 2026-03-06 |
| Phase 3: Runtime constants centralization | unassigned | TBD | `swarm/p3-runtime-constants-centralization` | `C:\git\tmp1\JD.AI.worktrees\p3-runtime-constants-centralization` | TBD | ready | 2026-03-06 |
| Phase 3: Provider orchestration simplification | unassigned | TBD | `swarm/p3-provider-orchestration-simplification` | `C:\git\tmp1\JD.AI.worktrees\p3-provider-orchestration-simplification` | TBD | ready | 2026-03-06 |
| Phase 4: Analyzer/generator guardrails | unassigned | TBD | `swarm/p4-analyzer-generator-guardrails` | `C:\git\tmp1\JD.AI.worktrees\p4-analyzer-generator-guardrails` | TBD | ready | 2026-03-06 |
| Phase 4: Docs ambiguity consolidation | unassigned | TBD | `swarm/p4-docs-ambiguity-consolidation` | `C:\git\tmp1\JD.AI.worktrees\p4-docs-ambiguity-consolidation` | TBD | ready | 2026-03-06 |

### Claiming Protocol

- [ ] Create/assign issue before coding; paste issue link into the board.
- [ ] Claim by setting `Owner`, `Branch`, and `Status=in_progress`.
- [ ] Keep PR scope to one workstream row.
- [ ] On merge, set `Status=merged` and add PR link.
- [ ] If blocked, set `Status=blocked` with blocker note in issue/PR.

## Backlog

### 1) Command System: Single Source of Truth

- [ ] Create a typed command manifest (`name`, aliases, args schema, handler binding, help text, completion metadata).
- [ ] Generate slash dispatch map, help output, and completion entries from the manifest.
- [ ] Remove duplicated definitions in `SlashCommandRouter` and `SlashCommandCatalog`.
- [ ] Add tests that diff generated help/completions against dispatch bindings.

### 2) Config Keys: Descriptor-Driven Mapping

- [ ] Replace `/config` key switch statements with a single key descriptor registry (`key`, parser, getter, setter, persistence policy).
- [ ] Generate `list/get/set` behavior from descriptors.
- [ ] Normalize key aliasing (`welcome.cwd`, `welcome_cwd`, etc.) through one canonical alias map.
- [ ] Add tests asserting every listed key is gettable/settable and persisted correctly when applicable.

### 3) Budget/Cost Correctness

- [ ] Introduce `ICostEstimator` (or similar policy) for session spend and budget enforcement.
- [ ] Use model metadata (`InputCostPerToken`, `OutputCostPerToken`) when available; fallback policy otherwise.
- [ ] Remove hardcoded `0.015m` output-only pricing path.
- [ ] Reconcile `UsageTools` and budget tracker to ensure one consistent cost model.
- [ ] Add scenario tests for metadata-present, metadata-missing, and local-model zero-cost paths.

### 4) Startup Constants and Identity Centralization

- [ ] Centralize gateway defaults (`host`, `port`, health endpoint) in one shared config class.
- [ ] Centralize daemon service identifiers (`JDAIDaemon`, `jdai-daemon`) behind platform-aware constants.
- [ ] Refactor welcome probe and daemon managers to consume shared constants.
- [ ] Add tests to assert constant parity across modules.

### 5) Welcome Panel Performance and Clarity

- [ ] Skip daemon/gateway probing if `welcome_services=false`.
- [ ] Skip MoTD fetch entirely unless both `welcome_motd=true` and URL is configured.
- [ ] Add timing instrumentation for welcome build path (cold/warm startup).
- [ ] Add perf guardrails in CI for startup regression trends.

### 6) Provider Orchestration Simplification

- [ ] Extract provider detector composition into a typed detector manifest/factory.
- [ ] Reduce branching in selection path using policy objects (CLI override policy, default policy, interactive policy).
- [ ] Eliminate repeated candidate filtering blocks with reusable predicates/specifications.
- [ ] Add tests that prove deterministic selection precedence.

### 7) PatternKit Adoption Opportunities

- [ ] Apply Strategy pattern for platform-specific service status probes (Windows/systemd/other).
- [ ] Apply Chain of Responsibility (or Pipeline) for startup selection flow (CLI override -> saved defaults -> full scan -> interactive).
- [ ] Apply Specification pattern for model/provider matching predicates.
- [ ] Evaluate source-generated pattern scaffolding from PatternKit for command/config registries.
- [ ] Add a short architecture note documenting chosen patterns and where generators are authoritative.

### 8) Analyzer and Generator Guardrails

- [ ] Add analyzer rule(s) to flag duplicated command identifiers across router/catalog/help.
- [ ] Add analyzer rule(s) to flag hardcoded runtime constants where shared constants exist.
- [ ] Add generator snapshot tests for command/config manifests.
- [ ] Fail CI on drift between generated and hand-authored command/config assets.

### 9) Dead Code / Ambiguity Audit Pass

- [ ] Identify ambiguous naming (`default`, `global`, `project`, `session-only`) and standardize terminology.
- [ ] Remove stale comments implying outdated behavior once refactors land.
- [ ] Consolidate duplicate docs sections for `/config` keys and command behavior.
- [ ] Add architecture decision record(s) for major centralization moves.

## Execution Order (Recommended)

- [ ] Phase 1: Budget/cost correctness + startup probe short-circuiting.
- [ ] Phase 2: Command/config single-source generation.
- [ ] Phase 3: Constants/identity centralization + provider orchestration cleanup.
- [ ] Phase 4: Analyzer/generator guardrails + docs consolidation.
