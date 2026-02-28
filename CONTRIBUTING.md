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

## Code Style

- Follow `.editorconfig` rules
- Run `dotnet format` before committing
- Add XML doc comments for public APIs
- Keep test coverage high

## Commit Messages

We use [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` — New features
- `fix:` — Bug fixes
- `docs:` — Documentation changes
- `test:` — Test additions/changes
- `chore:` — Maintenance tasks
- `refactor:` — Code restructuring

## Pull Request Process

1. Ensure all tests pass
2. Update CHANGELOG.md for user-facing changes
3. Fill out the PR template
4. Request review from @jerrettdavis
