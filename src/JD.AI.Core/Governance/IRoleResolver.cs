namespace JD.AI.Core.Governance;

/// <summary>
/// Resolves the role and group memberships for the current user context.
/// </summary>
public interface IRoleResolver
{
    /// <summary>
    /// Returns the primary role name for the given user ID, or <c>null</c> if none.
    /// </summary>
    string? ResolveRole(string? userId);

    /// <summary>
    /// Returns the group memberships for the given user ID.
    /// </summary>
    IReadOnlyList<string> ResolveGroups(string? userId);
}
