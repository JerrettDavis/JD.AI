# AI Providers

JD.AI detects and connects to AI providers automatically on startup. No manual configuration is required — install a provider, authenticate if needed, and JD.AI handles the rest.

## Provider detection

On startup, JD.AI checks for available providers in this order:

1. **Claude Code** — checks for a local CLI session
2. **GitHub Copilot** — checks for GitHub authentication
3. **Ollama** — checks for a local server at `localhost:11434`

The first provider with a valid connection becomes the default. Results are shown on startup:

```text
Detecting providers...
  ✅ Claude Code: Authenticated — 1 model(s)
  ✅ GitHub Copilot: Authenticated — 3 model(s)
  ✅ Ollama: 59 model(s) available
```

## Claude Code

**Setup:**

1. Install Claude Code: `npm install -g @anthropic-ai/claude-code`
2. Authenticate: `claude auth login`
3. JD.AI detects the session automatically

**How it works:**

- Uses the Claude Code CLI's OpenAI-compatible endpoint.
- Supports Claude Sonnet, Opus, and Haiku models.
- Auto-refresh: JD.AI attempts to renew expired sessions.

> [!TIP]
> **Best for:** Complex reasoning, long-form code generation, and nuanced analysis.

## GitHub Copilot

**Setup:**

1. Authenticate via GitHub CLI: `gh auth login --scopes copilot`
2. Or via VS Code: sign in to the GitHub Copilot extension.
3. JD.AI detects available Copilot models.

**How it works:**

- Uses Copilot token exchange for OpenAI-compatible API access.
- Available models include GPT-4o, Claude Sonnet, Gemini, and more.
- Auto-refresh: JD.AI attempts to renew expired tokens.

> [!TIP]
> **Best for:** Teams already using GitHub and access to multiple model families.

## Ollama

**Setup:**

1. Install Ollama from <https://ollama.com>.
2. Start the server: `ollama serve`
3. Pull models: `ollama pull llama3.2` (chat) and `ollama pull all-minilm` (embeddings).
4. JD.AI auto-detects all available models.

**How it works:**

- Connects to the local Ollama API at `http://localhost:11434`.
- Lists all locally available models.
- No authentication required.

> [!TIP]
> **Best for:** Offline usage, privacy, fast iteration, and experimentation.

## Switching providers and models

Use slash commands to manage providers and models at any time during a session:

```text
/providers          # List all detected providers with status
/provider           # Show current provider and model
/models             # List all available models across providers
/model qwen3:30b    # Switch to a specific model
```

## Provider comparison

| Feature | Claude Code | GitHub Copilot | Ollama |
|---|:-:|:-:|:-:|
| **Setup** | CLI auth | GitHub auth | Local install |
| **Internet required** | Yes | Yes | No |
| **Cost** | Claude subscription | Copilot subscription | Free (local) |
| **Model variety** | Claude family | Multi-family | Open source |
| **Speed** | Fast | Fast | Depends on hardware |
| **Privacy** | Cloud | Cloud | Fully local |
| **Embedding support** | Yes | Limited | Yes |

## Environment variables

| Variable | Description | Default |
|---|---|---|
| `OLLAMA_ENDPOINT` | Ollama API URL | `http://localhost:11434` |

## See also

- [Overview](overview.md)
- [Quickstart](quickstart.md)
