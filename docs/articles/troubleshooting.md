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
