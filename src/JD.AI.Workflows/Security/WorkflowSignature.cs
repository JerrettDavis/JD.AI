using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Workflows.Security;

/// <summary>
/// Signs and verifies workflow definitions using HMAC-SHA256.
/// Signatures cover the canonical JSON representation of the workflow,
/// ensuring that any modification (steps, metadata, version) is detected.
/// </summary>
public static class WorkflowSignature
{
    private static readonly JsonSerializerOptions CanonicalOptions = new(JsonDefaults.Options)
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Computes an HMAC-SHA256 signature of the workflow definition.
    /// </summary>
    /// <returns>Lowercase hex signature string.</returns>
    public static string Sign(AgentWorkflowDefinition definition, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(key);

        var canonical = Canonicalize(definition);
        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Verifies that a signature matches the workflow definition.
    /// </summary>
    public static bool Verify(AgentWorkflowDefinition definition, byte[] key, string signature)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(signature);

        var expected = Sign(definition, key);
        return string.Equals(expected, signature, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Produces a canonical JSON representation of the workflow for signing.
    /// Excludes volatile fields (CreatedAt, UpdatedAt) so signatures are stable.
    /// </summary>
    internal static string Canonicalize(AgentWorkflowDefinition definition)
    {
        var signable = new
        {
            definition.Name,
            definition.Version,
            definition.Description,
            definition.IsDeprecated,
            definition.MigrationGuidance,
            definition.BreakingChanges,
            definition.Tags,
            Steps = CanonicalizeSteps(definition.Steps),
        };

        return JsonSerializer.Serialize(signable, CanonicalOptions);
    }

    private static object[] CanonicalizeSteps(IList<AgentStepDefinition> steps)
    {
        return steps.Select(s => (object)new
        {
            s.Name,
            Kind = s.Kind.ToString(),
            s.Target,
            s.Condition,
            s.AllowedPlugins,
            SubSteps = CanonicalizeSteps(s.SubSteps),
        }).ToArray();
    }
}
