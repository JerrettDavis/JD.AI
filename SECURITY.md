# Security Policy

## Supported Versions

| Version | Supported          |
|---------|--------------------|
| 1.x     | ✅ Current         |

## Reporting a Vulnerability

If you discover a security vulnerability, please report it responsibly:

1. **Do not** open a public issue
2. Email security concerns to the repository owner
3. Include steps to reproduce the vulnerability
4. Allow reasonable time for a fix before public disclosure

## Security Considerations

JD.AI executes shell commands and file operations as directed by AI models.
Users should:

- Review tool invocations before confirming execution
- Use sandboxed execution modes when available
- Avoid running with elevated privileges unnecessarily
- Be cautious with untrusted AI provider endpoints

## Credential Management

JD.AI uses an encrypted credential store to protect API keys and tokens:

- **Windows**: Credentials are encrypted using DPAPI (Data Protection API)
- **Linux / macOS**: Credentials are encrypted using AES
- API keys are **always stored encrypted**, never in plain text
- Credential resolution chain (in priority order):
  1. CLI flags (e.g., `--api-key`)
  2. Environment variables
  3. Encrypted credential store
  4. OAuth flow (interactive)
- Use the `/provider add` wizard for secure credential setup

## MCP Security

MCP (Model Context Protocol) server connections are **local-only by default**. Users must explicitly configure remote MCP servers — no remote connections are made automatically.

## Local Model Security

Local model files are loaded **only from user-specified paths**. JD.AI does not automatically download remote model files without explicit user consent.

## Session Data

Session data is stored in a local SQLite database. There is **no cloud sync** — all session data remains on the user's machine.
