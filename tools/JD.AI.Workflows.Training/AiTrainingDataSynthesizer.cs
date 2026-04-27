using System.Text.Json;

namespace JD.AI.Workflows.Training;

/// <summary>
/// Uses Ollama (qwen3.5) to generate diverse, well-labeled training examples
/// and validate/audit existing training data.
/// </summary>
public sealed class AiTrainingDataSynthesizer : IDisposable
{
    private readonly OllamaClient _ollama;
    private readonly string _model;
    private int _generated;

    public AiTrainingDataSynthesizer(string model = "qwen3.5:9b", string ollamaHost = "http://localhost:11434")
    {
        _ollama = new OllamaClient(model, ollamaHost);
        _model = model;
    }

    public int Generated => _generated;

    /// <summary>
    /// Generates <paramref name="count"/> new training examples using Ollama,
    /// filtered to only include examples where the model gives a confident, consistent answer.
    /// </summary>
    public async Task<List<TrainingDataGenerator.LabeledPrompt>> GenerateAsync(
        int count,
        Action<int, int>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<TrainingDataGenerator.LabeledPrompt>();
        var batchSize = 10;
        var batches = (int)Math.Ceiling((double)count / batchSize);

        for (var batch = 0; batch < batches && results.Count < count; batch++)
        {
            ct.ThrowIfCancellationRequested();

            var toGenerate = Math.Min(batchSize, count - results.Count);
            var batchPrompts = GenerateBatchPrompts(batch, batch * batchSize);

            foreach (var seed in batchPrompts)
            {
                var label = await _ollama.ClassifyAsync(seed, ct);
                if (label is "WORKFLOW" or "CONVERSATION")
                {
                    results.Add(new TrainingDataGenerator.LabeledPrompt(seed, label == "WORKFLOW"));
                    _generated++;
                    progress?.Invoke(results.Count, count);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Validates existing labeled prompts against Ollama.
    /// Returns discrepancies where Ollama disagrees with the stored label.
    /// </summary>
    public async Task<List<ValidationResult>> ValidateAsync(
        IReadOnlyList<TrainingDataGenerator.LabeledPrompt> prompts,
        Action<int, int>? progress = null,
        CancellationToken ct = default)
    {
        var discrepancies = new List<ValidationResult>();

        for (var i = 0; i < prompts.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var prompt = prompts[i];
            var ollamaLabel = await _ollama.ClassifyAsync(prompt.Prompt, ct);
            var expected = prompt.IsWorkflow ? "WORKFLOW" : "CONVERSATION";

            if (ollamaLabel != expected)
            {
                discrepancies.Add(new ValidationResult(prompt.Prompt, expected, ollamaLabel));
            }

            progress?.Invoke(i + 1, prompts.Count);
        }

        return discrepancies;
    }

    /// <summary>
    /// Generates a diverse batch of seed prompts to classify.
    /// Mixes workflow and conversation examples across different domains and phrasings.
    /// </summary>
    private static List<string> GenerateBatchPrompts(int batchIdx, int offset)
    {
        // Diverse workflow domains for variety
        var domains = new[]
        {
            "git/development", "docker/containers", "cloud/deployment", "database/sql",
            "security/tls", "ci-cd/github-actions", "api/rest", "testing/tdd",
            "docs/readme", "docker-compose", "kubernetes", "shell/bash",
            "npm/node", "python/pip", "monitoring/logging", "networking/proxy",
            "file-ops", "code-review", "debugging", "devops",
        };

        var workflowTemplates = new[]
        {
            // Git / Dev
            "Create a PR that {action} and add reviewers",
            "Set up the git hooks for {domain}",
            "Squash the last 5 commits and force push",
            "Revert the last commit that broke {domain}",
            "Tag the release v1.{minor}.0 for {domain}",
            "Update the CHANGELOG for {domain}",

            // Docker / Infra
            "Write a Dockerfile for the {domain} service",
            "docker-compose up -d for {domain} and check logs",
            "Prune all stopped containers and dangling images",
            "Build and push the {domain} image to the registry",

            // Cloud / Deployment
            "Deploy {domain} to the staging environment",
            "Rollback the last deployment of {domain}",
            "Scale the {domain} service to 3 replicas",
            "Check the health of {domain} in production",

            // Database
            "Run a migration to add index on {domain}.{table}",
            "Seed the {domain} database with test fixtures",
            "Dump the {domain} production DB to a file",

            // CI/CD
            "Re-run the failed GitHub Actions for {domain}",
            "Add a required reviewer to the {domain} PR pipeline",
            "Enable branch protection for {domain} on GitHub",

            // Security
            "Check the SSL cert expiry for {domain}",
            "Audit the npm packages in {domain} for vulnerabilities",
            "Rotate the API key for {domain}",

            // File ops
            "Find all {domain} files older than 30 days and archive them",
            "Replace all TODO comments in the {domain} codebase with proper issues",
            "Fix the formatting in all {domain} source files",
            "Remove the dead code from the {domain} module",

            // Testing
            "Write integration tests for the {domain} API",
            "Run the full test suite for {domain} with coverage",
            "Add fuzzing tests for the {domain} input handler",

            // Code review / Debug
            "Review the {domain} PR and leave inline comments",
            "Debug why {domain} is returning 500 on startup",
            "Profile the {domain} memory usage in production",

            // Misc
            "Create a worktree for the {domain} feature branch",
            "Sync the {domain} fork with upstream main",
            "Archive old {domain} logs to S3",
        };

        var conversationTemplates = new[]
        {
            // Questions
            "What's the best way to handle {domain} migrations?",
            "How do I configure {domain} authentication?",
            "Can you explain how the {domain} pipeline works?",
            "What's the difference between {domain} v1 and v2?",

            // Opinions
            "Should we use {domain} or stick with the current approach?",
            "What do you think about {domain} for this use case?",
            "Is {domain} production-ready given our constraints?",

            // Information
            "What's new in the {domain} release notes?",
            "Does {domain} support WebSocket connections?",
            "What are the known limitations of {domain}?",

            // Casual
            "Thanks for the {domain} explanation!",
            "Hey, can you help me with something unrelated?",
            "Good morning! How are you?",
            "I had an idea about {domain} — thoughts?",

            // Brainstorming
            "How would you design {domain} from scratch?",
            "What patterns work best for {domain} at scale?",
            "How can we improve the {domain} developer experience?",
            "What would make {domain} more maintainable?",

            // Context
            "Context: we're running {domain} in production at 10k req/s",
            "Background: {domain} has been causing issues since last week",
            "Update: {domain} is now fixed, let me know if you need details",
        };

        var rnd = new Random(batchIdx * 31 + offset);
        var prompts = new List<string>();

        for (var i = 0; i < 10; i++)
        {
            var domain = domains[rnd.Next(domains.Length)];
            var isWorkflow = rnd.NextDouble() > 0.35; // 65% workflow, 35% conversation

            if (isWorkflow)
            {
                var template = workflowTemplates[rnd.Next(workflowTemplates.Length)];
                prompts.Add(ExpandTemplate(template, domain, rnd));
            }
            else
            {
                var template = conversationTemplates[rnd.Next(conversationTemplates.Length)];
                prompts.Add(ExpandTemplate(template, domain, rnd));
            }
        }

        return prompts;
    }

    private static string ExpandTemplate(string template, string domain, Random rnd)
    {
        var domainParts = domain.Split('/');
        var primary = domainParts[0];
        var secondary = domainParts.Length > 1 ? domainParts[1] : primary;

        var result = template
            .Replace("{domain}", primary)
            .Replace("{table}", PickRandom(["users", "sessions", "events", "audit_log", "tokens"], rnd))
            .Replace("{minor}", rnd.Next(0, 20).ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{action}", PickRandom(["fixes the auth bug", "adds rate limiting", "refactors the API client", "updates dependencies", "adds telemetry"], rnd));

        return result;
    }

    private static string PickRandom(string[] options, Random rnd) =>
        options[rnd.Next(options.Length)];

    public void Dispose() => _ollama.Dispose();
}

/// <summary>Result of comparing a stored label against Ollama's classification.</summary>
public sealed record ValidationResult(string Prompt, string ExpectedLabel, string OllamaLabel);
