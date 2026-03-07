namespace JD.AI.Core.Commands;

/// <summary>
/// Defines a single command that can be invoked from any channel.
/// </summary>
public interface IChannelCommand
{
    /// <summary>Command name without prefix (e.g., "help", "usage").</summary>
    string Name { get; }

    /// <summary>Short description shown in help text and slash command metadata.</summary>
    string Description { get; }

    /// <summary>Optional parameters this command accepts.</summary>
    IReadOnlyList<CommandParameter> Parameters { get; }

    /// <summary>Execute the command and return a result.</summary>
    Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default);
}

/// <summary>
/// A parameter definition for a channel command.
/// </summary>
public record CommandParameter
{
    /// <summary>Parameter name (e.g., "model").</summary>
    public required string Name { get; init; }

    /// <summary>Parameter description for help text.</summary>
    public required string Description { get; init; }

    /// <summary>Whether this parameter is required.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Parameter value type.</summary>
    public CommandParameterType Type { get; init; } = CommandParameterType.Text;

    /// <summary>Pre-defined choices (for autocomplete/dropdowns).</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];
}

/// <summary>
/// Supported parameter types for command arguments.
/// </summary>
public enum CommandParameterType
{
    Text,
    Number,
    Boolean
}

/// <summary>
/// Context provided when a command is invoked.
/// </summary>
public record CommandContext
{
    /// <summary>The command name that was invoked.</summary>
    public required string CommandName { get; init; }

    /// <summary>ID of the user who invoked the command.</summary>
    public required string InvokerId { get; init; }

    /// <summary>Display name of the invoker (if available).</summary>
    public string? InvokerDisplayName { get; init; }

    /// <summary>Channel/conversation ID where the command was invoked.</summary>
    public required string ChannelId { get; init; }

    /// <summary>The channel type (e.g., "discord", "signal", "slack").</summary>
    public required string ChannelType { get; init; }

    /// <summary>Parsed arguments keyed by parameter name.</summary>
    public IReadOnlyDictionary<string, string> Arguments { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>
/// Result returned by a command execution.
/// </summary>
public record CommandResult
{
    /// <summary>Whether the command succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Response content to send back to the invoker.</summary>
    public required string Content { get; init; }

    /// <summary>
    /// Whether the response should be ephemeral (visible only to invoker).
    /// Supported on Discord (ephemeral responses) and Slack (response_type: ephemeral).
    /// </summary>
    public bool Ephemeral { get; init; } = true;
}
