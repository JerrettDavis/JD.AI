using System.Text.RegularExpressions;

namespace JD.AI.Core.Governance;

/// <summary>
/// Applies regex-based redaction patterns to content before sending to AI providers.
/// Supports both flat <c>RedactPatterns</c> and structured <see cref="DataClassification"/>
/// rules with per-classification actions.
/// </summary>
public sealed class DataRedactor
{
    private readonly List<Regex> _patterns;
    private readonly List<(DataClassification Classification, Regex Regex)> _classifications;

    public DataRedactor(IEnumerable<string> patterns)
        : this(patterns, [])
    {
    }

    public DataRedactor(IEnumerable<string> patterns, IEnumerable<DataClassification> classifications)
    {
        _patterns = patterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
            .ToList();

        _classifications = classifications
            .SelectMany(c => c.Patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p =>
                {
                    try { return (c, new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1))); }
                    catch { return (c, (Regex?)null); }
                })
                .Where(x => x.Item2 is not null)
                .Select(x => (x.c, x.Item2!)))
            .ToList();
    }

    /// <summary>
    /// Returns a new DataRedactor with no patterns (pass-through).
    /// </summary>
    public static DataRedactor None { get; } = new([]);

    /// <summary>
    /// Replaces all matches of configured flat patterns with [REDACTED].
    /// Does not apply classification actions — use <see cref="RedactWithClassifications"/> for that.
    /// </summary>
    public string Redact(string input)
    {
        if (_patterns.Count == 0 || string.IsNullOrEmpty(input))
            return ApplyClassificationRedacts(input);

        var result = input;
        foreach (var pattern in _patterns)
        {
            try { result = pattern.Replace(result, "[REDACTED]"); }
            catch (RegexMatchTimeoutException) { /* skip slow patterns */ }
        }
        return ApplyClassificationRedacts(result);
    }

    /// <summary>
    /// Applies both flat redaction and structured classification rules.
    /// Returns a <see cref="RedactionResult"/> that includes which classifications fired
    /// and whether any request should be denied.
    /// </summary>
    public RedactionResult RedactWithClassifications(string input)
    {
        if (string.IsNullOrEmpty(input))
            return new RedactionResult(input, []);

        // 1. Apply flat patterns first
        var content = input;
        foreach (var pattern in _patterns)
        {
            try { content = pattern.Replace(content, "[REDACTED]"); }
            catch (RegexMatchTimeoutException) { /* skip */ }
        }

        // 2. Apply classification rules
        var matches = new List<ClassificationMatch>();
        foreach (var (cls, regex) in _classifications)
        {
            bool found;
            try { found = regex.IsMatch(content); }
            catch (RegexMatchTimeoutException) { continue; }

            if (!found) continue;

            matches.Add(new ClassificationMatch(cls.Name, cls.Action));

            content = cls.Action switch
            {
                ClassificationAction.Redact or ClassificationAction.RedactAndAudit =>
                    TryReplace(regex, content, $"[REDACTED:{cls.Name}]"),
                ClassificationAction.AuditOnly => content, // pass-through
                ClassificationAction.DenyAndAudit => content, // deny is surfaced via RedactionResult.ShouldDeny
                _ => content,
            };
        }

        return new RedactionResult(content, matches.AsReadOnly());
    }

    /// <summary>
    /// Checks if input contains any content matching redaction patterns or classifications.
    /// </summary>
    public bool HasSensitiveContent(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;

        foreach (var pattern in _patterns)
        {
            try { if (pattern.IsMatch(input)) return true; }
            catch (RegexMatchTimeoutException) { /* skip */ }
        }

        foreach (var (_, regex) in _classifications)
        {
            try { if (regex.IsMatch(input)) return true; }
            catch (RegexMatchTimeoutException) { /* skip */ }
        }

        return false;
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private string ApplyClassificationRedacts(string input)
    {
        if (_classifications.Count == 0 || string.IsNullOrEmpty(input)) return input;

        var result = input;
        foreach (var (cls, regex) in _classifications)
        {
            if (cls.Action is not (ClassificationAction.Redact or ClassificationAction.RedactAndAudit))
                continue;
            result = TryReplace(regex, result, $"[REDACTED:{cls.Name}]");
        }
        return result;
    }

    private static string TryReplace(Regex regex, string input, string replacement)
    {
        try { return regex.Replace(input, replacement); }
        catch (RegexMatchTimeoutException) { return input; }
    }
}
