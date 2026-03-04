using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects a locally-running Microsoft Foundry Local instance and enumerates
/// its models via the OpenAI-compatible REST API.
/// Port is discovered dynamically via the Foundry CLI or process scanning.
/// </summary>
public sealed partial class FoundryLocalDetector : IProviderDetector
{
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public string ProviderName => "Foundry Local";

    /// <summary>The resolved endpoint (populated after detection).</summary>
    internal string? Endpoint { get; private set; }

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        var endpoint = await DiscoverEndpointAsync(ct).ConfigureAwait(false);
        if (endpoint is null)
        {
            return new ProviderInfo(
                ProviderName,
                IsAvailable: false,
                StatusMessage: IsFoundryCliAvailable()
                    ? "Service not running — run 'foundry service start'"
                    : "Not installed",
                Models: []);
        }

        Endpoint = endpoint;

        try
        {
            var resp = await SharedClient
                .GetFromJsonAsync<FoundryModelsResponse>(
                    $"{endpoint}/v1/models", ct)
                .ConfigureAwait(false);

            var models = (resp?.Data ?? [])
                .Select(m =>
                {
                    var name = m.Id ?? "unknown";
                    var caps = ModelCapabilityHeuristics.InferFromName(name);
                    return new ProviderModelInfo(name, name, ProviderName, caps);
                })
                .ToList();

            return new ProviderInfo(
                ProviderName,
                IsAvailable: true,
                StatusMessage: $"{models.Count} model(s) at {endpoint}",
                Models: models);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ProviderInfo(
                ProviderName,
                IsAvailable: false,
                StatusMessage: $"Error querying models — {ex.Message}",
                Models: []);
        }
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var endpoint = Endpoint ?? "http://127.0.0.1:5272";

        var builder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0010 // OpenAI connector experimental
        builder.AddOpenAIChatCompletion(
            modelId: model.Id,
            apiKey: "foundry",
            httpClient: new HttpClient
            {
                BaseAddress = new Uri($"{endpoint}/v1/"),
                Timeout = TimeSpan.FromMinutes(10),
            });
#pragma warning restore SKEXP0010

        return builder.Build();
    }

    /// <summary>
    /// Discovers the Foundry Local endpoint by:
    /// 1. Parsing <c>foundry service start</c> output (e.g. "already running on http://...")
    /// 2. Scanning well-known ports as fallback
    /// </summary>
    private static async Task<string?> DiscoverEndpointAsync(CancellationToken ct)
    {
        // Strategy 1: Try "foundry service start" — it prints the URL if already running
        var cliEndpoint = TryGetEndpointFromCli();
        if (cliEndpoint is not null)
            return cliEndpoint;

        // Strategy 2: Scan well-known ports
        int[] ports = [5272, 64646, 62579];
        foreach (var port in ports)
        {
            var url = $"http://127.0.0.1:{port}";
            try
            {
                var resp = await SharedClient.GetAsync(new Uri($"{url}/v1/models"), ct)
                    .ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                    return url;
            }
            catch
            {
                // Try next port
            }
        }

        // Strategy 3: Scan for Inference.Service.Agent process listening port
        var processEndpoint = TryFindServiceProcessEndpoint();
        if (processEndpoint is not null)
            return processEndpoint;

        return null;
    }

    private static string? TryGetEndpointFromCli()
    {
        if (!IsFoundryCliAvailable())
            return null;

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "foundry",
                Arguments = "service start",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var errOutput = process.StandardError.ReadToEnd();
            process.WaitForExit(10_000);

            // Parse endpoint URL from output like "already running on http://127.0.0.1:62579/"
            // or "Service started on http://127.0.0.1:62579/"
            var combined = output + errOutput;
            var match = EndpointRegex().Match(combined);
            if (match.Success)
                return match.Groups[1].Value.TrimEnd('/');
        }
        catch
        {
            // CLI not available or errored
        }

        return null;
    }

    private static string? TryFindServiceProcessEndpoint()
    {
        try
        {
            // Look for the Foundry inference service process
            var procs = Process.GetProcessesByName("Inference.Service.Agent");
            if (procs.Length == 0)
                return null;

            // Use netstat to find listening port for the process
            using var netstat = new Process();
            netstat.StartInfo = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            netstat.Start();
            var netstatOutput = netstat.StandardOutput.ReadToEnd();
            netstat.WaitForExit(5_000);

            foreach (var proc in procs)
            {
                var pidStr = proc.Id.ToString();
                var lines = netstatOutput.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("LISTENING") && line.TrimEnd().EndsWith(pidStr))
                    {
                        // Parse port from line like "  TCP    127.0.0.1:62579    0.0.0.0:0    LISTENING    99156"
                        var portMatch = ListeningPortRegex().Match(line);
                        if (portMatch.Success)
                        {
                            var port = portMatch.Groups[1].Value;
                            return $"http://127.0.0.1:{port}";
                        }
                    }
                }

                proc.Dispose();
            }
        }
        catch
        {
            // Process scanning not available
        }

        return null;
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

            process?.WaitForExit(5_000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [GeneratedRegex(@"(https?://[\d.:]+)/?", RegexOptions.IgnoreCase)]
    private static partial Regex EndpointRegex();

    [GeneratedRegex(@":\s*(\d+)\s+.*LISTENING", RegexOptions.IgnoreCase)]
    private static partial Regex ListeningPortRegex();

    private sealed record FoundryModelsResponse(
        [property: JsonPropertyName("data")]
        List<FoundryModel>? Data);

    private sealed record FoundryModel(
        [property: JsonPropertyName("id")]
        string? Id);
}
