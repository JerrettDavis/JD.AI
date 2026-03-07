# ADR-007: Compliance Profiles and Data Classification Engine

## Status

Accepted

## Context

Issue #212 extends `PolicySpec` with two capabilities:

1. **Compliance preset bundles** — named policy templates pre-configured for
   SOC 2, GDPR, HIPAA, and PCI-DSS. Users reference them via `spec.extends`.
2. **Structured data classification** — named classification rules with per-label
   enforcement actions (Redact, RedactAndAudit, AuditOnly, DenyAndAudit).

The existing `DataPolicy.RedactPatterns` list performs global redaction but lacks
the ability to:
- Apply different actions to different types of sensitive data
- Signal that an entire request should be denied vs. just redacted
- Emit audit events labeled with the classification name
- Ship pre-built policy bundles for common regulatory frameworks

## Decision

### Data Classification Model

```csharp
public enum ClassificationAction { Redact, RedactAndAudit, AuditOnly, DenyAndAudit }

public sealed class DataClassification
{
    public string Name { get; set; }
    public IList<string> Patterns { get; set; }
    public ClassificationAction Action { get; set; }
    public IList<string> DenyProviders { get; set; }
}
```

`DataPolicy` gains a `Classifications` list alongside the existing `RedactPatterns`:

```yaml
spec:
  data:
    redactPatterns:
      - '\bSECRET\b'
    classifications:
      - name: PAN
        patterns:
          - '\b\d{16}\b'
        action: DenyAndAudit
        denyProviders: ['*']
```

### Action Semantics

| Action | Content | Audit |
|--------|---------|-------|
| `Redact` | Replaced with `[REDACTED:{name}]` | No |
| `RedactAndAudit` | Replaced with `[REDACTED:{name}]` | Yes |
| `AuditOnly` | Passed through unchanged | Yes |
| `DenyAndAudit` | Not redacted; caller checks `ShouldDeny` | Yes |

The `DenyAndAudit` action does not redact the content — instead it sets
`RedactionResult.ShouldDeny = true`, which callers (e.g., `ToolConfirmationFilter`)
must check and use to block the request entirely.

### DataRedactor Updates

`DataRedactor` gains a second constructor accepting `IEnumerable<DataClassification>`.
The new `RedactWithClassifications(string)` method returns a `RedactionResult`
containing the processed content and a list of `ClassificationMatch` objects.

The original `Redact(string)` method is preserved for backwards compatibility; it
now also applies `Redact` and `RedactAndAudit` classifications after flat patterns.

### Compliance Presets

Presets are embedded YAML resources in `JD.AI.Core`:

```
src/JD.AI.Core/Governance/CompliancePresets/
  soc2.yaml
  gdpr.yaml
  hipaa.yaml
  pci-dss.yaml
```

Each preset is a valid `PolicyDocument` YAML with pre-configured `data.classifications`,
`audit`, and `sessions` settings.

`PolicySpec.Extends` (string) names the preset. `PolicyResolver.Resolve()` expands
preset extensions before merging: the preset is inserted as a low-priority document
before the user policy, so user rules always override preset defaults.

### Compliance Controls

`CompliancePresetLoader.Check(presetName, resolvedSpec)` returns a list of
`ComplianceControl` objects with `Id`, `Description`, `Pass`, and `Remediation`.
Controls are specific per-framework (SOC2-AU-1, GDPR-RT-1, etc.).

### CLI

```text
jdai policy compliance list
jdai policy compliance check --profile soc2
jdai policy compliance check --profile gdpr --policy-dir ./policies
```

## Consequences

### Positive

- Policy YAML files can now declare `spec.extends: jdai/compliance/hipaa` and
  automatically inherit all HIPAA controls without repeating patterns.
- Audit events can be labeled with classification names for structured reporting.
- `DenyAndAudit` enables the system to block requests containing cardholder data
  or PHI before they reach any LLM provider.
- `jdai policy compliance check` provides instant PASS/FAIL feedback for any
  registered policy directory.

### Negative / Trade-offs

- `DataRedactor.Redact()` applies classifications inline (for backward compat),
  but does not surface `DenyAndAudit` signals — callers must use
  `RedactWithClassifications()` to detect deny conditions.
- Classification patterns are still regex (inheriting ReDoS risk); mitigated by
  the existing `TimeSpan.FromSeconds(1)` timeout per pattern.
- Preset controls are opinionated; organizations should review them and add
  org-specific overrides rather than treating them as exhaustive.

## Related

- ADR-003: RBAC Policy Evaluation and Scope Merge
- ADR-004: SQLite Audit Sink
- ADR-005: IApprovalService
- Issue #212 — Compliance Profiles + Data Classification
