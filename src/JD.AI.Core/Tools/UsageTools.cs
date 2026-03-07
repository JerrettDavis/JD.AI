using System.ComponentModel;
using System.Text;
using JD.AI.Core.Attributes;
using JD.AI.Core.Providers;
using JD.AI.Core.Usage;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Tools for tracking token usage and costs across the session.
/// </summary>
[ToolPlugin("usage", RequiresInjection = true)]
public sealed class UsageTools
{
    private long _promptTokens;
    private long _completionTokens;
    private int _toolCalls;
    private int _turns;
    private ProviderModelInfo? _currentModel;
    private readonly ICostEstimator _costEstimator;

    public UsageTools(ICostEstimator? costEstimator = null)
    {
        _costEstimator = costEstimator ?? new DefaultCostEstimator();
    }

    /// <summary>
    /// Sets the current model for accurate cost calculation when metadata is available.
    /// </summary>
    public void SetModel(ProviderModelInfo model) => _currentModel = model;

    /// <summary>
    /// Records token usage for a turn. Called by the agent loop after each response.
    /// </summary>
    public void RecordUsage(long promptTokens, long completionTokens, int toolCalls)
    {
        Interlocked.Add(ref _promptTokens, promptTokens);
        Interlocked.Add(ref _completionTokens, completionTokens);
        Interlocked.Add(ref _toolCalls, toolCalls);
        Interlocked.Increment(ref _turns);
    }

    [KernelFunction("get_usage")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description(
        "Get token usage and cost statistics for the current session. " +
        "Shows prompt tokens, completion tokens, total tokens, tool calls, " +
        "and estimated cost based on common model pricing.")]
    public string GetUsage()
    {
        var prompt = Interlocked.Read(ref _promptTokens);
        var completion = Interlocked.Read(ref _completionTokens);
        var total = prompt + completion;
        var tools = _toolCalls;
        var turns = _turns;

        var sb = new StringBuilder();
        sb.AppendLine("=== Session Usage ===");
        sb.AppendLine($"Turns: {turns}");
        sb.AppendLine($"Prompt tokens: {prompt:N0}");
        sb.AppendLine($"Completion tokens: {completion:N0}");
        sb.AppendLine($"Total tokens: {total:N0}");
        sb.AppendLine($"Tool calls: {tools}");
        sb.AppendLine();

        if (_currentModel is not null)
        {
            var (inputRate, outputRate, source) = _costEstimator.ResolveRates(_currentModel);
            var cost = _costEstimator.EstimateTurnCostUsd(_currentModel, prompt, completion);

            sb.AppendLine($"=== Cost ({_currentModel.DisplayName}) ===");
            sb.AppendLine($"  Rate source: {source}");
            sb.AppendLine($"  Input:  ${prompt * inputRate:F6}");
            sb.AppendLine($"  Output: ${completion * outputRate:F6}");
            sb.AppendLine($"  Total:  ${cost:F6}");
        }
        else
        {
            sb.AppendLine("=== Cost ===");
            sb.AppendLine("  Current model not set. Switch/select a model for cost estimation.");
        }

        return sb.ToString();
    }

    [KernelFunction("reset_usage")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Reset session usage counters to zero.")]
    public string ResetUsage()
    {
        Interlocked.Exchange(ref _promptTokens, 0);
        Interlocked.Exchange(ref _completionTokens, 0);
        Interlocked.Exchange(ref _toolCalls, 0);
        Interlocked.Exchange(ref _turns, 0);
        return "Usage counters reset.";
    }
}
