# Spec Audit Matrix

Last updated: 2026-03-12

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

## High-Priority Remaining Gaps
- Tool history interactive action flow:
  - Allow/Deny actions from history selection are not covered by deterministic tests.
  - Rewind-before/after behavior from `/tool-history` is not covered by automated tests.
- Explicit permission enforcement end-to-end:
  - No integration test currently validates both SK auto tool calls and text-emitted tool calls share the same explicit-permission gate.
- TUI behavior contracts:
  - Mode-bar rendering stability across turns is not covered by snapshot/spec tests.
- Provider parity tool execution:
  - Cross-provider/cross-shell tool behavior parity scenarios (Claude Code, Copilot, OpenClaw style calls) need integration-level specs.

## Suggested Next Spec Sprint
1. Add deterministic `ToolHistory` action tests by extracting action handling into a testable service.
2. Add integration spec for explicit deny/allow across:
   - Structured tool calls
   - Text tool-call fallback (`<tool_call>`, `<tool_use>`, fenced shell).
3. Add regression spec around duplicate tool rendering and duplicate invocation.
4. Add CLI/TUI state-transition specs for permission mode display and prompt loop behavior.
