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
