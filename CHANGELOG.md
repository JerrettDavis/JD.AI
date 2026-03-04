# Changelog

All notable changes to JD.AI are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions are auto-managed by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning);
entries are grouped by feature milestones.

## [Unreleased]

### Added
- **Multi-provider API key support** — 8 new API-key providers: OpenAI, Azure OpenAI, Anthropic, Google Gemini, Mistral, AWS Bedrock, HuggingFace, and OpenAI-Compatible (#42)
- **Dynamic provider/model switching** — ConversationTransformer with 5 handoff modes, AtomicConfigStore, model search, and `/default` commands (#44)
- **Observability** — OpenTelemetry tracing & metrics, health-check endpoints, and `/docs` command integration via JD.AI.Telemetry (#39)
- **Microsoft Foundry Local provider** — native integration with Microsoft Foundry Local (#38)
- **Interactive command expansion** — Added `/review`, `/security-review`, `/theme`, `/vim`, `/stats`, `/config`, `/agents`, `/hooks`, `/memory`, and `/output-style` with persistent UX/runtime configuration improvements

### Fixed
- CI pack: suppress NU5104 for prerelease Semantic Kernel connector dependencies (#45)

## [1.0.x] — Claude Code Parity & MCP

### Added
- **Claude Code feature parity** — print mode, system prompts, 7 new slash commands, and interactive enhancements (#24)
- **MCP server support** — `/mcp add`, `/mcp list`, `/mcp remove` commands with config-file loading (#22)
- **Diff/patch, batch edit & usage tracking tools** — extend the tool system with diff apply, multi-file batch edits, and token-usage tracking (#19)
- **17 new tools** — clipboard read/write, code execution, glob/grep, web fetch enhancements, file-tree, and more for Claude Code/Copilot parity (#18)
- **Ask-questions tool** — interactive TUI questionnaire for gathering user input during agent runs (#17)

### Changed
- Updated tools-reference and index documentation for the 17 new tools

### Fixed
- NU5118/NU5100: suppress LLamaSharp native-file pack conflicts in CI

## [1.0.x] — Local Inference & Codex

### Added
- **Local model inference** — LLamaSharp integration with model discovery, download, and `/local` commands (#15)
- **OpenAI Codex connector** — new provider for OpenAI Codex models (#14)
- Comprehensive documentation for Codex, local models, and new commands

### Fixed
- UTF-8 console encoding and replacement of emoji with safe Unicode glyphs (#13)

## [1.0.x] — Workflows & Spinner Styles

### Added
- **Workflows** — JD.AI.Workflows package with WorkflowFramework integration, in-memory catalog, two-tier matcher, and `/workflow run/list/refine` TUI commands (#11)
- **TUI spinner styles** — configurable spinner animations via `/spinner` command; removed input echo (#12)
- Spinner style and TUI settings tests with VHS tapes

### Fixed
- Use collection abstractions for public API properties
- Credential scanning for user profiles when running as a service
- Duplicate skill and plugin name handling in TUI

## [1.0.x] — Gateway, Channels & Dashboard

### Added
- **Blazor WebAssembly dashboard** — real-time gateway dashboard with web chat, streaming agent responses, WYSIWYG settings editor (6 tabbed sections), and comprehensive UI refresh
- **Gateway control plane** — extracted JD.AI.Core library; config-driven channel registration, agent auto-spawn, and routing
- **6 channel adapters** — Discord, Signal, Slack, Telegram, Web, and OpenClaw bridge projects
- **Channel slash commands** — remote agent and provider control via gateway; `/jdai-` prefixed aliases
- **Daemon mode** — consolidated Gateway API + Dashboard into all-in-one service with systemd/Windows Service support, service deployment CLI, and auto-update system
- **OpenClaw bridge** — WebSocket JSON-RPC with Ed25519 device auth, per-channel routing modes, native agent registration
- **Plugin SDK** — extensible plugin loader, security middleware, memory/embeddings API
- Configurable model parameters (temperature, top-p/k, context window, etc.)
- Retry resilience for transient Ollama errors
- Interactive model picker for `/model` and `/models` commands

### Fixed
- Blazor WASM dashboard asset loading in production
- Dashboard sans-serif font fallback and client model alignment
- OpenClaw routing lifecycle event handling and agent registrar RPC protocol
- Daemon content-root and tool-deployment path resolution
- Sanitize user-controlled values in PluginLoader log entries
- Flaky SlidingWindowRateLimiter expiration boundary in security tests
- Streaming display wiring (SpectreAgentOutput → ChatRenderer)

### Changed
- Centralized data-directory resolution for service accounts
- Replaced PowerShell with bash in all VHS tape files (#8)

## [1.0.x] — Subagents, Sessions & Core TUI

### Added
- **Subagent system** — 5 agent types (explore, task, plan, review, general) with swarm orchestration (#15 precursors)
- **Team execution strategies** — Sequential, Fan-Out, Supervisor, and Debate orchestration patterns
- **Feature parity phases** — JDAI.md project instructions, checkpoints, web search, sandbox support
- **Session persistence** — SQLite conversation store, JSON export, `/save`, `/sessions`, `/resume` commands
- **Interactive history viewer** and idle double-ESC handler
- **Clipboard paste** — text blocks, images, and files with collapsible chips
- **Streaming output** with mid-turn steering input and thinking/reasoning display (dim gray text)
- **Ghost-text completions** — interactive input with dropdown suggestion menu
- `--dangerously-skip-permissions` flag and `/permissions` command
- Double-tap ESC to cancel running operations
- Auto-update check on startup with `/update` command
- Auto-refresh of expired auth via CLI when providers are installed
- Ollama integration test workflow for GitHub Actions

### Fixed
- Graceful Ctrl+C with double-tap exit (no crash)
- ESC treated as "no" in tool permission prompts
- Line-wrapping crash in InteractiveInput on long input
- Ollama HttpClient timeout increased to 10 minutes
- Markup escaping in model selector to prevent Spectre parse errors
- All CI analyzer warnings treated as errors

## [1.0.x] — Initial Release

### Added
- **Standalone extraction** from JD.SemanticKernel.Extensions samples
- **Multi-provider support** — Claude Code, GitHub Copilot, Ollama
- **Interactive TUI** — Spectre.Console-based agent with streaming responses
- **Tool system** — file operations, shell commands, web fetch
- **Slash commands** — `/help`, `/model`, `/provider`, `/compact`, `/save`, `/load`, `/quit`
- **SK extensions integration** — skills, plugins, hooks, compaction
- **DocFX documentation site** — branded theme with light/dark support, 18 articles, VHS tape screenshots, Open Graph/Twitter Card meta tags
- **CI/CD** — GitHub Actions workflows, community files
- Comprehensive TUI unit and integration tests
