using FluentAssertions;
using JD.AI.Workflows;
using Microsoft.ML;
using Microsoft.ML.Data;
using Xunit;

namespace JD.AI.Tests.Workflows;

/// <summary>
/// Unit tests for <see cref="MlNetIntentClassifier"/>.
/// A compact model is trained in-memory once per test class and used
/// across all tests to validate classification behaviour.
/// </summary>
[Trait("Category", "MlModel")]
public class MlNetIntentClassifierTests : IDisposable
{
    // Training data — small but diverse, enough to learn the distinction
    private static readonly (string Prompt, bool IsWorkflow)[] TrainingData =
    [
        // Workflow — multi-step
        ("Create the API, write tests, then deploy to staging", true),
        ("First build the Docker image, push to registry, update k8s manifest, apply changes", true),
        ("For each service, run tests, deploy, and verify health", true),
        ("If CI passes, merge and deploy; otherwise notify the team", true),
        ("Set up the database, run migrations, seed data, verify schema", true),
        ("Add a feature flag, update the config, deploy to staging, run smoke tests", true),
        ("Write the migration, apply it, verify with a test query", true),
        ("Create a worktree, implement the feature, open a PR", true),
        ("Initialize the repo, add CI, configure branch protection, add README", true),
        ("Run the linter, fix warnings, run tests, push", true),

        // Workflow — single imperative (process-worthy)
        ("Deploy the app", true),
        ("Review this PR", true),
        ("Rollback the release", true),
        ("Backup the database", true),
        ("Run the full test suite", true),
        ("Seed the database", true),
        ("Audit the access logs", true),
        ("Verify the SSL certificates", true),
        ("Migrate the schema", true),
        ("Publish the NuGet package", true),

        // Conversation — questions
        ("What is dependency injection?", false),
        ("Explain the difference between async and parallel", false),
        ("How does the garbage collector work in .NET?", false),
        ("Can you explain how Kubernetes scheduling works?", false),
        ("Why would I choose gRPC over REST?", false),
        ("What is the CAP theorem?", false),
        ("Do you think we should use PostgreSQL or SQL Server?", false),
        ("What are the best practices for REST API design?", false),
        ("What is new in .NET 10?", false),
        ("What does the update contain?", false),

        // Conversation — casual / social
        ("Hello", false),
        ("Thanks!", false),
        ("Good morning", false),
        ("Can you help me with something?", false),
        ("Hey there", false),

        // Conversation — opinion / brainstorming
        ("What's your opinion on microservices vs monolith?", false),
        ("Should we use event sourcing for this feature?", false),
        ("What design pattern would you use here?", false),
        ("How would you structure this team?", false),
        ("What are the tradeoffs of this approach?", false),

        // Conversation — information seeking
        ("Tell me about the changes to the workflow engine", false),
        ("What do you think about this approach?", false),
        ("Thanks for the explanation", false),
        ("I had an idea about this", false),
    ];

    private readonly MlNetIntentClassifier _classifier;
    private readonly string _modelPath;

    public MlNetIntentClassifierTests()
    {
        _modelPath = Path.Combine(Path.GetTempPath(), $"ml_test_{Guid.NewGuid():N}.zip");

        // Train a compact model for testing — use all data (no TrainTestSplit to avoid temp dir)
        TrainModel(_modelPath);

        _classifier = new MlNetIntentClassifier(_modelPath);
    }

