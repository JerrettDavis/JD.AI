---
title: Reference Overview
description: "Quick-lookup reference material for JD.AI CLI flags, commands, tools, providers, and configuration."
---

# Reference

Quick-lookup reference material for JD.AI CLI flags, commands, tools, providers, and configuration. Each article is formatted for scanning — tables, parameter lists, and code blocks with minimal prose.

For tutorials and explanations, see the [User Guide](../user-guide/index.md) and [Developer Guide](../developer-guide/index.md).

## Reference articles

| Article | Description |
|---|---|
| [CLI Reference](cli.md) | All CLI flags, environment variables, exit codes, data directories, and piped input |
| [Commands Reference](commands.md) | All 33+ slash commands with full syntax, options, and examples |
| [Tools Reference](tools.md) | Built-in tools with parameters, return types, safety tiers, and examples |
| [Providers Reference](providers.md) | Provider comparison table, capabilities matrix, credential resolution, env vars |
| [Environment Variables](environment-variables.md) | Consolidated reference of all environment variables by category |
| [Configuration Reference](configuration.md) | config.json schema, JDAI.md syntax, per-project overrides, precedence chain |

## Quick links

- **Install:** `dotnet tool install -g JD.AI`
- **Update:** `dotnet tool update -g JD.AI` or `/update`
- **Help:** `jdai --help` or `/help`
- **Default directory:** `~/.jdai/`

## Conventions used in this reference

| Convention | Meaning |
|---|---|
| `<value>` | Required argument |
| `[value]` | Optional argument |
| `--flag` | CLI flag |
| `/command` | Slash command (interactive prompt) |
| `UPPER_CASE` | Environment variable |
| `~/.jdai/` | JD.AI data directory (`$HOME/.jdai/` on Linux/macOS, `%USERPROFILE%\.jdai\` on Windows) |

## See also

- [User Guide](../user-guide/index.md) — step-by-step tutorials and workflows
- [Developer Guide](../developer-guide/index.md) — in-depth explanations and guides
- [API Reference](https://jerrettdavis.github.io/JD.AI/api/) — .NET API documentation
