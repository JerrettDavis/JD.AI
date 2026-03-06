namespace JD.AI.Core.Governance;

/// <summary>
/// Action to take when a data classification pattern matches content.
/// </summary>
public enum ClassificationAction
{
    /// <summary>Replace matched content with [REDACTED:{name}] and continue.</summary>
    Redact,

    /// <summary>Redact matched content and emit an audit event.</summary>
    RedactAndAudit,

    /// <summary>Emit an audit event but allow the content through.</summary>
    AuditOnly,

    /// <summary>Block the entire request and emit an audit event.</summary>
    DenyAndAudit,
}

/// <summary>
/// A named data classification with matching patterns and an enforcement action.
/// </summary>
public sealed class DataClassification
{
    /// <summary>Classification label (e.g. "PCI-DSS", "PHI", "PII").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Regular expressions identifying this classification of data.
    /// Each pattern is OR-combined.
    /// </summary>
    public IList<string> Patterns { get; set; } = [];

    /// <summary>Action to apply when a match is detected.</summary>
    public ClassificationAction Action { get; set; } = ClassificationAction.Redact;

    /// <summary>
    /// Provider names that are denied when this classification is detected.
    /// Use <c>["*"]</c> to deny all external providers.
    /// Applicable only when <see cref="Action"/> is <see cref="ClassificationAction.DenyAndAudit"/>.
    /// </summary>
    public IList<string> DenyProviders { get; set; } = [];
}

/// <summary>
/// Describes content detected by a <see cref="DataClassification"/> rule during redaction.
/// </summary>
public sealed class ClassificationMatch(string classificationName, ClassificationAction action)
{
    /// <summary>Name of the classification that matched.</summary>
    public string ClassificationName { get; } = classificationName;

    /// <summary>Action applied to the matched content.</summary>
    public ClassificationAction Action { get; } = action;
}

/// <summary>
/// Result of running <see cref="DataRedactor.RedactWithClassifications"/> on a piece of content.
/// </summary>
public sealed class RedactionResult(string content, IReadOnlyList<ClassificationMatch> matches)
{
    /// <summary>Content after redaction (may equal input if action is <see cref="ClassificationAction.AuditOnly"/>).</summary>
    public string Content { get; } = content;

    /// <summary>All classification matches found during processing.</summary>
    public IReadOnlyList<ClassificationMatch> Matches { get; } = matches;

    /// <summary>True if any classification with <see cref="ClassificationAction.DenyAndAudit"/> fired.</summary>
    public bool ShouldDeny => Matches.Any(m => m.Action == ClassificationAction.DenyAndAudit);

    /// <summary>True if any match occurred.</summary>
    public bool HasMatches => Matches.Count > 0;
}