    private static void TrainModel(string modelPath)
    {
        var ml = new MLContext(seed: 42);

        var data = ml.Data.LoadFromEnumerable(
            TrainingData.Select(d => new PromptInput { Prompt = d.Prompt, IsWorkflow = d.IsWorkflow }));

        // Fit on all training data (no split — avoids ML.NET temp directory creation in test runner)
        var pipeline = ml.Transforms.Text
            .FeaturizeText("Features", nameof(PromptInput.Prompt))
            .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: nameof(PromptInput.IsWorkflow),
                featureColumnName: "Features",
                maximumNumberOfIterations: 50));

        var model = pipeline.Fit(data);

        using var stream = File.Create(modelPath);
        ml.Model.Save(model, data.Schema, stream);
    }

    // ML.NET schemas (same as in MlNetIntentClassifier)
    private sealed class PromptInput
    {
        [LoadColumn(0)]
        public string Prompt = "";
        [LoadColumn(1)]
        public bool IsWorkflow;
    }

    // ── Workflow prompts ────────────────────────────────────────────────

    [Theory]
    [InlineData("Create the API, write tests, then deploy to staging")]
    [InlineData("First build the Docker image, push to registry, update k8s manifest")]
    [InlineData("Run the linter, fix warnings, run tests, push")]
    [InlineData("Initialize the repo, add CI, configure branch protection")]
    [InlineData("Write the migration, apply it, verify with a test query")]
    public void Workflow_MultiStep_Prompts_Are_Classified_As_Workflow(string prompt)
    {
        var result = _classifier.Classify(prompt);
        result.IsWorkflow.Should().BeTrue(
            $"'{prompt}' is a multi-step workflow (confidence: {result.Confidence:F3})");
    }

    [Theory]
    [InlineData("Deploy the app")]
    [InlineData("Review this PR")]
    [InlineData("Rollback the release")]
    [InlineData("Run the full test suite")]
    [InlineData("Backup the database")]
    [InlineData("Seed the database")]
    [InlineData("Audit the access logs")]
    public void Workflow_ProcessWorthy_SingleActions_Are_Classified_As_Workflow(string prompt)
    {
        var result = _classifier.Classify(prompt);
        result.IsWorkflow.Should().BeTrue(
            $"'{prompt}' is a process-worthy action (confidence: {result.Confidence:F3})");
    }

    // ── Conversation prompts ───────────────────────────────────────────

    [Theory]
    [InlineData("What is dependency injection?")]
    [InlineData("Explain the difference between async and parallel")]
    [InlineData("How does the garbage collector work in .NET?")]
    [InlineData("Why would I choose gRPC over REST?")]
    [InlineData("What is the CAP theorem?")]
    [InlineData("What are the best practices for REST API design?")]
    public void Conversation_Questions_Are_Not_Classified_As_Workflow(string prompt)
    {
        var result = _classifier.Classify(prompt);
        result.IsWorkflow.Should().BeFalse(
            $"'{prompt}' is a question (confidence: {result.Confidence:F3})");
    }

    [Theory]
    [InlineData("Hello")]
    [InlineData("Thanks!")]
    [InlineData("Good morning")]
    [InlineData("Can you help me with something?")]
    [InlineData("Hey there")]
    public void Conversation_Casual_Are_Not_Classified_As_Workflow(string prompt)
    {
        var result = _classifier.Classify(prompt);
        result.IsWorkflow.Should().BeFalse(
            $"'{prompt}' is casual chat (confidence: {result.Confidence:F3})");
    }

    [Theory]
    [InlineData("What's your opinion on microservices vs monolith?")]
    [InlineData("Should we use event sourcing for this feature?")]
    [InlineData("What design pattern would you use here?")]
    public void Conversation_OpinionBrainstorm_Are_Not_Classified_As_Workflow(string prompt)
    {
        var result = _classifier.Classify(prompt);
        result.IsWorkflow.Should().BeFalse(
            $"'{prompt}' is opinion-seeking (confidence: {result.Confidence:F3})");
    }

    // ── Edge cases ────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_Or_Whitespace_Prompts_Return_NotWorkflow(string prompt)
    {
        var result = _classifier.Classify(prompt);
        result.IsWorkflow.Should().BeFalse();
        result.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void Null_Prompt_Returns_NotWorkflow()
    {
        var result = _classifier.Classify(null!);
        result.IsWorkflow.Should().BeFalse();
        result.Confidence.Should().Be(0.0);
    }

    // ── Confidence ───────────────────────────────────────────────────

    [Fact]
    public void Confidence_Is_Clamped_Between_Zero_And_One()
    {
        var prompts = TrainingData.Select(d => d.Prompt).Take(10);
        foreach (var prompt in prompts)
        {
            var result = _classifier.Classify(prompt);
            result.Confidence.Should().BeGreaterThanOrEqualTo(0.0);
            result.Confidence.Should().BeLessThanOrEqualTo(1.0);
        }
    }

    [Fact]
    public void IsModelLoaded_Returns_True_When_Model_Exists()
    {
        _classifier.IsModelLoaded.Should().BeTrue();
        _classifier.ModelPath.Should().Be(_modelPath);
    }

    // ── Hot-swap ─────────────────────────────────────────────────────

    [Fact]
    public void ReloadModel_Loads_Updated_Model_Without_Recreating_Classifier()
    {
        // Train two models with swapped labels
        var ml = new MLContext(seed: 99);
        var schema = ml.Data.LoadFromEnumerable(
            new[] { new PromptInput { Prompt = "Deploy the app", IsWorkflow = true } });

        var pipe = ml.Transforms.Text
            .FeaturizeText("Features", nameof(PromptInput.Prompt))
            .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: nameof(PromptInput.IsWorkflow),
                featureColumnName: "Features",
                maximumNumberOfIterations: 50));

        var model1Path = Path.Combine(Path.GetTempPath(), $"ml_swap1_{Guid.NewGuid():N}.zip");
        var model2Path = Path.Combine(Path.GetTempPath(), $"ml_swap2_{Guid.NewGuid():N}.zip");

        using (var s = File.Create(model1Path))
            ml.Model.Save(pipe.Fit(schema), schema.Schema, s);
        using (var s = File.Create(model2Path))
        {
            // Flip the label so the result differs
            var flipped = ml.Data.LoadFromEnumerable(
                new[] { new PromptInput { Prompt = "Deploy the app", IsWorkflow = false } });
            ml.Model.Save(pipe.Fit(flipped), flipped.Schema, s);
        }

        var classifier = new MlNetIntentClassifier(model1Path);
        classifier.IsModelLoaded.Should().BeTrue();

        // FileSystem watcher is not available — directly reload via path
        File.Copy(model2Path, model1Path, overwrite: true);
        classifier.ReloadModel();

        classifier.IsModelLoaded.Should().BeTrue("model should reload successfully");

        // Cleanup
        File.Delete(model1Path);
        File.Delete(model2Path);
    }

    [Fact]
    public void Dispose_Does_Not_Throw()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ml_discard_{Guid.NewGuid():N}.zip");
        var ml = new MLContext(seed: 1);
        var data = ml.Data.LoadFromEnumerable(
            new[] { new PromptInput { Prompt = "Deploy", IsWorkflow = true } });
        using (var s = File.Create(path))
            ml.Model.Save(ml.Transforms.Text
                .FeaturizeText("Features", nameof(PromptInput.Prompt))
                .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                    labelColumnName: nameof(PromptInput.IsWorkflow),
                    featureColumnName: "Features",
                    maximumNumberOfIterations: 10))
                .Fit(data), data.Schema, s);

        var classifier = new MlNetIntentClassifier(path);
        var act = () => classifier.Dispose();
        act.Should().NotThrow();
        File.Delete(path);
    }

    // ── Performance ───────────────────────────────────────────────────

    [Fact]
    public void Classification_Is_SubMillisecond()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int Iterations = 100;
        for (var i = 0; i < Iterations; i++)
            _classifier.Classify("Deploy the app, run tests, verify in staging");
        sw.Stop();

        (sw.Elapsed.TotalMilliseconds / Iterations).Should().BeLessThan(1.0,
            "ML.NET prediction should be sub-millisecond");
    }

    public void Dispose()
    {
        _classifier.Dispose();
        if (File.Exists(_modelPath))
            File.Delete(_modelPath);
    }
}
