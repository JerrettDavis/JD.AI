using System.Text.RegularExpressions;

namespace JD.AI.Workflows;

/// <summary>
/// TF-IDF-inspired intent classifier that determines whether a prompt describes
/// an actionable process that should be routed through the workflow pipeline.
/// Any imperative action that implies a repeatable, definable process is a workflow —
/// even single verbs like "deploy" or "review". The downstream pipeline handles
/// catalog lookup and planning mode if no workflow is defined yet.
/// Zero external dependencies, thread-safe, sub-millisecond classification.
/// </summary>
public sealed partial class TfIdfIntentClassifier : IPromptIntentClassifier
{
    // ── Category weights ────────────────────────────────────────────────
    private const double SequentialWeight = 1.5;
    private const double ConditionalWeight = 1.2;
    private const double IterationWeight = 1.3;
    private const double OrchestrationWeight = 1.0;
    private const double AntiSignalPenalty = -1.0;

    // ── Thresholds ──────────────────────────────────────────────────────
    // Aggressive: any action implying a repeatable process should route
    // through the workflow pipeline. The pipeline handles catalog lookup
    // and planning mode if no workflow exists yet.
    private const double WorkflowThreshold = 0.40;

    // ── Single imperative verb base score ───────────────────────────────
    // A single orchestration verb ("deploy", "review", "test") implies a
    // repeatable process that benefits from a defined workflow.
    private const double SingleImperativeBase = 0.5;

    // ── Multi-verb chain bonus ──────────────────────────────────────────
    private const double MultiVerbBonus = 0.25;

    // ── Vocabulary (immutable, precomputed) ─────────────────────────────
    private static readonly Dictionary<string, double> VocabularyWeights = BuildVocabulary();

