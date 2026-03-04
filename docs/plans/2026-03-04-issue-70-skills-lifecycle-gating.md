# Issue #70 Implementation Plan

Date: 2026-03-04
Issue: https://github.com/JerrettDavis/JD.AI/issues/70

## Objective
Build a native managed skill lifecycle with deterministic precedence, metadata gating, and hot-reload behavior.

## Scope
- Skill precedence model:
  - bundled
  - managed (`~/.jdai/skills`)
  - workspace (`<repo>/.jdai/skills`)
- Eligibility gates:
  - OS
  - required binaries
  - env/config dependencies
  - bundled allowlist support
- File watcher + cache invalidation.
- Per-run env injection and restoration semantics.

## Deliverables
- Skill discovery/merge service with precedence rules.
- Eligibility evaluator with reason codes.
- Watcher-driven refresh pipeline.
- Tests and documentation updates.

## Initial Task Breakdown
1. Assess current SKILL.md loading and integration points.
2. Add unified skill source model and precedence resolution.
3. Implement gating evaluator and status reporting.
4. Implement watcher and reload behavior.

## Risks
- Inconsistent behavior across OS for binary detection.
- Mid-session reload side effects.
- Secret injection safety and transcript leakage.

## Definition of Done
- Acceptance criteria in #70 satisfied.
- CI green with realistic tests.
- Docs updated with operational guidance.
