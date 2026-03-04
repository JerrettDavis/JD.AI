---
title: Prompt Caching Reference
description: "Automatic prompt caching behavior, Anthropic support, thresholds, TTL settings, and runtime controls."
---

# Prompt Caching Reference

JD.AI includes automatic prompt caching so stable, repeated prompt context can be reused across turns when supported by the active provider/model.

## Supported providers

Current native support:

- Anthropic API-key provider (`/provider add anthropic`)

For unsupported providers, JD.AI leaves request behavior unchanged.

## Runtime controls

Prompt caching is controlled with persisted `/config` keys:

| Key | Allowed values | Default | Description |
|---|---|---|---|
| `prompt_cache` | `on`, `off` | `on` | Master toggle for automatic prompt caching |
| `prompt_cache_ttl` | `5m`, `1h` | `5m` | Cache TTL used where provider supports it |

```text
/config get prompt_cache
/config get prompt_cache_ttl
/config set prompt_cache on
/config set prompt_cache off
/config set prompt_cache_ttl 5m
/config set prompt_cache_ttl 1h
```

These settings are persisted in `tui-settings.json` under the JD.AI data root.

## Auto-enable policy

JD.AI estimates prompt token count from chat history and only enables caching when context size is large enough to benefit:

- Claude Sonnet / Opus families: `>=1024` estimated tokens
- Claude Haiku family: `>=2048` estimated tokens

This avoids adding caching directives on short prompts where overhead can outweigh benefit.

## Anthropic behavior

When prompt caching is enabled for Anthropic:

- Requests are sent via Anthropic's native Messages API (`Anthropic.SDK`).
- Prompt caching mode is set to automatic caching for tools/system.
- Cache control checkpoints are applied to the last system/tool cache breakpoints.
- TTL is set from `prompt_cache_ttl` (`5m` or `1h`).

## Execution coverage

The same policy is applied across JD.AI execution surfaces:

- Interactive turns (streaming + non-streaming)
- Subagents and team orchestration executors
- Model analysis workflows
- Gateway agent turns

## See also

- [Commands Reference](commands.md)
- [Configuration Reference](configuration.md)
- [Providers Reference](providers.md)
- [User Guide: Configuration](../user-guide/configuration.md)
