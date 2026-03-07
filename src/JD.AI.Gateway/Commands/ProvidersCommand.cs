using System.Text;
using JD.AI.Core.Commands;
using JD.AI.Core.Providers;

namespace JD.AI.Gateway.Commands;

/// <summary>Lists all detected AI providers and their availability.</summary>
public sealed class ProvidersCommand(IProviderRegistry registry) : IChannelCommand
{
    public string Name => "providers";
    public string Description => "Lists all detected AI providers and their status.";
    public IReadOnlyList<CommandParameter> Parameters => [];

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var providers = await registry.DetectProvidersAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("**AI Providers**");
        sb.AppendLine();

        if (providers.Count == 0)
        {
            sb.AppendLine("No providers detected.");
        }
        else
        {
            foreach (var provider in providers)
            {
                var status = provider.IsAvailable ? "🟢 Online" : "🔴 Offline";
                sb.AppendLine($"{status} **{provider.Name}** — {provider.Models.Count} model(s)");

                if (provider.Models.Count > 0)
                {
                    foreach (var model in provider.Models)
                    {
                        sb.AppendLine($"   • `{model.DisplayName}`");
                    }
                }

                if (!string.IsNullOrEmpty(provider.StatusMessage))
                {
                    sb.AppendLine($"   _{provider.StatusMessage}_");
                }

                sb.AppendLine();
            }
        }

        return new CommandResult { Success = true, Content = sb.ToString() };
    }
}
