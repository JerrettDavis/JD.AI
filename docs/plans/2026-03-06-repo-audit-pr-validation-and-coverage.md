# Repository Audit: PR Validation and Coverage Baseline (2026-03-06)

## Scope

- Audited merged pull requests in `JerrettDavis/JD.AI` from repository creation through 2026-03-06.
- Re-ran CI-aligned coverage locally against `main`.
- Correlated merged PR runtime file changes with current measured coverage and presence of test/spec changes.

## Method

1. Pull merged PR metadata:
   - `gh pr list --state merged --limit 200 --json ...`
2. Collect changed files per PR:
   - Primary: `git show --name-only <mergeCommit>`
   - Fallback: `gh api repos/<repo>/pulls/<n>/files`
3. Re-run CI-equivalent test command with coverage:
   - `dotnet test JD.AI.slnx --configuration Release --filter "Category!=Integration" --collect:"XPlat Code Coverage" ...`
4. Merge coverage reports:
   - `reportgenerator -reports:"tests/**/TestResults/*/coverage.cobertura.xml" ...`
5. Map touched runtime files (`src/JD.AI/*.cs`) to merged coverage and classify PRs:
   - `Validated-strong`, `Validated-indirect`, `Partial-gap`, `Unvalidated-gap`, `Non-runtime`.

## Coverage Baseline (CI-equivalent)

- Line coverage: `43.2%` (`2778 / 6426`)
- Branch coverage: `35.3%` (`1332 / 3763`)
- Method coverage: `62.6%` (`384 / 613`)

Top zero-coverage runtime classes/files:

- `src/JD.AI/Program.cs`
- `src/JD.AI/Startup/InteractiveLoop.cs`
- `src/JD.AI/Startup/SessionConfigurator.cs`
- `src/JD.AI/Startup/GovernanceInitializer.cs`
- `src/JD.AI/Startup/OnboardingCliHandler.cs`
- `src/JD.AI/Startup/PrintModeRunner.cs`
- `src/JD.AI/Startup/SystemPromptBuilder.cs`
- `src/JD.AI/Startup/ToolRegistrar.cs`
- `src/JD.AI/Commands/AgentsCliHandler.cs`
- `src/JD.AI/Commands/PluginCliHandler.cs`
- `src/JD.AI/Commands/UpdateCliHandler.cs`
- `src/JD.AI/Commands/PolicyComplianceCliHandler.cs`
- `src/JD.AI/Commands/PolicySubcommandHandler.cs`
- `src/JD.AI/Rendering/HistoryViewer.cs`

## PR Validation Matrix Summary

- Total merged PRs audited: `144`
- Runtime-touching PRs: `67`
- Classified as `Validated-strong`: `7`
- Classified as `Validated-indirect`: `6`
- Classified as `Partial-gap`: `42`
- Classified as `Unvalidated-gap`: `12`
- Classified as `Non-runtime`: `77`

Most frequently uncovered touched runtime file:

- `src/JD.AI/Program.cs` (appears in 41 uncovered-touch PR rows)

## Highest-Risk Unvalidated PRs

1. `#121` refactor: decompose Program/startup modules
   - `6` runtime files touched, `6` uncovered
2. `#119` refactor: Program decomposition
   - `4` runtime files touched, `2` uncovered
3. `#224` versioned agent registry + CLI
   - `3` runtime files touched, `3` uncovered (partial-gap)
4. `#226` compliance profiles + classification engine
   - `3` runtime files touched, `3` uncovered (partial-gap)
5. `#177` startup fast-path and onboarding defaults
   - `5` runtime files touched, `2` uncovered (partial-gap)

## Generated Artifacts

- `artifacts/coverage-merged/Summary.txt`
- `artifacts/coverage-merged/Cobertura.xml`
- `artifacts/coverage-by-file.csv`
- `artifacts/pr-audit-matrix.csv`
- `artifacts/pr-validation-matrix.csv`

## Recommended Next Work Items

1. Add end-to-end/spec coverage for startup entrypoint and onboarding flows.
2. Add unit/spec tests for CLI subcommand handlers currently at 0%.
3. Add rendering/input tests for model picker, interactive input, history viewer, and service banner probes.
4. Add PR gate that fails when runtime-changing PRs do not include tests/specs (with allowlist for pure refactors/docs).
5. Re-audit all `Partial-gap` and `Unvalidated-gap` PRs after first two coverage waves.
