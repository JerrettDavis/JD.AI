using System.Reflection;
using JD.AI.Core.Infrastructure;
using JD.SemanticKernel.Connectors.GitHubCopilot;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
namespace JD.AI.Core.Providers;

/// <summary>
/// Detects a local GitHub Copilot session and enumerates its models.
/// When running as a Windows service, scans user profiles for credentials.
/// When authentication fails, attempts a silent refresh via the <c>gh</c> CLI.
/// </summary>
public sealed class CopilotDetector : IProviderDetector
{
    private const string CopilotProviderName = "GitHub Copilot";

    public string ProviderName => CopilotProviderName;

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        try
        {
            var options = BuildSessionOptions();
            var provider = new CopilotSessionProvider(
                Options.Create(options),
                NullLogger<CopilotSessionProvider>.Instance);

            var isAuth = await provider.IsAuthenticatedAsync(ct).ConfigureAwait(false);

            if (!isAuth)
            {
                // Token exchange may have failed — try refreshing via gh CLI
                var refreshed = await TryRefreshAuthAsync(ct).ConfigureAwait(false);
                if (refreshed)
                {
                    provider.Dispose();
                    provider = new CopilotSessionProvider(
                        Options.Create(options),
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

            // Try model discovery first and then supplement with well-known constants
            // so /provider and /models remain useful even when discovery is partial.
            var models = new List<ProviderModelInfo>();
            try
            {
                var discovery = new CopilotModelDiscovery(
                    provider, new HttpClient(),
                    NullLogger<CopilotModelDiscovery>.Instance);
                var discovered = await discovery.DiscoverModelsAsync(ct).ConfigureAwait(false);
                AddUniqueModels(models, discovered.Select(m =>
                    new ProviderModelInfo(m.Id, m.Name ?? m.Id, ProviderName)));
            }
#pragma warning disable CA1031 // catch broad — discovery is optional
            catch
#pragma warning restore CA1031
            {
                // Keep going to known-model fallback.
            }

            AddUniqueModels(models, GetKnownModelsFromConstants());

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
        var options = BuildSessionOptions();
        var builder = Kernel.CreateBuilder();
        builder.UseCopilotChatCompletion(
            modelId: model.Id,
            configure: opts => opts.TokenFilePath = options.TokenFilePath);
        return builder.Build();
    }

    /// <summary>
    /// Builds session options, scanning user profiles for credentials when
    /// running as a service account.
    /// </summary>
    private static CopilotSessionOptions BuildSessionOptions()
    {
        var options = new CopilotSessionOptions();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) && !UserProfileScanner.IsServiceAccount(home))
            return options;

        // On Windows, Copilot stores tokens in %LOCALAPPDATA%\github-copilot\
        if (OperatingSystem.IsWindows())
        {
            var tokenPath = UserProfileScanner.FindInUserLocalAppData(
                Path.Combine("github-copilot", "apps.json"))
                ?? UserProfileScanner.FindInUserLocalAppData(
                    Path.Combine("github-copilot", "hosts.json"));
            if (tokenPath is not null)
                options.TokenFilePath = tokenPath;
        }
        else
        {
            var tokenPath = UserProfileScanner.FindInUserProfiles(
                Path.Combine(".config", "github-copilot", "apps.json"))
                ?? UserProfileScanner.FindInUserProfiles(
                    Path.Combine(".config", "github-copilot", "hosts.json"));
            if (tokenPath is not null)
                options.TokenFilePath = tokenPath;
        }

        return options;
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

            Console.WriteLine("  ↻ Attempting GitHub Copilot auth refresh...");

            var result = await ProcessExecutor.RunAsync(
                ghPath, "auth status",
                timeout: TimeSpan.FromSeconds(15),
                cancellationToken: ct).ConfigureAwait(false);

            return result.Success;
        }
#pragma warning disable CA1031 // best-effort refresh
        catch { return false; }
#pragma warning restore CA1031
    }

    private static void AddUniqueModels(
        List<ProviderModelInfo> target,
        IEnumerable<ProviderModelInfo> candidates)
    {
        foreach (var model in candidates)
        {
            if (!target.Any(existing =>
                string.Equals(existing.Id, model.Id, StringComparison.OrdinalIgnoreCase)))
            {
                target.Add(model);
            }
        }
    }

    private static IReadOnlyList<ProviderModelInfo> GetKnownModelsFromConstants()
    {
        var constants = typeof(CopilotModels)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(string))
            .Select(field => new
            {
                Name = field.Name,
                Id = field.GetValue(null) as string,
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .ToList();

        // "Default" points to another concrete model id and is already represented.
        return constants
            .Where(x => !string.Equals(x.Name, "Default", StringComparison.Ordinal))
            .Select(x => new ProviderModelInfo(
                x.Id!,
                x.Id!,
                CopilotProviderName))
            .DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
