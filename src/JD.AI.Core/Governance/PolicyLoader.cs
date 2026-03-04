using JD.AI.Core.Config;

namespace JD.AI.Core.Governance;

/// <summary>
/// Loads <see cref="PolicyDocument"/> instances from the filesystem hierarchy.
/// </summary>
public static class PolicyLoader
{
    private const string PoliciesSubDir = "policies";

    /// <summary>
    /// Loads all policy documents from the standard locations:
    /// <list type="number">
    ///   <item><c>~/.jdai/policies/</c> (Global/User scope)</item>
    ///   <item><c>{projectPath}/.jdai/policies/</c> (Project scope, when a project path is provided)</item>
    /// </list>
    /// </summary>
    /// <param name="projectPath">
    /// Optional path to the current project directory.  When provided, the
    /// <c>{projectPath}/.jdai/policies/</c> directory is also scanned.
    /// </param>
    /// <returns>
    /// All successfully parsed <see cref="PolicyDocument"/> instances, ordered by scope.
    /// </returns>
    public static IReadOnlyList<PolicyDocument> Load(string? projectPath = null)
    {
        var documents = new List<PolicyDocument>();

        // Global user policies: ~/.jdai/policies/
        var globalPoliciesDir = Path.Combine(DataDirectories.Root, PoliciesSubDir);
        documents.AddRange(PolicyParser.ParseDirectory(globalPoliciesDir));

        // Project-level policies: {project}/.jdai/policies/
        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            var projectPoliciesDir = Path.Combine(projectPath, ".jdai", PoliciesSubDir);
            var projectDocs = PolicyParser.ParseDirectory(projectPoliciesDir)
                .Select(d =>
                {
                    // Ensure project-level policies have Project scope if not explicitly set
                    if (d.Metadata.Scope == PolicyScope.User)
                        d.Metadata.Scope = PolicyScope.Project;
                    return d;
                });

            documents.AddRange(projectDocs);
        }

        // Sort by scope then priority
        return documents
            .OrderBy(d => d.Metadata.Scope)
            .ThenBy(d => d.Metadata.Priority)
            .ToList()
            .AsReadOnly();
    }
}