    private static readonly HashSet<string> ImperativeVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "create", "build", "deploy", "test", "run", "write", "push", "pull",
        "update", "install", "configure", "set", "add", "remove", "delete",
        "migrate", "provision", "seed", "verify", "check", "apply", "start",
        "stop", "restart", "initialize", "scaffold", "generate", "compile",
        "publish", "release", "monitor", "connect", "send", "notify",
        "review", "audit", "validate", "approve", "merge", "rollback",
        "backup", "restore", "onboard", "setup", "teardown", "clean",
    };

    private static readonly string[] MultiWordSignals =
    [
        "after that", "followed by", "once done", "step 1", "step 2",
        "step 3", "in case", "depending on", "for each", "all of the",
        "each one", "set up", "what is", "how does", "thoughts on",
        "do you think", "what about",
    ];

    private static readonly Dictionary<string, double> MultiWordWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["after that"] = SequentialWeight,
        ["followed by"] = SequentialWeight,
        ["once done"] = SequentialWeight,
        ["step 1"] = SequentialWeight,
        ["step 2"] = SequentialWeight,
        ["step 3"] = SequentialWeight,
        ["in case"] = ConditionalWeight,
        ["depending on"] = ConditionalWeight,
        ["for each"] = IterationWeight,
        ["all of the"] = IterationWeight,
        ["each one"] = IterationWeight,
        ["set up"] = OrchestrationWeight,
        ["what is"] = AntiSignalPenalty,
        ["how does"] = AntiSignalPenalty,
        ["thoughts on"] = AntiSignalPenalty,
        ["do you think"] = AntiSignalPenalty,
        ["what about"] = AntiSignalPenalty,
    };

    [GeneratedRegex(@"[\w'-]+", RegexOptions.Compiled)]
    private static partial Regex TokenizerRegex();

    /// <inheritdoc/>
    public IntentClassification Classify(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return new IntentClassification(false, 0.0, []);

        var lower = prompt.ToLowerInvariant();
        var tokens = Tokenize(lower);
        var signalWords = new List<string>();
        double score = 0.0;
        int signalCount = 0;

        // ── 1. Multi-word phrase matching ────────────────────────────────
        foreach (var phrase in MultiWordSignals)
        {
            if (lower.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                score += MultiWordWeights[phrase];
                signalWords.Add(phrase);
                signalCount++;
            }
        }

        // ── 2. Single-token vocabulary matching ─────────────────────────
        foreach (var token in tokens)
        {
            if (VocabularyWeights.TryGetValue(token, out var weight))
            {
                score += weight;
                if (weight > 0)
                {
                    signalWords.Add(token);
                    signalCount++;
                }
            }
        }

        // ── 3. Imperative verb detection ────────────────────────────────
        // Any imperative verb implies an actionable process — even one.
        int verbCount = 0;
        foreach (var token in tokens)
        {
            if (ImperativeVerbs.Contains(token))
                verbCount++;
        }

        if (verbCount >= 1)
        {
            // Single imperative verb = base score (process-worthy)
            // Multiple verbs = stronger signal (multi-step)
            score += SingleImperativeBase + MultiVerbBonus * Math.Max(0, verbCount - 1);
            signalCount++;
            foreach (var token in tokens)
            {
                if (ImperativeVerbs.Contains(token) && !signalWords.Contains(token, StringComparer.OrdinalIgnoreCase))
                    signalWords.Add(token);
            }
        }

        // ── 4. Sequential connector detection ───────────────────────────
        int connectorCount = CountSequentialConnectors(lower);
        if (connectorCount > 0)
        {
            score += connectorCount * 0.3;
            signalCount++;
        }

        // ── 5. Comma-separated action chain detection ───────────────────
        int commaActions = CountCommaActionChains(lower, tokens);
        if (commaActions >= 2)
        {
            score += 0.3 * commaActions;
            signalCount++;
        }

        // ── 6. Normalize to 0.0–1.0 ────────────────────────────────────
        // Use sigmoid-like normalization: confidence = score / (score + k) for positive,
        // and clamp negatives to 0.
        double confidence;
        if (score <= 0)
        {
            confidence = 0.0;
        }
        else
        {
            // k controls the steepness — lower k means signals translate to higher confidence
            const double k = 0.65;
            confidence = score / (score + k);
        }

        confidence = Math.Clamp(confidence, 0.0, 1.0);

        bool isWorkflow = confidence >= WorkflowThreshold;

        return new IntentClassification(
            isWorkflow,
            confidence,
            signalWords.AsReadOnly());
    }

    // ── Private helpers ─────────────────────────────────────────────────

    private static string[] Tokenize(string text)
    {
        var matches = TokenizerRegex().Matches(text);
        var tokens = new string[matches.Count];
        for (int i = 0; i < matches.Count; i++)
            tokens[i] = matches[i].Value;
        return tokens;
    }

    private static int CountSequentialConnectors(string text)
    {
        int count = 0;
        ReadOnlySpan<string> connectors = ["then ", " then ", ", then", " after ", " next ", " finally "];
        foreach (var c in connectors)
        {
            if (text.Contains(c, StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    private static int CountCommaActionChains(string text, string[] tokens)
    {
        // Count comma-separated clauses that start with imperative verbs
        var clauses = text.Split(',', StringSplitOptions.TrimEntries);
        int actionClauses = 0;
        foreach (var clause in clauses)
        {
            var clauseTokens = Tokenize(clause);
            if (clauseTokens.Length > 0 && ImperativeVerbs.Contains(clauseTokens[0]))
                actionClauses++;
        }
        return actionClauses;
    }

    private static Dictionary<string, double> BuildVocabulary()
    {
        var vocab = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // Sequential markers
        foreach (var word in new[] { "then", "next", "finally", "first", "afterwards", "subsequently" })
            vocab[word] = SequentialWeight;

        // Conditional
        foreach (var word in new[] { "if", "when", "unless", "otherwise" })
            vocab[word] = ConditionalWeight;

        // Iteration
        foreach (var word in new[] { "each", "every", "repeat", "loop" })
            vocab[word] = IterationWeight;

        // Orchestration
        foreach (var word in new[] {
            "deploy", "build", "test", "migrate", "pipeline", "workflow",
            "automate", "configure", "provision", "scaffold", "initialize" })
            vocab[word] = OrchestrationWeight;

        // Anti-signals (single-word)
        foreach (var word in new[] { "explain", "describe", "why", "hello", "thanks", "hi", "hey" })
            vocab[word] = AntiSignalPenalty;

        return vocab;
    }
}
