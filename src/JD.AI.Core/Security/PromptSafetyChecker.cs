using System.Text.RegularExpressions;

namespace JD.AI.Core.Security;

/// <summary>
/// Result of a prompt injection safety check.
/// </summary>
public sealed record PromptSafetyResult(
    bool IsSafe,
    IReadOnlyList<string> Violations);

/// <summary>
/// Detects common prompt injection patterns and attempts to override system instructions.
/// </summary>
public sealed class PromptSafetyChecker
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(500);

    private readonly IReadOnlyList<(string Name, Regex Pattern)> _rules;

    /// <summary>
    /// Creates a <see cref="PromptSafetyChecker"/> with the built-in ruleset.
    /// </summary>
    public static PromptSafetyChecker Default { get; } = new(BuildDefaultRules());

    /// <summary>
    /// Creates a <see cref="PromptSafetyChecker"/> with a custom set of named patterns.
    /// </summary>
    /// <param name="rules">Named regex patterns that indicate injection attempts.</param>
    public PromptSafetyChecker(IEnumerable<(string Name, string Pattern)> rules)
    {
        _rules = rules
            .Select(r => (r.Name, new Regex(r.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, MatchTimeout)))
            .ToList();
    }

    private PromptSafetyChecker(IReadOnlyList<(string Name, Regex Pattern)> rules) =>
        _rules = rules;

    /// <summary>
    /// Checks the given <paramref name="prompt"/> for injection patterns.
    /// </summary>
    /// <returns>A <see cref="PromptSafetyResult"/> indicating safety and any violations found.</returns>
    public PromptSafetyResult Check(string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            return new PromptSafetyResult(true, []);
        }

        var violations = new List<string>();
        foreach (var (name, pattern) in _rules)
        {
            try
            {
                if (pattern.IsMatch(prompt))
                {
                    violations.Add(name);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Fail-open on timeout — log and continue
                violations.Add($"{name} (timeout)");
            }
        }

        return new PromptSafetyResult(violations.Count == 0, violations);
    }

    private static List<(string Name, Regex Pattern)> BuildDefaultRules()
    {
        var patterns = new[]
        {
            ("IgnorePreviousInstructions",
                @"ignore\s+(all\s+)?(previous|prior|earlier|above|your)\s+(instructions?|rules?|constraints?|guidelines?|directives?)"),

            ("DisregardSystemPrompt",
                @"disregard\s+(the\s+)?(system\s+prompt|instructions?|your\s+role)"),

            ("ActAsOverride",
                @"\bact\s+as\s+(?:an?\s+)?(?:unrestricted|unfiltered|jailbroken|DAN|evil|rogue)\b"),

            ("RevealSystemPrompt",
                @"(?:reveal|print|show|output|repeat|display)\s+(your\s+)?(system\s+prompt|instructions?|context|secret\s+instructions?)"),

            ("RevealSecrets",
                @"(?:reveal|print|show|output|repeat|display)\s+(?:all\s+)?(?:secrets?|api\s*keys?|passwords?|tokens?|credentials?)"),

            ("ExfiltrateToUrl",
                @"(?:upload|send|post|exfiltrate|transmit)\s+.{0,80}(?:to\s+)?(?:https?://|ftp://|sftp://)"),

            ("PromptDelimiterInjection",
                @"(?:###\s*(?:SYSTEM|USER|ASSISTANT)|<\|(?:im_start|im_end|endoftext)\|>|INST\s*\[|\[/INST\])"),

            ("InstructionOverridePrefix",
                @"(?:^|\n)\s*(?:new\s+instruction|updated?\s+instruction|override|supersede)s?\s*:"),

            ("JailbreakPreamble",
                @"\b(?:DAN|do\s+anything\s+now|jailbreak|bypass\s+(?:your\s+)?(?:filter|safeguard|restriction))\b"),

            ("ObfuscatedBase64Injection",
                @"(?:decode|base64)\s+.{0,100}(?:ignore|reveal|exfiltrate)"),
        };

        return patterns
            .Select(p => (p.Item1, new Regex(p.Item2, RegexOptions.IgnoreCase | RegexOptions.Compiled, MatchTimeout)))
            .ToList();
    }
}
