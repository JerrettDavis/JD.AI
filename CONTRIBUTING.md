# Contributing to JD.AI

Thank you for your interest in contributing!

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/<you>/JD.AI.git`
3. Create a branch: `git checkout -b feature/my-feature`
4. Make your changes
5. Run tests: `dotnet test`
6. Commit: `git commit -m "feat: add my feature"`
7. Push: `git push origin feature/my-feature`
8. Open a Pull Request

## Development Prerequisites

- .NET 10.0 SDK
- At least one AI provider for integration testing (Ollama recommended)

## Building

```bash
dotnet build JD.AI.slnx
```

## Testing

```bash
# Unit tests only
dotnet test --filter "Category!=Integration"

# All tests (requires Ollama)
dotnet test
```

### Test Categories

The project has **772+ tests** across **3 test projects** (`JD.AI.Tests`, `JD.AI.Gateway.Tests`, `JD.AI.IntegrationTests`). Follow these test naming conventions:

- Test methods: `MethodName_Scenario_ExpectedResult`
- Test classes: `{ClassUnderTest}Tests`
- Integration tests must be marked with `[Trait("Category", "Integration")]`

## Code Style

- Follow `.editorconfig` rules
- Run `dotnet format` before committing
- Add XML doc comments for public APIs
- Keep test coverage high

## Code Quality Requirements

All pull requests must pass the following analyzer checks (CI enforced):

- **Formatting**: `dotnet format --verify-no-changes` must pass with zero warnings
- **Meziantou.Analyzer**:
  - `MA0006` — Use `StringComparison` for string comparisons
  - `MA0144` — Use `OperatingSystem.IsWindows()` instead of `RuntimeInformation`
- **xUnit**: `xUnit1030` — Do not use `ConfigureAwait(false)` in test methods
- **Code Analysis**:
  - `CA1002` / `CA2227` — Use proper collection types (`List<T>` suppressed only for POCO serialization scenarios)
- **Additional analyzers active**: SonarAnalyzer, AsyncFixer, Roslynator

## Project Structure

The solution contains **17 projects** (14 src + 3 test), organized by layer:

| Layer | Projects |
|-------|----------|
| **Core** | `JD.AI.Core` (shared library) |
| **Applications** | `JD.AI` (TUI), `JD.AI.Gateway`, `JD.AI.Daemon` |
| **Channels** | `Discord`, `Signal`, `Slack`, `Telegram`, `Web`, `OpenClaw` |
| **Libraries** | `Plugins.SDK`, `Workflows`, `Telemetry` |
| **UI** | `Dashboard.Wasm` |
| **Tests** | `JD.AI.Tests` (unit), `JD.AI.Gateway.Tests`, `JD.AI.IntegrationTests` |

## DRY Extension Points

To reduce boilerplate and keep changes localized, use these shared extension points:

- **API-key providers**: derive from [`ApiKeyProviderDetectorBase`](src/JD.AI.Core/Providers/ApiKeyProviderDetectorBase.cs) and override:
  - `KnownModels`
  - `ConfigureKernel(...)`
- **Slash command completions**: add or update completion entries in [`SlashCommandCatalog`](src/JD.AI/Commands/SlashCommandCatalog.cs).  
  Do not duplicate completion registrations in `Program.cs` or tests.

When adding commands/providers, update the corresponding unit tests (`CompletionProviderTests`, provider detector tests).

## Commit Messages

We use [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` — New features
- `fix:` — Bug fixes
- `docs:` — Documentation changes
- `test:` — Test additions/changes
- `chore:` — Maintenance tasks
- `refactor:` — Code restructuring

## CI Checks

Every pull request must pass the following **11 CI checks**:

1. Label PR
2. pr-checks
3. analyze
4. Dependency Review
5. Validate PR
6. validate-docs
7. release
8. publish-docs
9. Label PR Size
10. CodeQL
11. Test Results

## Versioning

This project uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) for automatic semantic versioning. Version numbers are derived from `version.json` and the git height — do not manually set version numbers.

## Pull Request Process

1. Ensure all tests pass
2. Update CHANGELOG.md for user-facing changes
3. Fill out the PR template
4. Request review from @jerrettdavis
