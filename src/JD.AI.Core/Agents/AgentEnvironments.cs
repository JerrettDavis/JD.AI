namespace JD.AI.Core.Agents;

/// <summary>
/// Describes which environment scope an agent definition belongs to.
/// Environments form a promotion pipeline: Dev → Staging → Production.
/// </summary>
public static class AgentEnvironments
{
    public const string Dev = "dev";
    public const string Staging = "staging";
    public const string Prod = "prod";

    /// <summary>Ordered environments from lowest to highest.</summary>
    public static readonly IReadOnlyList<string> All = [Dev, Staging, Prod];

    /// <summary>Returns whether the supplied environment is one of the supported values.</summary>
    public static bool IsKnown(string? env) =>
        env is not null && All.Any(candidate => candidate.Equals(env, StringComparison.OrdinalIgnoreCase));

    /// <summary>Normalizes a supported environment name to its canonical lowercase form.</summary>
    public static string Normalize(string env, string paramName = "environment")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(env, paramName);

        foreach (var candidate in All)
        {
            if (candidate.Equals(env, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        throw new ArgumentException(
            $"Environment must be one of: {string.Join(", ", All)}.",
            paramName);
    }

    /// <summary>Returns the next environment in the promotion chain, or null.</summary>
    public static string? NextAfter(string env) =>
        env.Equals(Dev, StringComparison.OrdinalIgnoreCase) ? Staging :
        env.Equals(Staging, StringComparison.OrdinalIgnoreCase) ? Prod : null;
}
