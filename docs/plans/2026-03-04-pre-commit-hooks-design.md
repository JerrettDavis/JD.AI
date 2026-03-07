# Pre-Commit Hooks & Code Quality Enforcement

**Date:** 2026-03-04
**Status:** Approved

## Goal

Ensure nothing reaches GitHub that wouldn't pass PR validation workflows.
Enforce C#14 style standards with automated cleanup on commit.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Hook mechanism | Husky.Net via `dnx` | Auto-installs on restore, no manual setup, .NET 10 `dnx` eliminates manifest entry |
| Format behavior | Auto-fix + re-stage | Pit of success — formatting just happens |
| Test targeting | Project-dependency mapping | Fast, accurate, skips irrelevant tests |
| Line length | Hard 80, warn | IDE guidance + CI enforcement |
| Code cleanup | JetBrains CleanupCode CLI | Deep structural fixes beyond `dotnet format` |
| Auto-setup | On restore via MSBuild | Zero friction for contributors |

## Tool Strategy

**`dnx` (no install):**

- `dnx husky install` — MSBuild restore target
- `dnx husky run` — invoked by git hooks

**`.config/dotnet-tools.json` (local manifest):**

- `JetBrains.ReSharper.GlobalTools` — code cleanup CLI
- `dotnet-reportgenerator-globaltool` — coverage summary

## Pre-Commit Pipeline

```
1. jb cleanupcode (staged files)  → deep structural cleanup
2. dotnet format (staged files)   → catch remaining warnings
3. git add (fixed files)          → re-stage
4. dotnet build                   → compile check
5. smart-test.sh                  → targeted tests with coverage
```

## Smart Test Targeting

Script at `.husky/scripts/smart-test.sh`:

1. Get staged file list via `git diff --cached --name-only`
2. Extract unique `src/` project directories from changed files
3. Scan `tests/` for `.csproj` files with `<ProjectReference>`
   pointing to changed source projects
4. Skip tests if no matching test projects found
5. Run matched test projects with Coverlet coverage (Cobertura)
6. Generate text coverage summary via ReportGenerator
7. Always exclude `Category=Integration` tests

**Global-impact files** (trigger full test run):

- `Directory.Build.props`, `Directory.Build.targets`
- `.editorconfig`
- `JD.AI.slnx`
- `Directory.Packages.props`

**Test-only changes** run those test projects directly.

## EditorConfig Changes

**Line length:** `max_line_length = 80` (down from 120)

**Rules upgraded to warning (auto-fixable by JB CleanupCode):**

- Primary constructors
- Collection expressions
- Expression-bodied members
- Braces: `when_multiline`
- Null coalescing / propagation / forgiving patterns
- Switch expressions
- Object/collection initializers
- Pattern matching
- Using directive placement
- Modifier ordering
- Unnecessary casts/imports

**Rules kept as suggestion (require developer judgment):**

- Top-level statements (only relevant to Program.cs)
- Async suffix naming convention

## Files To Create/Modify

| File | Action |
|------|--------|
| `.config/dotnet-tools.json` | Create |
| `.husky/task-runner.json` | Create |
| `.husky/scripts/smart-test.sh` | Create |
| `Directory.Build.targets` | Create |
| `Directory.Packages.props` | Modify — fix merge conflict |
| `.editorconfig` | Modify — 80 char, upgrade rules |
| `JD.AI.DotSettings` | Create — CleanupCode profile |
| `.github/workflows/ci.yml` | Modify — `dotnet tool restore` |
| `.github/workflows/pr-validation.yml` | Modify — same |

## Bypass

`git commit --no-verify` for emergencies (standard git).
