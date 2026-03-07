// Licensed under the MIT License.

using JD.AI.Core.Tools;

namespace JD.AI.Core.Attributes;

/// <summary>
/// Declares the safety tier for a <c>[KernelFunction]</c> method. This attribute
/// replaces the hardcoded dictionary in <c>ToolConfirmationFilter</c> by co-locating
/// safety classification with the tool definition.
/// </summary>
/// <example>
/// <code>
/// [KernelFunction("read_file")]
/// [ToolSafetyTier(SafetyTier.AutoApprove)]
/// [Description("Read file contents")]
/// public static string ReadFile([Description("Path")] string path) { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ToolSafetyTierAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="ToolSafetyTierAttribute"/> with the specified tier.
    /// </summary>
    /// <param name="tier">The safety tier classification for this tool method.</param>
    public ToolSafetyTierAttribute(SafetyTier tier)
    {
        Tier = tier;
    }

    /// <summary>Gets the safety tier classification.</summary>
    public SafetyTier Tier { get; }

    /// <summary>
    /// Gets or sets an optional reason for the tier classification,
    /// useful for documentation and audit purposes.
    /// </summary>
    public string? Reason { get; set; }
}
