using System.Text;
using JD.AI.Core.Commands;

namespace JD.AI.Gateway.Commands;

/// <summary>Lists all available commands and their usage.</summary>
public sealed class HelpCommand(ICommandRegistry registry) : IChannelCommand
{
    public string Name => "help";
    public string Description => "Lists all available JD.AI commands and their usage.";
    public IReadOnlyList<CommandParameter> Parameters => [];

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**JD.AI Commands**");
        sb.AppendLine();

        foreach (var cmd in registry.Commands.OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            sb.Append($"• **jdai-{cmd.Name}**");
            if (cmd.Parameters.Count > 0)
            {
                var paramList = string.Join(' ', cmd.Parameters.Select(p =>
                    p.IsRequired ? $"<{p.Name}>" : $"[{p.Name}]"));
                sb.Append($" {paramList}");
            }
            sb.AppendLine($" — {cmd.Description}");
        }

        return Task.FromResult(new CommandResult { Success = true, Content = sb.ToString() });
    }
}
