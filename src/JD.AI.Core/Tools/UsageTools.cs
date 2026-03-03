using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Tools for tracking token usage and costs across the session.
/// </summary>
public sealed class UsageTools
{
    private long _promptTokens;
    private long _completionTokens;
    private int _toolCalls;
    private int _turns;

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
        sb.AppendLine("=== Estimated Cost ===");

        // Common pricing tiers (approximate, may be outdated)
        var costs = new (string Model, decimal PromptPer1K, decimal CompletionPer1K)[]
        {
            ("Claude Sonnet 4", 0.003m, 0.015m),
            ("Claude Haiku 4", 0.0008m, 0.004m),
            ("GPT-4.1", 0.002m, 0.008m),
            ("GPT-5-mini", 0.00015m, 0.0006m),
            ("Local (Ollama/LLamaSharp)", 0m, 0m),
        };

        foreach (var (model, promptRate, completionRate) in costs)
        {
            var cost = (prompt / 1000m * promptRate) + (completion / 1000m * completionRate);
            sb.AppendLine($"  {model}: ${cost:F4}");
        }

        return sb.ToString();
    }

    [KernelFunction("reset_usage")]
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
