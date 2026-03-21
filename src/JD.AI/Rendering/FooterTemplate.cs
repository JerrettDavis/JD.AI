using System.Text;
using System.Text.RegularExpressions;

namespace JD.AI.Rendering;

/// <summary>
/// An individual token produced by <see cref="FooterTemplate.Parse"/>.
/// </summary>
public sealed record TemplateToken
{
    /// <summary>Whether this token is a literal text fragment.</summary>
    public bool IsLiteral { get; init; }

    /// <summary>The literal text, set when <see cref="IsLiteral"/> is <see langword="true"/>.</summary>
    public string? LiteralText { get; init; }

    /// <summary>The segment key, set when <see cref="IsLiteral"/> is <see langword="false"/>.</summary>
    public string? SegmentKey { get; init; }

    /// <summary>Whether this segment is optional (uses the <c>?</c> suffix in the template).</summary>
    public bool IsConditional { get; init; }

    /// <summary>Creates a literal text token.</summary>
    public static TemplateToken Literal(string text) =>
        new() { IsLiteral = true, LiteralText = text };

    /// <summary>Creates a segment placeholder token.</summary>
    public static TemplateToken Segment(string key, bool conditional) =>
        new() { IsLiteral = false, SegmentKey = key, IsConditional = conditional };
}

/// <summary>
/// A parsed footer template that can render itself given a dictionary of segment values.
/// </summary>
/// <remarks>
/// Template syntax:
/// <list type="bullet">
///   <item><c>{key}</c> — required segment (omitted silently if key is not in the dictionary)</item>
///   <item><c>{key?}</c> — conditional segment (also omitted when value is null or empty)</item>
///   <item>Any other text — rendered as-is</item>
/// </list>
/// Adjacent separator literals (<c>│</c>, <c>|</c>, <c>·</c>, <c>•</c>, <c>-</c>, <c>—</c>,
/// or whitespace-only strings) that surround omitted segments are collapsed so that
/// <c>"A │ │ D"</c> becomes <c>"A │ D"</c>.
/// </remarks>
public sealed partial class FooterTemplate
{
    // Matches {key} or {key?} where key may contain letters, digits, colon, underscore, hyphen.
    [GeneratedRegex(@"\{(?<key>[a-zA-Z][a-zA-Z0-9:_-]*)(?<cond>\?)?\}", RegexOptions.Compiled)]
    private static partial Regex TokenPattern();

    // A literal token whose text is purely a separator character (with optional surrounding spaces).
    [GeneratedRegex(@"^\s*[│|·•\-—]\s*$")]
    private static partial Regex SeparatorPattern();

    /// <summary>The ordered list of tokens that make up this template.</summary>
    public IReadOnlyList<TemplateToken> Tokens { get; }

    private FooterTemplate(IReadOnlyList<TemplateToken> tokens) => Tokens = tokens;

    /// <summary>Parses <paramref name="template"/> into a <see cref="FooterTemplate"/>.</summary>
    public static FooterTemplate Parse(string template)
    {
        if (string.IsNullOrEmpty(template))
            return new FooterTemplate([TemplateToken.Literal(string.Empty)]);

        var tokens = new List<TemplateToken>();
        var regex = TokenPattern();
        var pos = 0;

        foreach (Match match in regex.Matches(template))
        {
            // Emit any literal text before this match
            if (match.Index > pos)
                tokens.Add(TemplateToken.Literal(template[pos..match.Index]));

            var key = match.Groups["key"].Value;
            var conditional = match.Groups["cond"].Success;
            tokens.Add(TemplateToken.Segment(key, conditional));
            pos = match.Index + match.Length;
        }

        // Any remaining text (including malformed / unclosed braces) is literal
        if (pos < template.Length)
            tokens.Add(TemplateToken.Literal(template[pos..]));

        return new FooterTemplate(tokens);
    }

    /// <summary>
    /// Renders the template by substituting values from <paramref name="segments"/>.
    /// Conditional segments whose value is null or empty string are omitted, and adjacent
    /// separator literals are collapsed to prevent double-separator artifacts.
    /// </summary>
    public string Render(IReadOnlyDictionary<string, string?>? segments)
    {
        segments ??= new Dictionary<string, string?>();

        // --- Pass 1: resolve each token to a string (null = omitted) ---
        var resolved = new string?[Tokens.Count];
        for (var i = 0; i < Tokens.Count; i++)
        {
            var token = Tokens[i];
            if (token.IsLiteral)
            {
                resolved[i] = token.LiteralText ?? string.Empty;
            }
            else
            {
                var key = token.SegmentKey!;
                if (segments.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                    resolved[i] = value;
                else
                    resolved[i] = null; // omit
            }
        }

        // --- Pass 2: collapse separators adjacent to omitted segments ---
        // When a segment is omitted, we also remove the separator immediately to its
        // LEFT (the bridge between it and the preceding content).  Doing this in a
        // stable loop handles chains of omitted segments correctly.
        //
        // e.g.  seg │ [omit] │ seg  →  seg [omit] │ seg  →  "seg │ seg"  ✓
        //       seg │ [omit] · [omit] │ seg  →  seg [omit] [omit] │ seg  →  "seg │ seg" ✓
        bool changed;
        do
        {
            changed = false;
            for (var i = 1; i < resolved.Length; i++)
            {
                // If this position is a non-null separator AND the next token is omitted,
                // remove this separator.
                if (resolved[i - 1] == null) continue;       // already removed
                var prevToken = Tokens[i - 1];
                if (!prevToken.IsLiteral || !IsSeparator(resolved[i - 1]!)) continue;

                // Is the token immediately to the right of this separator omitted?
                if (i < resolved.Length && resolved[i] == null)
                {
                    resolved[i - 1] = null;
                    changed = true;
                }
            }
        } while (changed);

        // --- Pass 3: concatenate ---
        var sb = new StringBuilder();
        foreach (var part in resolved)
        {
            if (part != null)
                sb.Append(part);
        }

        return sb.ToString().Trim();
    }

    private static bool IsSeparator(string text) =>
        SeparatorPattern().IsMatch(text);
}
