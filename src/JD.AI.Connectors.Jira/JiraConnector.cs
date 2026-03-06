using JD.AI.Connectors.Sdk;

namespace JD.AI.Connectors.Jira;

/// <summary>
/// Reference connector for Atlassian Jira. Demonstrates the connector authoring pattern.
/// Provides issue search, retrieval, and creation tools.
/// </summary>
[JdAiConnector(
    name: "jira",
    displayName: "Atlassian Jira",
    version: "1.0.0",
    Description = "Tools for searching, reading, and creating Jira issues and sprints.",
    Homepage = "https://developer.atlassian.com/cloud/jira/platform/rest/v3/")]
public sealed class JiraConnector : IConnector
{
    /// <inheritdoc/>
    public void Configure(IConnectorBuilder builder)
    {
        builder
            .AddAuthentication<JiraApiKeyAuthProvider>()
            .AddToolPlugin<JiraIssueTool>()
            .AddLoadout("jira-readonly", toolName => toolName.Contains("search", StringComparison.OrdinalIgnoreCase)
                                                      || toolName.Contains("get", StringComparison.OrdinalIgnoreCase));
    }
}
