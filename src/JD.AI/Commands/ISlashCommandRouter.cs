namespace JD.AI.Tui.Commands;

/// <summary>
/// Handles slash command routing.
/// </summary>
public interface ISlashCommandRouter
{
    /// <summary>Returns true if the input starts with /.</summary>
    bool IsSlashCommand(string input);

    /// <summary>Execute a slash command and return the output text.</summary>
    Task<string?> ExecuteAsync(string input, CancellationToken ct = default);
}
