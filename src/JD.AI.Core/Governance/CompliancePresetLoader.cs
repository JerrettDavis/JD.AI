using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Governance;

/// <summary>
/// Loads and resolves built-in compliance preset policies embedded in the assembly.
/// </summary>
/// <remarks>
/// Built-in presets:
/// <list type="bullet">
///   <item><c>jdai/compliance/soc2</c> — SOC 2 Type II baseline</item>
///   <item><c>jdai/compliance/gdpr</c> — GDPR PII protection</item>
///   <item><c>jdai/compliance/hipaa</c> — HIPAA PHI protection</item>
///   <item><c>jdai/compliance/pci-dss</c> — PCI-DSS cardholder data protection</item>
/// </list>
/// </remarks>
public static class CompliancePresetLoader
{
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    // Map from preset name to embedded resource manifest name (suffix of resource)
    private static readonly Dictionary<string, string> _presetMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jdai/compliance/soc2"] = "soc2.yaml",
        ["jdai/compliance/gdpr"] = "gdpr.yaml",
        ["jdai/compliance/hipaa"] = "hipaa.yaml",
        ["jdai/compliance/pci-dss"] = "pci-dss.yaml",
    };

    /// <summary>Returns all available built-in preset names.</summary>
    public static IReadOnlyCollection<string> AvailablePresets => _presetMap.Keys;

    /// <summary>
    /// Loads the preset with the given name.
    /// Returns <c>null</c> if the preset is not recognized.
    /// </summary>
    public static PolicyDocument? LoadPreset(string name)
    {
        if (!_presetMap.TryGetValue(name, out var resourceSuffix))
            return null;

        var asm = typeof(CompliancePresetLoader).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null) return null;

        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null) return null;

        using var reader = new StreamReader(stream);
        var yaml = reader.ReadToEnd();
        return _deserializer.Deserialize<PolicyDocument>(yaml);
    }

    /// <summary>
    /// If <paramref name="spec"/> has a non-empty <see cref="PolicySpec.Extends"/> field,
    /// loads the named preset and returns it as a low-priority <see cref="PolicyDocument"/>.
    /// Returns <c>null</c> if <c>Extends</c> is empty or the preset is not found.
    /// </summary>
    public static PolicyDocument? ResolveExtension(PolicySpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.Extends))
            return null;

        return LoadPreset(spec.Extends);
    }

    /// <summary>
    /// Checks whether a compliance preset report passes for the given resolved policy.
    /// Returns a list of control results (name → pass/fail and optional guidance).
    /// </summary>
    public static IReadOnlyList<ComplianceControl> Check(string presetName, PolicySpec resolvedSpec)
    {
        return presetName.ToLowerInvariant() switch
        {
            "jdai/compliance/soc2" => CheckSoc2(resolvedSpec),
            "jdai/compliance/gdpr" => CheckGdpr(resolvedSpec),
            "jdai/compliance/hipaa" => CheckHipaa(resolvedSpec),
            "jdai/compliance/pci-dss" => CheckPciDss(resolvedSpec),
            _ => [],
        };
    }

    // ── SOC 2 controls ────────────────────────────────────────────────────

    private static IReadOnlyList<ComplianceControl> CheckSoc2(PolicySpec spec)
    {
        return
        [
            Control("SOC2-AU-1", "Audit logging is enabled",
                spec.Audit?.Enabled == true,
                "Enable audit logging: set audit.enabled: true"),

            Control("SOC2-AU-2", "Session retention configured (≥ 365 days)",
                spec.Sessions?.RetentionDays >= 365,
                "Set sessions.retentionDays to at least 365"),

            Control("SOC2-DP-1", "API key / secret detection configured",
                spec.Data?.Classifications?.Any(c =>
                    c.Name.Equals("APIKey", StringComparison.OrdinalIgnoreCase)) == true,
                "Add an APIKey classification with DenyAndAudit action"),

            Control("SOC2-DP-2", "Credit card data detection configured",
                spec.Data?.Classifications?.Any(c =>
                    c.Name.Equals("CreditCard", StringComparison.OrdinalIgnoreCase)) == true,
                "Add a CreditCard classification"),
        ];
    }

    // ── GDPR controls ─────────────────────────────────────────────────────

    private static IReadOnlyList<ComplianceControl> CheckGdpr(PolicySpec spec)
    {
        return
        [
            Control("GDPR-RT-1", "Session retention ≤ 90 days",
                spec.Sessions?.RetentionDays is > 0 and <= 90,
                "Set sessions.retentionDays to 90 or less for GDPR data minimization"),

            Control("GDPR-AU-1", "Audit logging enabled",
                spec.Audit?.Enabled == true,
                "Enable audit logging for GDPR accountability"),

            Control("GDPR-DP-1", "PII email detection configured",
                spec.Data?.Classifications?.Any(c =>
                    c.Name.Contains("PII", StringComparison.OrdinalIgnoreCase) ||
                    c.Name.Contains("Email", StringComparison.OrdinalIgnoreCase)) == true,
                "Add PII email classification with RedactAndAudit"),

            Control("GDPR-DP-2", "External provider restrictions active",
                spec.Data?.NoExternalProviders?.Any() == true,
                "Restrict external providers under data.noExternalProviders"),
        ];
    }

    // ── HIPAA controls ────────────────────────────────────────────────────

    private static IReadOnlyList<ComplianceControl> CheckHipaa(PolicySpec spec)
    {
        return
        [
            Control("HIPAA-AU-1", "Audit trail enabled",
                spec.Audit?.Enabled == true,
                "Enable audit logging — HIPAA mandates audit controls"),

            Control("HIPAA-DP-1", "SSN / PHI detection with deny action",
                spec.Data?.Classifications?.Any(c =>
                    c.Action == ClassificationAction.DenyAndAudit &&
                    (c.Name.Contains("PHI", StringComparison.OrdinalIgnoreCase) ||
                     c.Name.Contains("SSN", StringComparison.OrdinalIgnoreCase))) == true,
                "Add PHI/SSN classification with DenyAndAudit action"),

            Control("HIPAA-DP-2", "All external providers restricted",
                spec.Data?.NoExternalProviders?.Contains("*") == true,
                "Set data.noExternalProviders: ['*'] — no cloud LLMs for PHI without BAA"),

            Control("HIPAA-RT-1", "Session retention ≥ 7 years (2555 days)",
                spec.Sessions?.RetentionDays >= 2555,
                "Set sessions.retentionDays to at least 2555 (7 years)"),
        ];
    }

    // ── PCI-DSS controls ──────────────────────────────────────────────────

    private static IReadOnlyList<ComplianceControl> CheckPciDss(PolicySpec spec)
    {
        return
        [
            Control("PCI-AU-1", "Audit logging enabled",
                spec.Audit?.Enabled == true,
                "Enable audit logging — PCI DSS Requirement 10"),

            Control("PCI-DP-1", "PAN (card number) detection with deny action",
                spec.Data?.Classifications?.Any(c =>
                    c.Action == ClassificationAction.DenyAndAudit &&
                    c.Name.Equals("PAN", StringComparison.OrdinalIgnoreCase)) == true,
                "Add PAN classification with DenyAndAudit action"),

            Control("PCI-DP-2", "CVV detection configured",
                spec.Data?.Classifications?.Any(c =>
                    c.Name.Equals("CVV", StringComparison.OrdinalIgnoreCase)) == true,
                "Add CVV classification"),

            Control("PCI-DP-3", "All external providers restricted",
                spec.Data?.NoExternalProviders?.Contains("*") == true,
                "Restrict all external providers for cardholder data"),

            Control("PCI-RT-1", "Session retention ≥ 1 year",
                spec.Sessions?.RetentionDays >= 365,
                "Set sessions.retentionDays to at least 365"),
        ];
    }

    private static ComplianceControl Control(
        string id, string description, bool pass, string remediation) =>
        new(id, description, pass, pass ? null : remediation);
}

/// <summary>
/// Result of a single compliance control check.
/// </summary>
public sealed class ComplianceControl(
    string id,
    string description,
    bool pass,
    string? remediation)
{
    /// <summary>Control identifier (e.g. "SOC2-AU-1").</summary>
    public string Id { get; } = id;

    /// <summary>Human-readable description of the control.</summary>
    public string Description { get; } = description;

    /// <summary>True if the control passes for the evaluated policy.</summary>
    public bool Pass { get; } = pass;

    /// <summary>Remediation guidance if the control fails; null if passing.</summary>
    public string? Remediation { get; } = remediation;
}
