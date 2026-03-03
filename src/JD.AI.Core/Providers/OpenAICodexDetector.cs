using JD.SemanticKernel.Connectors.OpenAICodex;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects a local OpenAI Codex session and exposes its models.
/// When running as a Windows service, scans user profiles for credentials.
/// Supports automatic token refresh and device code login.
/// </summary>
public sealed class OpenAICodexDetector : IProviderDetector
{
    public string ProviderName => "OpenAI Codex";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        try
        {
            var options = BuildSessionOptions();
            using var provider = new CodexSessionProvider(
                Options.Create(options),
                NullLogger<CodexSessionProvider>.Instance);

            var isAuth = await provider.IsAuthenticatedAsync(ct).ConfigureAwait(false);

            if (!isAuth)
            {
                return new ProviderInfo(
                    ProviderName,
                    IsAvailable: false,
                    StatusMessage: "Not authenticated — set OPENAI_API_KEY or run 'codex login'",
                    Models: []);
            }

            // Use model discovery to enumerate available models
            var models = new List<ProviderModelInfo>();
            try
            {
                var discovery = new CodexModelDiscovery();
                var discovered = await discovery.DiscoverModelsAsync(ct).ConfigureAwait(false);
                models.AddRange(discovered.Select(m =>
                    new ProviderModelInfo(m.Id, m.Name ?? m.Id, ProviderName)));
            }
#pragma warning disable CA1031 // catch broad — discovery is optional
            catch
#pragma warning restore CA1031
            {
                // Fall back to well-known models
                models.AddRange(
                [
                    new ProviderModelInfo(CodexModels.O3, "o3", ProviderName),
                    new ProviderModelInfo(CodexModels.O4Mini, "o4-mini", ProviderName),
                    new ProviderModelInfo(CodexModels.CodexMini, "codex-mini", ProviderName),
                    new ProviderModelInfo(CodexModels.Gpt4Point1, "GPT-4.1", ProviderName),
                    new ProviderModelInfo(CodexModels.Gpt4Point1Mini, "GPT-4.1-mini", ProviderName),
                    new ProviderModelInfo(CodexModels.Gpt4Point1Nano, "GPT-4.1-nano", ProviderName),
                ]);
            }

            return new ProviderInfo(
                ProviderName,
                IsAvailable: true,
                StatusMessage: $"Authenticated — {models.Count} model(s)",
                Models: models);
        }
        catch (CodexSessionException ex)
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
        var options = BuildSessionOptions();
        var builder = Kernel.CreateBuilder();
        builder.UseCodexChatCompletion(
            modelId: model.Id,
            configure: opts => opts.CredentialsPath = options.CredentialsPath);
        return builder.Build();
    }

    /// <summary>
    /// Builds session options, scanning user profiles for credentials when
    /// running as a service account (LocalSystem, NetworkService, etc.).
    /// </summary>
    private static CodexSessionOptions BuildSessionOptions()
    {
        var options = new CodexSessionOptions();

        // Check if the default path would resolve to a service account home
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) && !UserProfileScanner.IsServiceAccount(home))
            return options;

        // Scan real user profiles for Codex credentials
        var credPath = UserProfileScanner.FindInUserProfiles(
            Path.Combine(".codex", "auth.json"));
        if (credPath is not null)
            options.CredentialsPath = credPath;

        return options;
    }
}
