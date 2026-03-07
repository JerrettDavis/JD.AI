---
title: Troubleshooting
description: Common problems and solutions — installation, providers, local models, sessions, performance, and diagnostics.
---

# Troubleshooting

This page covers common issues you might encounter with JD.AI and how to resolve them. Start with the `/doctor` command for a quick health check, then look for your specific issue below.

## Quick diagnostics with /doctor

Run `/doctor` to check system health in one step:

```text
/doctor
```

```text
=== JD.AI Doctor ===
Version:  1.0.0
Runtime:  .NET 10.0.0
Health:   ✔ Healthy

Checks:
  ✔ Gateway       — Gateway operational
  ✔ Providers     — 2/3 providers reachable
  ✔ Memory        — 142 MB managed heap
  ✔ Session Store — SQLite OK (14 sessions)
```

If any check shows ⚠ or ✗, the description tells you what's wrong.

## Installation issues

| Problem | Solution |
|---------|----------|
| `jdai` command not found | Ensure `~/.dotnet/tools` is in your PATH. On Linux/macOS: `export PATH="$PATH:$HOME/.dotnet/tools"`. On Windows: restart your terminal after installing. |
| .NET 10 SDK not found | Install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download). Verify with `dotnet --version`. |
| Permission denied on install | Use `--tool-path` for a local install: `dotnet tool install JD.AI --tool-path ./tools` |
| NuGet restore fails | Check internet connectivity. Try `dotnet nuget locals all --clear` then reinstall. |
| Update fails | Run `dotnet tool update --global JD.AI`. If that fails, uninstall and reinstall: `dotnet tool uninstall --global JD.AI && dotnet tool install --global JD.AI` |

## Provider connection problems

### Claude Code

| Problem | Solution |
|---------|----------|
| "Not authenticated" | Run `claude auth login` to re-authenticate |
| Session expired | JD.AI auto-attempts refresh; if it fails, run `claude auth login` again |
| "Claude Code: Not available" | Install the CLI: `npm install -g @anthropic-ai/claude-code` |

### GitHub Copilot

| Problem | Solution |
|---------|----------|
| "Not authenticated" | Run `gh auth login --scopes copilot` |
| No models listed | Ensure your Copilot subscription is active. Check with `gh copilot status`. |
| Token refresh failed | Run `gh auth refresh --scopes copilot` |

### OpenAI / Codex

| Problem | Solution |
|---------|----------|
| "Not authenticated" | Run `codex auth login` or set `OPENAI_API_KEY` environment variable |
| API key not working | Verify the key at [platform.openai.com/api-keys](https://platform.openai.com/api-keys) |
| "Codex: Not available" | Install the CLI: `npm install -g @openai/codex` |
| Rate limited | Wait and retry. Consider upgrading your API tier. |

### Ollama

| Problem | Solution |
|---------|----------|
| "Not available" | Start the server: `ollama serve` |
| No models found | Pull a model: `ollama pull llama3.2` |
| Connection refused | Verify Ollama is running on port 11434. Check with `curl http://localhost:11434/api/tags`. |
| Custom endpoint not detected | Set `OLLAMA_ENDPOINT` environment variable to your Ollama URL |

### API key providers (OpenAI, Anthropic, Gemini, Mistral, etc.)

| Problem | Solution |
|---------|----------|
| "Invalid API key" | Verify the key is correct and not expired. Re-enter via `/provider add <name>`. |
| "Connection refused" | Check internet connectivity and firewall rules. |
| Wrong models listed | The provider may have updated available models. Run `/providers` to refresh. |
| Azure endpoint issues | Verify `AZURE_OPENAI_ENDPOINT` includes the full URL (e.g., `https://myresource.openai.azure.com/`). |

### Testing provider connections

Test all providers or a specific one:

```text
/provider test           # Test all configured providers
/provider test openai    # Test a specific provider
```

## Local model issues

| Problem | Solution |
|---------|----------|
| "No models detected" | Add a model: `/local add /path/to/model.gguf` or download one: `/local download TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF` |
| Model fails to load (OOM) | Use a smaller model or lower quantization (Q4_K_M). Check available RAM with `/doctor`. |
| Slow inference | Install CUDA drivers for GPU offload. Check startup status shows `[Cuda]` not `[Cpu]`. Use Q4_K_M instead of Q8_0. |
| HuggingFace search fails | Set `HF_TOKEN` environment variable. Check network connectivity. |
| CUDA not detected | Ensure NVIDIA drivers are installed. Check `nvidia-smi` works. Restart JD.AI after driver installation. |
| Model gives poor results | Try a larger model or higher quantization. Small models (1-3B) are best for testing, not production use. |

## Session issues

| Problem | Solution |
|---------|----------|
| Session not found | Check `~/.jdai/sessions.db` exists. Sessions may have been created in a different working directory — try `/sessions` without a project filter. |
| Corrupt session database | Delete `~/.jdai/sessions.db` and restart. Sessions will be lost but a fresh database is created automatically. |
| Cannot resume session | The session may reference a model or provider that's no longer available. Try `--provider <name>` when resuming. |
| Export fails | Check write permissions on `~/.jdai/exports/`. Create the directory manually if missing. |

## Performance issues

| Problem | Solution |
|---------|----------|
| Context too long / slow responses | Run `/compact` to compress conversation history. Start new sessions for unrelated tasks. |
| High memory usage | Local models consume significant RAM. Switch to a smaller model or use a cloud provider. |
| Tool execution timeout | Increase the timeout in your prompt (e.g., "run the tests with a 5-minute timeout"). Some builds need more time. |
| Slow startup | Many providers detected — startup checks each one. Set a default provider with `/default provider <name>` to prioritize. |

## Common error messages

| Error | Meaning | Solution |
|-------|---------|----------|
| `CS1061: does not contain a definition` | C# compilation error in a tool result | This is a code error, not a JD.AI error — fix the code |
| `Context window exceeded` | Conversation is too long for the model | Run `/compact` or start a new session |
| `Tool execution denied` | You declined a tool confirmation | Approve the tool or use `/autorun` for trusted operations |
| `EPERM: operation not permitted` | File permission issue | Check file permissions. Avoid modifying system files. |
| `sqlite3: database is locked` | Another JD.AI instance is using the session DB | Close other JD.AI instances or wait |

## Terminal display issues

| Problem | Solution |
|---------|----------|
| Cursor position error | Terminal width too narrow — resize your terminal window |
| Garbled output | Ensure your terminal supports ANSI escape codes. Try a different terminal emulator. |
| Spinner not animating | Use `/spinner minimal` or `/spinner none` for simpler terminals |

## Getting help

- Run `/help` to see all available commands
- Run `/doctor` for a system health report
- Run `/docs` to browse documentation
- View loaded project instructions with `/instructions`
- File issues at [github.com/JerrettDavis/JD.AI](https://github.com/JerrettDavis/JD.AI/issues)
- Check for updates with `/update`

## See also

- [Installation](installation.md) — prerequisites and setup
- [Provider Setup](provider-setup.md) — configuring each provider
- [Local Models](local-models.md) — GGUF model troubleshooting
- [Configuration](configuration.md) — paths, env vars, and defaults
