# Testing Flows: SDD, BDD, TDD

## Purpose

Use one repeatable path for all features so coverage rises without test bloat.

## 1) SDD (Spec-Driven Development)

When to use:

- New domain behavior.
- Complex policy, routing, auth, or orchestration logic.

Steps:

1. Write a concise spec (`docs/specs/...`) with:
- domain terms
- contract (inputs/outputs)
- invariants
- failure semantics

2. Add acceptance examples as executable scenarios.

3. Implement against the spec, not current implementation.

4. Trace tests back to spec IDs.

Exit criteria:

- Every spec invariant has at least one automated test.
- Error semantics are verified (not only success path).

## 2) BDD (Behavior-Driven Development)

When to use:

- End-user workflows.
- Provider/channel behavior and cross-component integration.

Guidelines:

- Keep Given/When/Then focused on externally visible behavior.
- Avoid asserting private fields or call counts unless contractually required.
- Include unhappy paths in every feature.

Feature template:

```gherkin
Feature: Provider model discovery
  Scenario: Lists available models when auth is present
    Given provider credentials are configured
    When I refresh provider models
    Then the model list includes known models

  Scenario: Skips with clear message when credentials are missing
    Given provider credentials are not configured
    When I refresh provider models
    Then the operation is skipped with a diagnostic reason
```

## 3) TDD (Red-Green-Refactor)

When to use:

- Branch-heavy pure logic.
- Parser/normalizer/decision engine classes.
- Failure-handling paths that are hard to verify via BDD alone.

Cycle:

1. Red: write one failing test for one branch.
2. Green: smallest code change to pass.
3. Refactor: simplify while preserving behavior.

Coverage intent:

- Prioritize branch coverage, not just line coverage.
- Cover null/empty, malformed input, timeout, retry, and cancellation.

## Test layering policy

1. Unit tests (`tests/JD.AI.Tests`, `tests/JD.AI.Gateway.Tests`):
- Fast contract and branch coverage.

2. Behavior specs (`tests/JD.AI.Specs`):
- Integration of multiple components.

3. Integration tests (`tests/JD.AI.IntegrationTests`):
- Real provider/channel behavior.
- Skip when secrets/env are missing.

## Anti-patterns to avoid

- Asserting implementation details instead of behavior.
- Duplicating the same scenario in unit and BDD tests.
- Slow flaky tests in default PR path.
- Hidden secret dependencies without skip guards.

