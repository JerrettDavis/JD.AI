using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using JD.AI.Core.Attributes;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Gateway lifecycle and configuration tools — status, config, restart controls.
/// Operates against the local gateway process or a configured remote endpoint.
/// </summary>
[ToolPlugin("gateway", RequiresInjection = true)]
public sealed class GatewayOpsTools
{
    private readonly string? _gatewayEndpoint;
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <param name="gatewayEndpoint">Optional gateway base URL (e.g. http://localhost:18789).
    /// If null, tools report based on process-level checks.</param>
    public GatewayOpsTools(string? gatewayEndpoint = null)
    {
        _gatewayEndpoint = gatewayEndpoint?.TrimEnd('/');
    }

    [KernelFunction("gateway_status")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Check the gateway's health and connection status.")]
    public async Task<string> GetStatusAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Gateway Status");

        if (string.IsNullOrEmpty(_gatewayEndpoint))
        {
            sb.AppendLine("- **Endpoint**: Not configured");
            sb.AppendLine("- **Status**: No gateway endpoint set. Use --gateway-url or configure in settings.");
            return sb.ToString();
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Endpoint**: {_gatewayEndpoint}");

        try
        {
            var healthUrl = new Uri($"{_gatewayEndpoint}/health");
            using var response = await SharedClient.GetAsync(healthUrl).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Health**: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                sb.AppendLine("- **Available**: Yes");
                if (!string.IsNullOrWhiteSpace(body) && body.Length < 500)
                    sb.AppendLine(CultureInfo.InvariantCulture, $"- **Details**: {body}");
            }
            else
            {
                sb.AppendLine("- **Available**: No (unhealthy)");
            }
        }
        catch (HttpRequestException ex)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Available**: No ({ex.Message})");
        }
        catch (TaskCanceledException)
        {
            sb.AppendLine("- **Available**: No (timeout)");
        }

        // Check local gateway process
        try
        {
            var processes = Process.GetProcessesByName("JD.AI.Gateway");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Local processes**: {processes.Length}");
            foreach (var p in processes)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"  - PID {p.Id}, uptime: {(DateTime.Now - p.StartTime).TotalMinutes:F0} min");
                p.Dispose();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Process check**: {ex.Message}");
        }

        return sb.ToString();
    }

    [KernelFunction("gateway_config")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Retrieve the current gateway configuration (secrets redacted).")]
    public async Task<string> GetConfigAsync()
    {
        if (string.IsNullOrEmpty(_gatewayEndpoint))
            return "Error: No gateway endpoint configured.";

        try
        {
            var url = new Uri($"{_gatewayEndpoint}/api/v1/gateway/config");
            using var response = await SharedClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var sb = new StringBuilder();
            sb.AppendLine("## Gateway Configuration");
            sb.AppendLine("```json");
            sb.AppendLine(json.Length > 5000 ? string.Concat(json.AsSpan(0, 5000), "\n... [truncated]") : json);
            sb.AppendLine("```");
            return sb.ToString();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"Error fetching gateway config: {ex.Message}";
        }
    }

    [KernelFunction("gateway_channels")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List channels registered in the gateway with their connection status.")]
    public async Task<string> ListGatewayChannelsAsync()
    {
        if (string.IsNullOrEmpty(_gatewayEndpoint))
            return "Error: No gateway endpoint configured.";

        try
        {
            var url = new Uri($"{_gatewayEndpoint}/api/v1/channels");
            using var response = await SharedClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var sb = new StringBuilder();
            sb.AppendLine("## Gateway Channels");
            sb.AppendLine("```json");
            sb.AppendLine(json.Length > 3000 ? string.Concat(json.AsSpan(0, 3000), "\n... [truncated]") : json);
            sb.AppendLine("```");
            return sb.ToString();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"Error fetching channels: {ex.Message}";
        }
    }

    [KernelFunction("gateway_agents")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List agents managed by the gateway.")]
    public async Task<string> ListGatewayAgentsAsync()
    {
        if (string.IsNullOrEmpty(_gatewayEndpoint))
            return "Error: No gateway endpoint configured.";

        try
        {
            var url = new Uri($"{_gatewayEndpoint}/api/v1/agents");
            using var response = await SharedClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var sb = new StringBuilder();
            sb.AppendLine("## Gateway Agents");
            sb.AppendLine("```json");
            sb.AppendLine(json.Length > 3000 ? string.Concat(json.AsSpan(0, 3000), "\n... [truncated]") : json);
            sb.AppendLine("```");
            return sb.ToString();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"Error fetching agents: {ex.Message}";
        }
    }

    [KernelFunction("gateway_sessions")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List active sessions in the gateway.")]
    public async Task<string> ListGatewaySessionsAsync()
    {
        if (string.IsNullOrEmpty(_gatewayEndpoint))
            return "Error: No gateway endpoint configured.";

        try
        {
            var url = new Uri($"{_gatewayEndpoint}/api/v1/sessions");
            using var response = await SharedClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var sb = new StringBuilder();
            sb.AppendLine("## Gateway Sessions");
            sb.AppendLine("```json");
            sb.AppendLine(json.Length > 5000 ? string.Concat(json.AsSpan(0, 5000), "\n... [truncated]") : json);
            sb.AppendLine("```");
            return sb.ToString();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"Error fetching sessions: {ex.Message}";
        }
    }
}
