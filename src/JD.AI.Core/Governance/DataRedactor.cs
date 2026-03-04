using System.Text.RegularExpressions;

namespace JD.AI.Core.Governance;

/// <summary>
/// Applies regex-based redaction patterns to content before sending to AI providers.
/// Patterns come from the resolved policy's DataPolicy.RedactPatterns.
/// </summary>
public sealed class DataRedactor
{
    private readonly List<Regex> _patterns;

    public DataRedactor(IEnumerable<string> patterns)
    {
        _patterns = patterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
            .ToList();
    }

    /// <summary>
    /// Returns a new DataRedactor with no patterns (pass-through).
    /// </summary>
    public static DataRedactor None { get; } = new([]);

    /// <summary>
    /// Replaces all matches of configured patterns with [REDACTED].
    /// </summary>
    public string Redact(string input)
    {
        if (_patterns.Count == 0 || string.IsNullOrEmpty(input))
            return input;

        var result = input;
        foreach (var pattern in _patterns)
        {
            try
            {
                result = pattern.Replace(result, "[REDACTED]");
            }
            catch (RegexMatchTimeoutException)
            {
                // Skip patterns that take too long (possible ReDoS)
            }
        }
        return result;
    }

    /// <summary>
    /// Checks if input contains any content matching redaction patterns.
    /// </summary>
    public bool HasSensitiveContent(string input)
    {
        if (_patterns.Count == 0 || string.IsNullOrEmpty(input))
            return false;

        foreach (var pattern in _patterns)
        {
            try
            {
                if (pattern.IsMatch(input))
                    return true;
            }
            catch (RegexMatchTimeoutException)
            {
                // Skip
            }
        }
        return false;
    }
}
