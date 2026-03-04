using System.Diagnostics;

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
            // List cached models
            var cached = await RunFoundryCommandAsync("models list", ct)
                .ConfigureAwait(false);

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

                // First token is typically the model name
                var name = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (name is null)
                {
                    continue;
                }

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
                    null));
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
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "foundry",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<List<string>> RunFoundryCommandAsync(
        string arguments, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "foundry",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();

        var lines = new List<string>();
        while (await process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            lines.Add(line);
        }

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return lines;
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
