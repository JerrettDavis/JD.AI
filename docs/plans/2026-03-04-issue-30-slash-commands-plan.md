# Issue #30 Implementation Plan and TODO

## Goal

Implement the feature request in [issue #30](https://github.com/JerrettDavis/JD.AI/issues/30) by extending JD.AI slash commands for review, security analysis, theming, vim mode, stats, configuration, profile management, memory editing, and output style control.

## Scope and Delivery Strategy

1. Add command plumbing and persistent settings first.
2. Implement high-value review/security commands.
3. Implement UX/state commands (`/theme`, `/vim`, `/output-style`, `/config`, `/stats`).
4. Implement profile/memory commands (`/agents`, `/hooks`, `/memory`).
5. Update completions/tests and validate with build + tests.

## TODO Checklist

- [x] Add persistent command settings:
  - [x] `theme`
  - [x] `vimMode`
  - [x] `outputStyle`
  - [x] Keep existing `spinnerStyle`
- [x] Add slash command routing and help entries for:
  - [x] `/review`
  - [x] `/security-review`
  - [x] `/theme`
  - [x] `/vim`
  - [x] `/stats`
  - [x] `/config`
  - [x] `/agents`
  - [x] `/hooks`
  - [x] `/memory`
  - [x] `/output-style`
- [x] Implement `/review` with branch/target diff support.
- [x] Implement `/security-review` with changed-files and `--full` scanning.
- [x] Wire live runtime behavior:
  - [x] Apply theme updates at runtime.
  - [x] Apply output style updates at runtime.
  - [x] Apply vim mode updates at runtime.
- [x] Add profile persistence for:
  - [x] `~/.jdai/agents.json`
  - [x] `~/.jdai/hooks.json`
- [x] Add/adjust tests for new command surface and completion updates.
- [ ] Create feature branch PR with implementation summary and test evidence.

## Notes

- `/security-review` uses deterministic OWASP/CWE-style heuristic checks for predictable output.
- `/review` is model-assisted using git diff + changed file context.
- `/agents` and `/hooks` are file-backed CRUD-style management commands suitable for iterative enhancements.
