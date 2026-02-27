using JD.SemanticKernel.Connectors.GitHubCopilot;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Providers;

/// <summary>
/// Detects a local GitHub Copilot session and enumerates its models.
/// </summary>
public sealed class CopilotDetector : IProviderDetector
{
    public string ProviderName => "GitHub Copilot";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        try
        {
            var provider = new CopilotSessionProvider(
                Options.Create(new CopilotSessionOptions()),
                NullLogger<CopilotSessionProvider>.Instance);

            var isAuth = await provider.IsAuthenticatedAsync(ct).ConfigureAwait(false);

            if (!isAuth)
            {
                return new ProviderInfo(
                    ProviderName,
                    IsAvailable: false,
                    StatusMessage: "Not authenticated",
                    Models: []);
            }

            // Try model discovery; fall back to well-known models
            var models = new List<ProviderModelInfo>();
            try
            {
                var discovery = new CopilotModelDiscovery(
                    provider, new HttpClient(),
                    NullLogger<CopilotModelDiscovery>.Instance);
                var discovered = await discovery.DiscoverModelsAsync(ct).ConfigureAwait(false);
                models.AddRange(discovered.Select(m =>
                    new ProviderModelInfo(m.Id, m.Name ?? m.Id, ProviderName)));
            }
#pragma warning disable CA1031 // catch broad — discovery is optional
            catch
#pragma warning restore CA1031
            {
                // Fall back to well-known models
                models.AddRange(new[]
                {
                    new ProviderModelInfo(CopilotModels.Default, "Claude Sonnet 4.6", ProviderName),
                    new ProviderModelInfo(CopilotModels.Gpt4o, "GPT-4o", ProviderName),
                    new ProviderModelInfo(CopilotModels.ClaudeOpus46, "Claude Opus 4.6", ProviderName),
                });
            }

            return new ProviderInfo(
                ProviderName,
                IsAvailable: true,
                StatusMessage: $"Authenticated — {models.Count} model(s)",
                Models: models);
        }
        catch (CopilotSessionException ex)
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
        builder.UseCopilotChatCompletion(modelId: model.Id);
        return builder.Build();
    }
}
