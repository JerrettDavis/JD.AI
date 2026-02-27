using JD.SemanticKernel.Connectors.ClaudeCode;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Spectre.Console;

namespace JD.AI.Tui.Providers;

/// <summary>
/// Detects a local Claude Code session and exposes its models.
/// When the session token is expired, attempts a silent refresh via the Claude CLI.
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
                // Token may be expired — try a silent CLI refresh
                var refreshed = await TryRefreshAuthAsync(ct).ConfigureAwait(false);
                if (refreshed)
                {
                    // Re-create provider to pick up refreshed credentials
                    provider.Dispose();
                    provider = new ClaudeCodeSessionProvider(
                        Options.Create(new ClaudeCodeSessionOptions()),
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
        var builder = Kernel.CreateBuilder();
        builder.UseClaudeCodeChatCompletion(modelId: model.Id);
        return builder.Build();
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

            AnsiConsole.MarkupLine("[dim]  ↻ Attempting Claude session refresh...[/]");

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
