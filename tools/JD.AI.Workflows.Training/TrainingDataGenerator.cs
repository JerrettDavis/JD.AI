using System.Text.RegularExpressions;

namespace JD.AI.Workflows.Training;

/// <summary>
/// Generates synthetic labeled prompt pairs for training the ML.NET intent classifier.
/// Uses a seed corpus from real prompt examples and applies controlled augmentation
/// to produce a diverse, well-labeled training set.
/// </summary>
public static class TrainingDataGenerator
{
    /// <summary>A labeled prompt for training.</summary>
    public sealed record LabeledPrompt(string Prompt, bool IsWorkflow);

    // ── Seed corpus ─────────────────────────────────────────────────────

    private static readonly string[] WorkflowSeeds =
    [
        // Multi-step chains
        "Create a new API endpoint, write tests for it, then deploy to staging",
        "First set up the database, then run migrations, after that seed the data and verify",
        "For each microservice in the cluster, update the helm chart, redeploy, and run smoke tests",
        "If the build passes, deploy to staging. Otherwise, notify the team and create a bug ticket",
        "Build the Docker image, push to registry, update the k8s manifest, apply, and verify pods",
        "Create the API, write tests, then deploy to staging",
        "First build the project, then run tests, and finally deploy to production",
        "Create a user registration form with validation, connect it to the API, add error handling, and write tests",

        // Single-imperative (process-worthy)
        "Deploy the app",
        "Review this PR",
        "Onboard the new hire",
        "Rollback the release",
        "Backup the database",
        "Provision the dev environment",
        "Run the full test suite",
        "Initialize the CI pipeline",
        "Scaffold the new microservice",
        "Bootstrap the ML training job",
        "Monitor the production metrics",
        "Notify the team on Slack",
        "Audit the access logs",
        "Verify the SSL certificates",
        "Seed the database with test data",
        "Migrate the legacy schema",
        "Refactor the auth module",
        "Publish the NuGet package",
        "Clean up the old branches",
        "Debug the memory leak",

        // Conditional
        "If the CI build fails, revert the commit and alert the team",
        "When the deploy completes, run the smoke tests and notify on failure",
        "Unless the tests pass, don't merge the PR",
        "Depending on the environment, use the staging or production config",
        "If the user is admin, show the dashboard, otherwise show the public page",

        // Iterative
        "For each file in the output directory, compress it and upload it to S3",
        "Run the linter on all modified files and fix any warnings",
        "Check each dependency for security vulnerabilities and update if needed",
        "Validate all incoming webhooks, log failures, and retry on transient errors",

        // Sequential creation
        "Create the repository, set up the CI, add a README, and configure branch protection",
        "Design the database schema, write the migrations, generate the entities, and add the repositories",
        "Build the frontend components, wire them to the API, add routing, and deploy to the CDN",
        "Set up monitoring, configure alerts, set up dashboards, and add runbooks",
    ];

    // ── Real examples from JD's Discord sessions ──────────────────────
    // Manually labeled from OpenClaw session transcripts (0c4ef8fc)
    // Labeling: multi-step / coding task = workflow; question / chat = conversation

    private static readonly string[] RealWorkflowSeeds =
    [
        "pull gh issue #423 from jd.ai. We have the repo is c:\\git\\jd.ai. We need to make sure to use our own worktree before merging back to main",
        "Fix the PR validation errors: https://github.com/JerrettDavis/JD.AI/actions/runs/...",
        "remove the tmp-* files from the repo root and commit to main in jd.ai. Get it pushed",
        "validate that https://github.com/JerrettDavis/JD.AI/issues/399 has not already been implemented. If it has, validate it's feature complete and close the PR. If it's not yet complete, review the PR, review the codebase, create a plan to implement the feature, create structured todos, and knock it out in a dedicated worktree",
        "Let's start work on #405. We can review, plan and see what we can accomplish in our worktree. We want to open a PR with our work",
        "start work on issue #404. We want to develop the ML pathway and the training tooling to make it work properly",
        "review, plan, and start on issue #404. You can come back and ask questions if guidance is needed",
        "close the associated issue if we're complete",
        "build a new feature that does X, add tests, and get it ready for review",
        "create a dedicated worktree off of the latest remote main for this feature work",
        "Ensure all tests, linters, and analyzers pass. Get a PR open and all workflows green",
    ];

