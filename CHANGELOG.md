# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
via [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning).

## [Unreleased]

### Added
- Initial standalone release extracted from JD.SemanticKernel.Extensions samples
- Multi-provider support: Claude Code, GitHub Copilot, Ollama
- Streaming responses with thinking block rendering
- Interactive TUI with Spectre.Console
- Tool system: file operations, shell commands, web fetch
- Slash commands: /help, /model, /provider, /compact, /save, /load, /quit
- Session persistence (save/load conversations)
- Auto-update checking via NuGet
- Clipboard paste support (text blocks, images, files)
- Subagent orchestration (explore, task, plan, review, general)
- Team execution strategies: sequential, fan-out, supervisor, debate
- JDAI.md project instructions support
- Comprehensive test suite (281+ unit tests)
- CI/CD with GitHub Actions
- DocFX documentation
