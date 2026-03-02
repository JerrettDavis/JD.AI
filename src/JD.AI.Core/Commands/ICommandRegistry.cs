namespace JD.AI.Core.Commands;

/// <summary>
/// Registry of all available channel commands.
/// Channel adapters query this to register platform-native commands.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>All registered commands.</summary>
    IReadOnlyList<IChannelCommand> Commands { get; }

    /// <summary>Register a command.</summary>
    void Register(IChannelCommand command);

    /// <summary>Look up a command by name (case-insensitive).</summary>
    IChannelCommand? GetCommand(string name);
}

/// <summary>
/// Thread-safe in-memory command registry.
/// </summary>
public sealed class CommandRegistry : ICommandRegistry
{
    private readonly List<IChannelCommand> _commands = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<IChannelCommand> Commands
    {
        get { lock (_lock) return _commands.ToList().AsReadOnly(); }
    }

    public void Register(IChannelCommand command)
    {
        lock (_lock)
        {
            _commands.RemoveAll(c => string.Equals(c.Name, command.Name, StringComparison.OrdinalIgnoreCase));
            _commands.Add(command);
        }
    }

    public IChannelCommand? GetCommand(string name)
    {
        lock (_lock)
            return _commands.FirstOrDefault(c =>
                string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
