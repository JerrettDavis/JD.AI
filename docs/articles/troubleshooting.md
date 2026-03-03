# Troubleshooting

## Installation issues

| Problem | Solution |
|---------|----------|
| `jdai` command not found | Ensure `~/.dotnet/tools` is in PATH |
| .NET 10 SDK not found | Install from https://dotnet.microsoft.com |
| Permission denied on install | Use `--tool-path` for local install |

## Provider issues

### Claude Code
| Problem | Solution |
|---------|----------|
| "Not authenticated" | Run `claude auth login` to re-authenticate |
| Session expired | JD.AI auto-attempts refresh; if it fails, re-login |
| "Claude Code: Not available" | Install: `npm install -g @anthropic-ai/claude-code` |

### GitHub Copilot
| Problem | Solution |
|---------|----------|
| "Not authenticated" | Run `gh auth login --scopes copilot` |
| No models listed | Ensure Copilot subscription is active |

### Ollama
| Problem | Solution |
|---------|----------|
| "Not available" | Start Ollama: `ollama serve` |
| No models | Pull a model: `ollama pull llama3.2` |
| Connection refused | Check if Ollama is running on port 11434 |

### OpenAI Codex
| Problem | Solution |
|---------|----------|
| "Not authenticated" | Run `codex auth login` or set `OPENAI_API_KEY` env var |
| API key not working | Verify key is valid at https://platform.openai.com/api-keys |
| "Codex: Not available" | Install: `npm install -g @openai/codex` |

### Local models (LLamaSharp)
| Problem | Solution |
|---------|----------|
| "No models detected" | Add a model: `/local add /path/to/model.gguf` or `/local download TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF` |
| Model fails to load (OOM) | Use a smaller model or lower quantization (Q4_K_M) |
| Slow inference | Install CUDA drivers for GPU offload; check startup status shows `[Cuda]` not `[Cpu]` |
| HuggingFace search fails | Set `HF_TOKEN` env var for authenticated access |

## Runtime issues

| Problem | Solution |
|---------|----------|
| Context too long | Use `/compact` to compress history |
| Tool execution timeout | Increase timeout or check command |
| Cursor position error | Terminal width too narrow; resize window |
| Crash on Ctrl+C | Fixed in latest version; update with `/update` |
| Session not found | Check `~/.jdai/sessions.db` exists |

## Getting help
- Check `/help` for available commands
- View project instructions with `/instructions`
- File issues at https://github.com/JerrettDavis/JD.AI/issues