    private static readonly string[] RealConversationSeeds =
    [
        "introduce yourself",
        "what do you think about microservices vs monolith?",
        "thanks for the explanation",
        "can you help me with something?",
    ];

    private static readonly string[] ConversationSeeds =
    [
        // Questions
        "What is dependency injection?",
        "Explain the difference between async and parallel",
        "How does the garbage collector work in .NET?",
        "Do you think we should use PostgreSQL or SQL Server?",
        "What are the best practices for REST API design?",
        "Can you explain how Kubernetes scheduling works?",
        "Why would I choose gRPC over REST?",
        "What is the CAP theorem and how does it affect database choice?",

        // Casual / social
        "Hello",
        "Thanks!",
        "Hey there",
        "Good morning",
        "Can you help me with something?",
        "What do you think about this approach?",
        "Thoughts on the new architecture?",
        "What about using CQRS here?",

        // Opinion / brainstorming
        "What is your opinion on microservices vs monolith?",
        "Should we use event sourcing for this feature?",
        "Do you think this is a good use of blockchain?",
        "What design pattern would you use here?",
        "How would you structure this team?",
        "What are the tradeoffs of this approach?",

        // Information seeking
        "What is new in .NET 10?",
        "How does the new caching middleware work?",
        "Tell me about the changes to the workflow engine",
        "What does the update contain?",
    ];

    // ── Augmentation rules ──────────────────────────────────────────────

    // Imperative verb synonyms for augmentation
    private static readonly Dictionary<string, string[]> ImperativeSynonyms = new()
    {
        ["create"] = ["make", "build", "add", "generate", "set up"],
        ["build"] = ["compile", "assemble", "construct"],
        ["deploy"] = ["release", "push", "ship", "publish"],
        ["test"] = ["verify", "check", "validate", "run tests for"],
        ["run"] = ["execute", "start", "kick off"],
        ["write"] = ["author", "compose", "add"],
        ["push"] = ["upload", "send", "publish"],
        ["update"] = ["upgrade", "refresh", "modify"],
        ["install"] = ["set up", "configure", "add"],
        ["configure"] = ["set up", "adjust", "tune", "customize"],
        ["add"] = ["include", "insert", "introduce"],
        ["remove"] = ["delete", "uninstall", "strip"],
        ["delete"] = ["remove", "drop", "erase"],
        ["migrate"] = ["transfer", "move", "convert"],
        ["provision"] = ["set up", "allocate", "create"],
        ["seed"] = ["populate", "initialize", "load"],
        ["verify"] = ["confirm", "check", "validate", "ensure"],
        ["check"] = ["inspect", "review", "audit"],
        ["apply"] = ["execute", "run", "implement"],
        ["start"] = ["launch", "begin", "initiate"],
        ["stop"] = ["halt", "end", "terminate"],
        ["restart"] = ["reboot", "reload", "relaunch"],
        ["initialize"] = ["set up", "bootstrap", "configure"],
        ["scaffold"] = ["bootstrap", "generate", "set up"],
        ["generate"] = ["produce", "create", "make"],
        ["compile"] = ["build", "assemble"],
        ["publish"] = ["release", "push", "distribute"],
        ["monitor"] = ["track", "watch", "observe"],
        ["notify"] = ["alert", "tell", "message"],
        ["review"] = ["examine", "assess", "evaluate"],
        ["audit"] = ["inspect", "review", "examine"],
        ["validate"] = ["verify", "confirm", "check"],
        ["approve"] = ["accept", "endorse", "confirm"],
        ["merge"] = ["combine", "join", "integrate"],
        ["rollback"] = ["revert", "undo", "restore"],
        ["backup"] = ["save", "archive", "copy"],
        ["restore"] = ["recover", "rebuild", "revert"],
        ["onboard"] = ["set up", "configure", "add"],
        ["setup"] = ["configure", "initialize", "prepare"],
        ["clean"] = ["remove", "clear", "purge"],
        ["implement"] = ["add", "build", "create"],
        ["design"] = ["architect", "plan", "outline"],
        ["architect"] = ["design", "plan", "structure"],
        ["develop"] = ["build", "create", "implement"],
        ["refactor"] = ["restructure", "rewrite", "clean up"],
        ["integrate"] = ["combine", "merge", "connect"],
        ["bootstrap"] = ["set up", "initialize", "start"],
        ["analyze"] = ["examine", "study", "assess"],
        ["debug"] = ["fix", "investigate", "troubleshoot"],
        ["fix"] = ["repair", "correct", "resolve"],
    };

