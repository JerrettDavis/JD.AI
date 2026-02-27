using JD.SemanticKernel.Connectors.ClaudeCode;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Providers;

/// <summary>
/// Detects a local Claude Code session and exposes its models.
/// </summary>
public sealed class ClaudeCodeDetector : IProviderDetector
{
    public string ProviderName => "Claude Code";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        try
        {
            var provider = new ClaudeCodeSessionProvider(
                Options.Create(new ClaudeCodeSessionOptions()),
                NullLogger<ClaudeCodeSessionProvider>.Instance);

            var isAuth = await provider.IsAuthenticatedAsync(ct).ConfigureAwait(false);

            if (!isAuth)
            {
                return new ProviderInfo(
                    ProviderName,
                    IsAvailable: false,
                    StatusMessage: "Not authenticated",
                    Models: []);
            }

            var models = new List<ProviderModelInfo>
            {
                new(ClaudeModels.Opus, "Claude Opus 4.6", ProviderName),
                new(ClaudeModels.Sonnet, "Claude Sonnet 4.6", ProviderName),
                new(ClaudeModels.Haiku, "Claude Haiku 4.5", ProviderName),
            };

            return new ProviderInfo(
                ProviderName,
                IsAvailable: true,
                StatusMessage: "Authenticated",
                Models: models);
        }
        catch (ClaudeCodeSessionException ex)
        {
            return new ProviderInfo(
                ProviderName,
                IsAvailable: false,
                StatusMessage: ex.Message,
                Models: []);
        }
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var builder = Kernel.CreateBuilder();
        builder.UseClaudeCodeChatCompletion(modelId: model.Id);
        return builder.Build();
    }
}
