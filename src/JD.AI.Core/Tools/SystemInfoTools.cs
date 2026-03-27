using System.ComponentModel;
using System.Globalization;
using System.Text;
using JD.AI.Core.Attributes;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Self-identity and system information tools.
/// Enables the agent to report what model/provider it is running on,
/// its capabilities, and runtime metadata.
/// </summary>
[ToolPlugin("system", RequiresInjection = true)]
public sealed class SystemInfoTools
{
    private ProviderModelInfo? _model;
    private string? _agentId;
    private DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private string? _daemonVersion;
    private string? _latestDaemonVersion;
    private DateTimeOffset? _versionCheckedAt;

    /// <summary>Sets the active model info (called during registration).</summary>
    public void SetModel(ProviderModelInfo model) => _model = model;

    /// <summary>Sets the agent instance ID (called after spawn).</summary>
    public void SetAgentId(string agentId) => _agentId = agentId;

    /// <summary>Sets the agent start time.</summary>
    public void SetStartedAt(DateTimeOffset startedAt) => _startedAt = startedAt;

    /// <summary>Sets the daemon version info for identity reporting.</summary>
    public void SetDaemonVersion(string currentVersion, string? latestVersion = null)
    {
        _daemonVersion = currentVersion;
        _latestDaemonVersion = latestVersion;
        _versionCheckedAt = DateTimeOffset.UtcNow;
    }

    [KernelFunction("get_identity")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description(
        "Get information about this agent's identity: model name, provider, capabilities, " +
        "context window size, and other model metadata. Use this when asked 'what model are you?' " +
        "or similar self-identification questions.")]
    public string GetIdentity()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Agent Identity ===");

        if (_model is not null)
        {
            sb.AppendLine($"Model: {_model.DisplayName}");
            sb.AppendLine($"Model ID: {_model.Id}");
            sb.AppendLine($"Provider: {_model.ProviderName}");
            sb.AppendLine($"Context Window: {_model.ContextWindowTokens:N0} tokens");
            sb.AppendLine($"Max Output: {_model.MaxOutputTokens:N0} tokens");
            sb.AppendLine($"Capabilities: {_model.Capabilities.ToLabel()}");

            if (_model.InputCostPerToken > 0 || _model.OutputCostPerToken > 0)
            {
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "Cost: ${0:F6}/input token, ${1:F6}/output token",
                    _model.InputCostPerToken,
                    _model.OutputCostPerToken));
            }
        }
        else
        {
            sb.AppendLine("Model: Unknown (metadata not available)");
        }

        if (!string.IsNullOrEmpty(_agentId))
            sb.AppendLine($"Agent ID: {_agentId}");

        sb.AppendLine($"Platform: JD.AI");

        if (!string.IsNullOrEmpty(_daemonVersion))
        {
            sb.AppendLine($"Daemon Version: {_daemonVersion}");
            if (!string.IsNullOrEmpty(_latestDaemonVersion))
            {
                var isLatest = string.Equals(_daemonVersion, _latestDaemonVersion, StringComparison.Ordinal);
                sb.AppendLine(isLatest
                    ? $"Latest Version: {_latestDaemonVersion} (up-to-date ✓)"
                    : $"Latest Version: {_latestDaemonVersion} (UPDATE AVAILABLE)");
            }
            if (_versionCheckedAt.HasValue)
                sb.AppendLine($"Version Checked: {_versionCheckedAt.Value:u}");
        }

        sb.AppendLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");

        return sb.ToString();
    }

    [KernelFunction("get_system_status")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description(
        "Get the current system status: uptime, agent ID, and runtime information. " +
        "Use this for health checks or when asked about system state.")]
    public string GetSystemStatus()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== System Status ===");

        var uptime = DateTimeOffset.UtcNow - _startedAt;
        sb.AppendLine($"Status: Online");
        sb.AppendLine($"Uptime: {FormatUptime(uptime)}");
        sb.AppendLine($"Started: {_startedAt:u}");

        if (!string.IsNullOrEmpty(_agentId))
            sb.AppendLine($"Agent ID: {_agentId}");

        if (_model is not null)
        {
            sb.AppendLine($"Model: {_model.ProviderName}/{_model.Id}");
            sb.AppendLine($"Capabilities: {_model.Capabilities.ToLabel()}");
        }

        if (!string.IsNullOrEmpty(_daemonVersion))
        {
            sb.AppendLine($"Daemon Version: {_daemonVersion}");
            if (!string.IsNullOrEmpty(_latestDaemonVersion))
            {
                var isLatest = string.Equals(_daemonVersion, _latestDaemonVersion, StringComparison.Ordinal);
                sb.Append(isLatest ? "Update: Up-to-date ✓" : $"Update: {_latestDaemonVersion} available!");
                sb.AppendLine();
            }
        }

        sb.AppendLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        sb.AppendLine($"Processors: {Environment.ProcessorCount}");

        return sb.ToString();
    }

    private static string FormatUptime(TimeSpan ts) => ts switch
    {
        { TotalDays: >= 1 } => $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m",
        { TotalHours: >= 1 } => $"{(int)ts.TotalHours}h {ts.Minutes}m",
        _ => $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
    };
}