    // Sequential connectors for augmentation
    private static readonly string[][] SequentialConnectorPairs =
    [
        ["then", "and then", "followed by", "after that", "once done"],
        ["next", "afterwards", "subsequently", "then", "and next"],
        ["finally", "lastly", "in the end", "to finish", "as a last step"],
    ];

    // Prefix templates
    private static readonly string[] WorkflowPrefixes =
    [
        "Please ",
        "Can you ",
        "I need you to ",
        "Could you ",
        "Go ahead and ",
        "Start by ",
        "",
    ];

    // Suffix templates
    private static readonly string[] WorkflowSuffixes =
    [
        " please",
        " if you can",
        "",
        " and let me know when done",
        " automatically",
    ];

    /// <summary>
    /// Generates a training dataset of labeled prompts.
    /// </summary>
    /// <param name="augmentPerSeed">Number of augmented variants to produce per seed prompt.</param>
    public static List<LabeledPrompt> Generate(int augmentPerSeed = 10)
    {
        var prompts = new List<LabeledPrompt>();

        foreach (var seed in WorkflowSeeds)
        {
            prompts.Add(new LabeledPrompt(seed, IsWorkflow: true));
            prompts.AddRange(AugmentWorkflow(seed, augmentPerSeed));
        }

        foreach (var seed in RealWorkflowSeeds)
        {
            prompts.Add(new LabeledPrompt(seed, IsWorkflow: true));
            prompts.AddRange(AugmentWorkflow(seed, augmentPerSeed / 2));
        }

        foreach (var seed in ConversationSeeds)
        {
            prompts.Add(new LabeledPrompt(seed, IsWorkflow: false));
            prompts.AddRange(AugmentConversation(seed, Math.Max(3, augmentPerSeed / 3)));
        }

        foreach (var seed in RealConversationSeeds)
        {
            prompts.Add(new LabeledPrompt(seed, IsWorkflow: false));
            prompts.AddRange(AugmentConversation(seed, Math.Max(2, augmentPerSeed / 4)));
        }

        return prompts;
    }

    /// <summary>
    /// Augments a workflow seed into multiple variants.
    /// </summary>
    private static IEnumerable<LabeledPrompt> AugmentWorkflow(string seed, int count)
    {
        var results = new List<LabeledPrompt>();
        var rng = new Random(GetStableHash(seed));

        for (var i = 0; i < count; i++)
        {
            var variant = ApplyRandomAugmentation(seed, rng);
            if (!string.Equals(variant, seed, StringComparison.OrdinalIgnoreCase))
                results.Add(new LabeledPrompt(variant, IsWorkflow: true));
        }

        return results;
    }

    /// <summary>
    /// Augments a conversation seed into mild variants (different phrasing, not adding workflow signals).
    /// </summary>
    private static IEnumerable<LabeledPrompt> AugmentConversation(string seed, int count)
    {
        var results = new List<LabeledPrompt>();
        var rng = new Random(GetStableHash(seed));

        for (var i = 0; i < count; i++)
        {
            var variant = ApplyConversationAugmentation(seed, rng);
            if (!string.Equals(variant, seed, StringComparison.OrdinalIgnoreCase))
                results.Add(new LabeledPrompt(variant, IsWorkflow: false));
        }

        return results;
    }

