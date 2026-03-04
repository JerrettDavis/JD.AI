# Issue #65 Implementation Plan

Date: 2026-03-04
Issue: https://github.com/JerrettDavis/JD.AI/issues/65

## Objective
Add an OpenClaw-compatible tool envelope and alias compatibility layer on top of JD.AI native tools without weakening policy enforcement.

## Scope
- Add compatibility aliases (`bash`, `read`, `edit`, `ls`, `webfetch`, `websearch`, `todo_read`, `todo_write`).
- Implement shared envelope parameters where applicable:
  - `summary`
  - `maxResultChars`
  - `noContext`
  - `noStream`
  - `timeoutMs`
- Ensure translation occurs before policy checks and execution.
- Add telemetry for original alias vs resolved tool name.

## Deliverables
- Core translation/mapping implementation.
- Validation and deterministic error handling.
- Unit and integration tests for mapping and parameter behavior.
- Documentation updates for compatibility mode.

## Initial Task Breakdown
1. Identify current tool dispatch points and extension hooks.
2. Implement alias resolver and normalized invocation contract.
3. Apply common parameter handling and policy guardrails.
4. Add tests and docs.

## Risks
- Behavior drift between native and compatibility calls.
- Context-leak edge cases around `noContext`.
- Inconsistent truncation behavior across tools.

## Definition of Done
- Acceptance criteria in #65 satisfied.
- CI green.
- Docs updated with examples and limits.
