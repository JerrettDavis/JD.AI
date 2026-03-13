# Spec Audit Matrix

Last updated: 2026-03-13

## Scope Reviewed
- `tests/JD.AI.Specs` (Reqnroll BDD core/gateway)
- `tests/JD.AI.Specs.UI` (dashboard UI BDD)
- `tests/JD.AI.Tests` (unit + guardrails)

## Added in This Pass
- New BDD scenarios for persisted tool permissions in:
  - `AtomicConfigStore.feature`
  - `AtomicConfigStoreSteps.cs`
- New router unit coverage for:
  - `/permissions allow <tool> [global|project]`
  - `/permissions deny <tool> [global|project]`
  - `/tool-history` no-history behavior
  - `/help` includes `/tool-history`

## Resolved High-Priority Gaps
- Tool history interactive action flow:
  - Deterministic tests now cover allow/deny actions and rewind-before/after flows.
- Explicit permission enforcement end-to-end:
  - Shared permission gate is now tested for parity across structured SK calls and text-emitted tool calls.
- TUI behavior contracts:
  - Mode-bar stability and permission-mode transition specs were added for non-dup and state correctness.
- Provider parity tool execution:
  - Regression specs now cover duplicate tool invocation/render behavior in text-fallback paths.

## Current Focus
1. Extend parity suites to additional provider/channel adapters as new call patterns are introduced.
2. Keep matrix and regression suites in lockstep with any tool pipeline/refactor changes.