    private static string ApplyRandomAugmentation(string prompt, Random rng)
    {
        var result = prompt;

        // 1. Synonym replacement for imperative verbs
        foreach (var (verb, synonyms) in ImperativeSynonyms)
        {
            var pattern = $@"\b{Regex.Escape(verb)}\b";
            result = Regex.Replace(result, pattern, _ => synonyms[rng.Next(synonyms.Length)], RegexOptions.IgnoreCase);
        }

        // 2. Swap sequential connectors
        foreach (var group in SequentialConnectorPairs)
        {
            foreach (var connector in group)
            {
                if (result.Contains(connector, StringComparison.OrdinalIgnoreCase))
                {
                    var replacement = group[rng.Next(group.Length)];
                    result = Regex.Replace(
                        result, Regex.Escape(connector),
                        m => m.Index == 0 ? char.ToUpper(replacement[0]) + replacement[1..] : replacement,
                        RegexOptions.IgnoreCase);
                    break;
                }
            }
        }

        // 3. Add / change prefix
        if (rng.NextDouble() > 0.5)
        {
            var prefix = WorkflowPrefixes[rng.Next(WorkflowPrefixes.Length)];
            if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                result = prefix + char.ToLower(result[0]) + result[1..];
        }

        // 4. Add / change suffix
        if (rng.NextDouble() > 0.6)
        {
            var suffix = WorkflowSuffixes[rng.Next(WorkflowSuffixes.Length)];
            if (!result.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                result = result.TrimEnd() + suffix;
        }

        // 5. Comma-chain variation
        if (result.Contains(',') && rng.NextDouble() > 0.5)
        {
            // Replace comma-separated clauses with "then" and vice versa
            var commaCount = result.Count(c => c == ',');
            if (commaCount >= 2)
            {
                // Replace some commas with " and then"
                result = Regex.Replace(result, @",\s*", rng.NextDouble() > 0.5 ? " and then " : ", ");
            }
        }

        return result.Trim();
    }

    private static string ApplyConversationAugmentation(string prompt, Random rng)
    {
        var result = prompt;

        // Mild rephrasing: change question structure slightly
        if (prompt.Contains("What is", StringComparison.OrdinalIgnoreCase))
        {
            if (rng.NextDouble() > 0.5)
                result = Regex.Replace(result, @"^What is", "Could you explain what is", RegexOptions.IgnoreCase);
        }
        else if (prompt.Contains("How does", StringComparison.OrdinalIgnoreCase))
        {
            if (rng.NextDouble() > 0.5)
                result = Regex.Replace(result, @"^How does", "Can you explain how does", RegexOptions.IgnoreCase);
        }
        else if (prompt.Contains("Do you think", StringComparison.OrdinalIgnoreCase))
        {
            if (rng.NextDouble() > 0.5)
                result = Regex.Replace(result, @"Do you think", "What's your take on", RegexOptions.IgnoreCase);
        }

        return result.Trim();
    }

    /// <summary>
    /// Writes training data to a JSON Lines file (one JSON object per line).
    /// This avoids CSV quoting issues with commas in prompts.
    /// </summary>
    public static void WriteCsv(IEnumerable<LabeledPrompt> prompts, string path)
    {
        using var writer = new StreamWriter(path, append: false);
        foreach (var p in prompts)
        {
            var promptJson = System.Text.Json.JsonSerializer.Serialize(p.Prompt);
            writer.WriteLine($"{{\"Prompt\":{promptJson},\"IsWorkflow\":{p.IsWorkflow.ToString().ToLowerInvariant()}}}");
        }
    }

    /// <summary>Reads training data from a JSON Lines file.</summary>
    public static List<LabeledPrompt> ReadCsv(string path)
    {
        var prompts = new List<LabeledPrompt>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = System.Text.Json.JsonDocument.Parse(line);
            var prompt = doc.RootElement.GetProperty("Prompt").GetString() ?? "";
            var isWorkflow = doc.RootElement.GetProperty("IsWorkflow").GetBoolean();
            prompts.Add(new LabeledPrompt(prompt, isWorkflow));
        }
        return prompts;
    }

    private static int GetStableHash(string input)
    {
        var hash = new HashCode();
        hash.Add(input);
        return hash.ToHashCode();
    }
}
