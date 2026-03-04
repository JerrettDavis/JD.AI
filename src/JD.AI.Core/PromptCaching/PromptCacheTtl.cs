namespace JD.AI.Core.PromptCaching;

/// <summary>
/// Prompt cache lifetimes supported by Anthropic prompt caching.
/// </summary>
public enum PromptCacheTtl
{
    FiveMinutes = 0,
    OneHour = 1,
}
