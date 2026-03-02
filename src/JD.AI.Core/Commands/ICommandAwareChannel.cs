namespace JD.AI.Core.Commands;

/// <summary>
/// Extended channel interface for channels that support native command registration
/// (e.g., Discord slash commands, Slack /commands).
/// </summary>
public interface ICommandAwareChannel
{
    /// <summary>
    /// Registers commands from the registry as platform-native commands.
    /// Called after ConnectAsync when commands are available.
    /// </summary>
    Task RegisterCommandsAsync(ICommandRegistry registry, CancellationToken ct = default);
}
