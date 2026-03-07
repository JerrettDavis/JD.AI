namespace JD.AI.Core.Tools;

/// <summary>
/// Well-known loadout names provided by the built-in <see cref="ToolLoadoutRegistry"/>.
/// </summary>
public static class WellKnownLoadouts
{
    /// <summary>Bare-minimum tools: filesystem, shell, and think.</summary>
    public const string Minimal = "minimal";

    /// <summary>Developer-oriented tools: minimal + git, github, search, analysis, memory.</summary>
    public const string Developer = "developer";

    /// <summary>DevOps-oriented tools: minimal + git, network, scheduling.</summary>
    public const string DevOps = "devops";

    /// <summary>Research-oriented tools: minimal + search, web, memory, multimodal.</summary>
    public const string Research = "research";

    /// <summary>All registered tool categories.</summary>
    public const string Full = "full";
}
