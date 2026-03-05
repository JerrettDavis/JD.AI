using JD.SemanticKernel.Connectors.OpenAICodex;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using System.Text.Json;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects a local OpenAI Codex session and exposes its models.
/// When running as a Windows service, scans user profiles for credentials.
/// Supports automatic token refresh and device code login.
/// </summary>
public sealed class OpenAICodexDetector : IProviderDetector
{
    private const string CodexProviderName = "OpenAI Codex";

    public string ProviderName => CodexProviderName;

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
            AddUniqueModels(models, DiscoverModelsFromCache(options));

            if (models.Count == 0)
            {
                try
                {
                    var discovery = new CodexModelDiscovery();
                    var discovered = await discovery.DiscoverModelsAsync(ct).ConfigureAwait(false);
                    AddUniqueModels(models, discovered.Select(m =>
                        new ProviderModelInfo(m.Id, m.Name ?? m.Id, ProviderName)));
                }
#pragma warning disable CA1031 // catch broad — discovery is optional
                catch
#pragma warning restore CA1031
                {
                    // Keep going to fallback list.
                }
            }

            if (models.Count == 0)
            {
                AddUniqueModels(models,
                [
                    new ProviderModelInfo("gpt-5.3-codex", "gpt-5.3-codex", ProviderName),
                    new ProviderModelInfo("gpt-5.2-codex", "gpt-5.2-codex", ProviderName),
                    new ProviderModelInfo("gpt-5.1-codex-max", "gpt-5.1-codex-max", ProviderName),
                    new ProviderModelInfo("gpt-5.2", "gpt-5.2", ProviderName),
                    new ProviderModelInfo("gpt-5.1-codex-mini", "gpt-5.1-codex-mini", ProviderName),
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

    internal static IReadOnlyList<ProviderModelInfo> ReadModelsFromCache(string cachePath)
    {
        if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath))
            return [];

        try
        {
            using var stream = File.OpenRead(cachePath);
            using var doc = JsonDocument.Parse(stream);
            if (!doc.RootElement.TryGetProperty("models", out var modelsElement) ||
                modelsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var models = new List<(int Priority, ProviderModelInfo Model)>();
            foreach (var entry in modelsElement.EnumerateArray())
            {
                var id = GetString(entry, "slug");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var visibility = GetString(entry, "visibility");
                if (!string.Equals(visibility, "list", StringComparison.OrdinalIgnoreCase))
                    continue;

                var supportedInApi = !entry.TryGetProperty("supported_in_api", out var supportedElement)
                    || supportedElement.ValueKind == JsonValueKind.True;
                if (!supportedInApi)
                    continue;

                var displayName = GetString(entry, "display_name") ?? id;
                var priority = entry.TryGetProperty("priority", out var priorityElement)
                    && priorityElement.TryGetInt32(out var parsedPriority)
                    ? parsedPriority
                    : int.MaxValue;

                models.Add((priority, new ProviderModelInfo(id, displayName, CodexProviderName)));
            }

            return models
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Model.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Model)
                .DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IReadOnlyList<ProviderModelInfo> DiscoverModelsFromCache(CodexSessionOptions options)
    {
        var cachePath = ResolveModelsCachePath(options);
        return cachePath is null ? [] : ReadModelsFromCache(cachePath);
    }

    private static string? ResolveModelsCachePath(CodexSessionOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CredentialsPath))
        {
            var credentialsDirectory = Path.GetDirectoryName(options.CredentialsPath);
            if (!string.IsNullOrWhiteSpace(credentialsDirectory))
            {
                var sibling = Path.Combine(credentialsDirectory, "models_cache.json");
                if (File.Exists(sibling))
                    return sibling;
            }
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home) && !UserProfileScanner.IsServiceAccount(home))
        {
            var localCache = Path.Combine(home, ".codex", "models_cache.json");
            if (File.Exists(localCache))
                return localCache;
        }

        return UserProfileScanner.FindInUserProfiles(
            Path.Combine(".codex", "models_cache.json"));
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

    private static string? GetString(JsonElement entry, string propertyName)
    {
        if (!entry.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }
}
