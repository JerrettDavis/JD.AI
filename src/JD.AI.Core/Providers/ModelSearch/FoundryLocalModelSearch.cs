using System.Diagnostics;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Core.Providers.ModelSearch;

/// <summary>
/// Searches for models available through Microsoft Foundry Local CLI.
/// </summary>
public sealed class FoundryLocalModelSearch : IRemoteModelSearch
{
    public string ProviderName => "Foundry Local";

    public async Task<IReadOnlyList<RemoteModelResult>> SearchAsync(
        string query, CancellationToken ct = default)
    {
        if (!IsFoundryCliAvailable())
        {
            return [];
        }

        try
        {
            var procResult = await ProcessExecutor.RunAsync(
                "foundry", "models list", cancellationToken: ct)
                .ConfigureAwait(false);

            var cached = procResult.StandardOutput
                .Split('\n')
                .ToList();

            var results = new List<RemoteModelResult>();

            foreach (var line in cached)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)
                    || trimmed.StartsWith('-')
                    || trimmed.StartsWith("NAME", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var name = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (name is null)
                    continue;

                if (!string.IsNullOrWhiteSpace(query)
                    && !name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                results.Add(new RemoteModelResult(
                    name,
                    name,
                    ProviderName,
                    null,
                    "Installed",
                    null,
                    ModelCapabilityHeuristics.InferFromName(name)));
            }

            return results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    public async Task<bool> PullAsync(
        RemoteModelResult model,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsFoundryCliAvailable())
        {
            progress?.Report("Foundry CLI not found.");
            return false;
        }

        try
        {
            // PullAsync needs streaming output for progress — keep manual Process for this
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "foundry",
                Arguments = $"models pull {model.Id}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();

            await Task.WhenAll(
                ReadStreamAsync(process.StandardOutput, progress, ct),
                ReadStreamAsync(process.StandardError, progress, ct))
                .ConfigureAwait(false);

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            progress?.Report($"Error: {ex.Message}");
            return false;
        }
    }

    private static bool IsFoundryCliAvailable()
    {
        try
        {
            var result = ProcessExecutor.RunAsync(
                "foundry", "--version", timeout: TimeSpan.FromSeconds(5))
                .GetAwaiter().GetResult();
            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    private static async Task ReadStreamAsync(
        System.IO.StreamReader reader,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            progress?.Report(line);
        }
    }
}
