using Anthropic.SDK;
using JD.SemanticKernel.Connectors.ClaudeCode;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects a local Claude Code session and exposes its models.
/// When running as a Windows service, scans user profiles for credentials.
/// When the session token is expired, attempts a silent refresh via the Claude CLI.
/// </summary>
public sealed class ClaudeCodeDetector : IProviderDetector
{
    public string ProviderName => "Claude Code";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        try
        {
            // Build options with credential path resolution for service accounts
            var options = BuildSessionOptions();
            var provider = new ClaudeCodeSessionProvider(
                Options.Create(options),
                NullLogger<ClaudeCodeSessionProvider>.Instance);

            var isAuth = await provider.IsAuthenticatedAsync(ct).ConfigureAwait(false);

            if (!isAuth)
            {
                // Token may be expired — try a silent CLI refresh
                var refreshed = await TryRefreshAuthAsync(ct).ConfigureAwait(false);
                if (refreshed)
                {
                    provider.Dispose();
                    provider = new ClaudeCodeSessionProvider(
                        Options.Create(options),
                        NullLogger<ClaudeCodeSessionProvider>.Instance);
                    isAuth = await provider.IsAuthenticatedAsync(ct).ConfigureAwait(false);
                }

                if (!isAuth)
                {
                    provider.Dispose();
                    return new ProviderInfo(
                        ProviderName,
                        IsAvailable: false,
                        StatusMessage: "Not authenticated — run 'claude login' to sign in",
                        Models: []);
                }
            }

            provider.Dispose();

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
        var options = BuildSessionOptions();
        var builder = Kernel.CreateBuilder();
        ConfigureKernelBuilder(builder, options, model.Id);
        return builder.Build();
    }

    internal static void ConfigureKernelBuilder(
        IKernelBuilder builder,
        ClaudeCodeSessionOptions options,
        string modelId = ClaudeModels.Default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        builder.Services.AddSingleton(Options.Create(options));

        builder.Services.AddSingleton(sp =>
            new ClaudeCodeSessionProvider(
                sp.GetRequiredService<IOptions<ClaudeCodeSessionOptions>>(),
                NullLogger<ClaudeCodeSessionProvider>.Instance));

        builder.Services.AddSingleton<IChatClient>(sp =>
        {
            var sessionProvider = sp.GetRequiredService<ClaudeCodeSessionProvider>();
            var token = sessionProvider
                .GetTokenAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ClaudeCodeSessionException("No Claude token available.");
            }

            var httpClient = new HttpClient(new ClaudeCodeSessionHttpHandler(sessionProvider))
            {
                Timeout = TimeSpan.FromMinutes(10),
            };

            var anthropicClient = new AnthropicClient(
                new APIAuthentication(token),
                httpClient);

            return new ChatClientBuilder(
                    new AnthropicPromptCachingChatClient(anthropicClient.Messages))
                .ConfigureOptions(o => o.ModelId ??= modelId)
                .Build();
        });

        builder.Services.AddSingleton<IChatCompletionService>(sp =>
            sp.GetRequiredService<IChatClient>()
              .AsChatCompletionService(sp));
    }

    /// <summary>
    /// Builds session options, scanning user profiles for credentials when
    /// running as a service account (LocalSystem, NetworkService, etc.).
    /// </summary>
    private static ClaudeCodeSessionOptions BuildSessionOptions()
    {
        var options = new ClaudeCodeSessionOptions
        {
        };

        // Check if the default path would resolve to a service account home
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) && !UserProfileScanner.IsServiceAccount(home))
            return options;

        // Scan real user profiles for Claude credentials
        var credPath = UserProfileScanner.FindInUserProfiles(
            Path.Combine(".claude", ".credentials.json"));
        if (credPath is not null)
            options.CredentialsPath = credPath;

        return options;
    }

    /// <summary>
    /// Attempts to refresh Claude Code auth by invoking the CLI.
    /// Running <c>claude --version</c> triggers internal token refresh
    /// when the refresh token is still valid.
    /// </summary>
    private static async Task<bool> TryRefreshAuthAsync(CancellationToken ct)
    {
        try
        {
            var claudePath = FindCli("claude");
            if (claudePath is null) return false;

            Console.WriteLine("  ↻ Attempting Claude session refresh...");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = claudePath,
                Arguments = "--version",
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

    /// <summary>Finds a CLI executable on PATH.</summary>
    public static string? FindCli(string name)
    {
        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".cmd", ".exe", ".bat" }
            : new[] { "" };

        var pathDirs = Environment.GetEnvironmentVariable("PATH")?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) ?? [];

        foreach (var dir in pathDirs)
        {
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(dir, name + ext);
                if (File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }
}
