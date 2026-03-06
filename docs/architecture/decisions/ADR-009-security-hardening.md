# ADR-009: Agent Security Hardening — Secret Detection, Prompt Safety, and Outbound Scanning

**Date**: 2025-07  
**Status**: Accepted  
**Deciders**: JD  
**Issue**: [#191](https://github.com/JerrettDavis/JD.AI/issues/191)

---

## Context

As JD.AI evolves from a developer tool into an agentic runtime, it gains access to
sensitive systems: API keys, cloud providers, code repositories, and CI/CD pipelines.
Without systemic protections, several threat vectors exist:

1. **Secret exfiltration** — an agent tool inadvertently includes an API key in an
   outbound HTTP request payload.
2. **Prompt injection** — malicious content in a document or web page instructs the
   agent to reveal secrets or bypass safeguards.
3. **Lack of a canonical pattern library** — each developer integrating security scans
   must independently curate regex patterns, leading to inconsistent coverage.

JD.AI already ships `DataRedactor` (pattern-based content redaction) and compliance
presets (SOC2/GDPR/HIPAA/PCI-DSS). The gap is translating those foundations into
actionable, composable security components.

## Decision

Introduce three new components in `JD.AI.Core.Security`:

### 1. `SecretPatternLibrary`

A static library of curated, compiled-ready regex patterns covering:

| Pattern | Type |
|---|---|
| `AwsAccessKeyId` | AWS AKIA… keys |
| `AwsSecretAccessKey` | AWS secret key env-var assignments |
| `GitHubClassicPat` / `GitHubFineGrainedPat` / `GitHubOAuthToken` / `GitHubActionsToken` | GitHub PATs |
| `OpenAiKey` / `AnthropicKey` / `HuggingFaceToken` | AI API keys |
| `StripeSecretKey` | Stripe sk_live / sk_test |
| `Jwt` | JSON Web Tokens |
| `PemPrivateKey` | PEM headers |
| `GenericBase64Secret` | High-entropy generic secrets |
| `DatabaseConnectionString` | Connection string passwords |

Two subsets are provided: `All` (maximum recall, some false positives) and
`HighConfidence` (minimum false positives, suitable for blocking).

### 2. `PromptSafetyChecker`

Detects common prompt injection attack patterns before a prompt reaches the model:

- Instruction override attempts ("ignore all previous instructions")
- System prompt extraction requests
- Secret reveal commands
- Jailbreak preambles (DAN, "do anything now")
- Prompt delimiter injection (`### SYSTEM`, `<|im_start|>`, `[INST]`)
- Exfiltration-to-URL commands

Returns a `PromptSafetyResult` with `IsSafe` and a list of violation names.
Custom rules can be injected via the `(IEnumerable<(string, string)>)` constructor.

### 3. `OutboundSecretScanningHandler`

An `HttpMessageHandler` / `DelegatingHandler` that:

1. Reads the outbound request body.
2. Passes it through `DataRedactor`.
3. If any content was redacted (secrets found), either throws `SecurityException`
   (blocking mode, default) or logs a warning and allows the request (audit mode).
4. Also inspects `Authorization: Bearer` tokens of significant length.

### 4. `SecurityException`

A typed exception that wraps security policy violations, distinct from general
`InvalidOperationException`. Callers can catch this specifically for security alerts.

## Consequences

### Positive

- `SecretPatternLibrary.HighConfidence` can immediately be wired into `DataRedactor`
  to block secrets from reaching AI providers, without duplicating regex research.
- `PromptSafetyChecker.Default` provides zero-configuration injection detection; teams
  can extend it with domain-specific patterns.
- `OutboundSecretScanningHandler` is a drop-in `DelegatingHandler` — no code changes
  needed in tool implementations, only `HttpClient` registration.
- All three components are independently composable and testable with mocks.

### Negative / Trade-offs

- **Regex-based detection has false positives and negatives**. The `HighConfidence`
  subset minimizes false positives; the `All` subset is better for auditing.
- **Outbound scanning reads the full body into memory** for pattern matching. This is
  acceptable for API payloads but should not be applied to file upload streams.
- **Prompt safety patterns are heuristic**, not ML-based. Sophisticated adversarial
  prompts using obfuscation or encoding may evade detection.

## Alternatives Considered

**ML-based prompt safety classifier** — too heavyweight for a synchronous inline check.
Could be an optional async pre-filter in a future enhancement.

**Integrate patterns directly into DataRedactor** — rejected; keeps concerns separate.
`SecretPatternLibrary` is a static data source; `DataRedactor` is the engine.

**Block all outbound HTTP from agents by default** — too restrictive; many legitimate
tools call external APIs. Policy-driven allow/deny lists are a better long-term solution.

## Related

- `DataRedactor` — consumes `SecretPatternLibrary` patterns via `new DataRedactor(SecretPatternLibrary.All)`
- Compliance presets (ADR-007) — `DataPolicy.Classifications` works with `DataRedactor`
- ADR-008 — credential store backends (separate from in-flight secret scanning)
