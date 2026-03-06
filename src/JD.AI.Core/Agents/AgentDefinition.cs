namespace JD.AI.Core.Agents;

/// <summary>
/// Describes a fully configured agent as a serializable, versionable artifact.
/// An agent definition captures everything needed to reproduce an agent's behavior
/// across environments: model selection, tool loadout, system prompt, and workflow chain.
/// </summary>
/// <remarks>
/// YAML format example:
/// <code>
/// name: pr-reviewer
/// displayName: PR Reviewer Agent
/// description: Reviews pull requests for code quality and security
/// version: "1.0"
/// model:
///   provider: ClaudeCode
///   id: claude-opus-4
///   maxOutputTokens: 8096
/// loadout: developer
/// systemPrompt: |
///   You are a code reviewer focused on...
/// workflows:
///   - pr-review-workflow
/// tags:
///   - code-review
/// </code>
/// </remarks>
public sealed class AgentDefinition
{
    /// <summary>Unique identifier for this agent (kebab-case, e.g. "pr-reviewer").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-readable display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Description of this agent's purpose and capabilities.</summary>
    public string? Description { get; set; }

    /// <summary>Semantic version string (e.g. "1.0", "2.3.1").</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Model selection for this agent.</summary>
    public AgentModelSpec? Model { get; set; }

    /// <summary>
    /// Name of the tool loadout to apply (must be registered in <c>IToolLoadoutRegistry</c>).
    /// Defaults to the session's active loadout if not specified.
    /// </summary>
    public string? Loadout { get; set; }

    /// <summary>System prompt override. Supports multi-line YAML block scalars.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Named workflows this agent can trigger (references <c>AgentWorkflowDefinition.Name</c>).
    /// </summary>
    public IList<string> Workflows { get; init; } = [];

    /// <summary>Classification and discovery tags.</summary>
    public IList<string> Tags { get; init; } = [];

    /// <summary>Whether this definition is deprecated and should not be used for new sessions.</summary>
    public bool IsDeprecated { get; set; }

    /// <summary>Guidance for migrating to a newer version.</summary>
    public string? MigrationGuidance { get; set; }

    /// <summary>Environment this definition is intended for (e.g. "development", "production").</summary>
    public string? Environment { get; set; }

    /// <summary>Timestamp the definition was first authored.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp the definition was last modified.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model selection within an <see cref="AgentDefinition"/>.
/// </summary>
public sealed class AgentModelSpec
{
    /// <summary>Provider name (e.g. "ClaudeCode", "OpenAI", "Ollama").</summary>
    public string? Provider { get; set; }

    /// <summary>Model identifier within the provider (e.g. "claude-opus-4", "gpt-4o").</summary>
    public string? Id { get; set; }

    /// <summary>Maximum output tokens override.</summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>Temperature override (0.0–2.0).</summary>
    public double? Temperature { get; set; }
}
