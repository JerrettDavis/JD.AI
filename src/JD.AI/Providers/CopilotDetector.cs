using JD.SemanticKernel.Connectors.GitHubCopilot;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Spectre.Console;

namespace JD.AI.Tui.Providers;

/// <summary>
/// Detects a local GitHub Copilot session and enumerates its models.
/// When authentication fails, attempts a silent refresh via the <c>gh</c> CLI.
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
                // Token exchange may have failed — try refreshing via gh CLI
                var refreshed = await TryRefreshAuthAsync(ct).ConfigureAwait(false);
                if (refreshed)
                {
                    // Re-create provider to pick up refreshed credentials
                    provider.Dispose();
                    provider = new CopilotSessionProvider(
                        Options.Create(new CopilotSessionOptions()),
                        NullLogger<CopilotSessionProvider>.Instance);
                    isAuth = await provider.IsAuthenticatedAsync(ct).ConfigureAwait(false);
                }

                if (!isAuth)
                {
                    provider.Dispose();
                    return new ProviderInfo(
                        ProviderName,
                        IsAvailable: false,
                        StatusMessage: "Not authenticated — run 'gh auth login' to sign in",
                        Models: []);
                }
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

            provider.Dispose();

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

    /// <summary>
    /// Attempts to refresh GitHub auth by invoking <c>gh auth status</c>.
    /// This triggers token validation and may refresh expired tokens
    /// when the underlying OAuth grant is still valid.
    /// </summary>
    private static async Task<bool> TryRefreshAuthAsync(CancellationToken ct)
    {
        try
        {
            var ghPath = ClaudeCodeDetector.FindCli("gh");
            if (ghPath is null) return false;

            AnsiConsole.MarkupLine("[dim]  ↻ Attempting GitHub Copilot auth refresh...[/]");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ghPath,
                Arguments = "auth status",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return false;

            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            return proc.ExitCode == 0;
        }
#pragma warning disable CA1031 // best-effort refresh
        catch { return false; }
#pragma warning restore CA1031
    }
}
