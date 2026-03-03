using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// A scratchpad/thinking tool that lets the agent reason out loud without side effects.
/// The agent can use this to organize thoughts, plan steps, or reason through complex logic.
/// </summary>
public sealed class ThinkTools
{
    [KernelFunction("think")]
    [Description(
        "Use this tool to think through complex problems, plan multi-step approaches, " +
        "or reason about trade-offs. This has no side effects — it simply returns your " +
        "thought back to you as a structured note. Use it before taking action when " +
        "the task requires careful reasoning.")]
    public static string Think(
        [Description("Your reasoning, plan, or analysis")] string thought)
    {
        return $"[Thought recorded]\n{thought}";
    }
}
