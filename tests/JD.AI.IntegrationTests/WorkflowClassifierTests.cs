using System.Diagnostics;
using FluentAssertions;
using JD.AI.Workflows;
using Xunit;

namespace JD.AI.IntegrationTests;

/// <summary>
/// Tests for <see cref="TfIdfIntentClassifier"/> and the updated
/// <see cref="AgentWorkflowDetector"/> that delegates to it.
/// </summary>
public class WorkflowClassifierTests
{
    private readonly TfIdfIntentClassifier _classifier = new();

    // ── Workflow prompts (should classify as workflow) ───────────────────

    [Theory]
    [InlineData("Create a new API endpoint, write tests for it, then deploy to staging")]
    [InlineData("First set up the database, then run migrations, after that seed the data and verify")]
    [InlineData("For each microservice in the cluster, update the helm chart, redeploy, and run smoke tests")]
    [InlineData("If the build passes, deploy to staging. Otherwise, notify the team and create a bug ticket")]
    [InlineData("Build the Docker image, push to registry, update the k8s manifest, apply, and verify pods")]
    public void Workflow_Prompts_Are_Classified_As_Workflow(string prompt)
    {
        var result = _classifier.Classify(prompt);

        result.IsWorkflow.Should().BeTrue(
            because: $"'{prompt}' is a multi-step workflow prompt (confidence: {result.Confidence:F3})");
        result.Confidence.Should().BeGreaterThan(0.5);
        result.SignalWords.Should().NotBeEmpty();
    }

    // ── Conversation prompts (should NOT classify as workflow) ───────────

    [Theory]
    [InlineData("What is dependency injection?")]
    [InlineData("Explain the difference between async and parallel")]
    [InlineData("How does the garbage collector work in .NET?")]
    [InlineData("Do you think we should use PostgreSQL or SQL Server?")]
    [InlineData("Fix the null reference exception in UserService.cs")]
    [InlineData("Hello")]
    [InlineData("Thanks!")]
    public void Conversation_Prompts_Are_Not_Classified_As_Workflow(string prompt)
    {
        var result = _classifier.Classify(prompt);

        result.IsWorkflow.Should().BeFalse(
            because: $"'{prompt}' is a conversation prompt, not a workflow (confidence: {result.Confidence:F3})");
    }

    // ── Edge cases ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("Deploy the app")]
    [InlineData("Review this PR")]
    public void Single_Action_Prompts_Are_Not_Workflow(string prompt)
    {
        var result = _classifier.Classify(prompt);

        result.IsWorkflow.Should().BeFalse(
            because: $"'{prompt}' is a single action, not a multi-step workflow (confidence: {result.Confidence:F3})");
    }

    [Fact]
    public void Multi_Step_Creation_Prompt_Is_Workflow()
    {
        const string prompt =
            "Create a user registration form with validation, connect it to the API, add error handling, and write tests";

        var result = _classifier.Classify(prompt);

        result.IsWorkflow.Should().BeTrue(
            because: $"multi-step creation prompt should be classified as workflow (confidence: {result.Confidence:F3})");
        result.SignalWords.Should().NotBeEmpty();
    }

    // ── Signal words populated ──────────────────────────────────────────

    [Fact]
    public void Signal_Words_Are_Populated_For_Workflow_Prompts()
    {
        var result = _classifier.Classify(
            "First build the project, then run tests, and finally deploy to production");

        result.IsWorkflow.Should().BeTrue();
        result.SignalWords.Should().NotBeEmpty();
        result.SignalWords.Should().Contain(w =>
            w == "first" || w == "then" || w == "finally" ||
            w == "build" || w == "deploy" || w == "test");
    }

    // ── Performance ─────────────────────────────────────────────────────

    [Fact]
    public void Classification_Completes_In_Under_One_Millisecond()
    {
        const string prompt =
            "Build the Docker image, push to registry, update the k8s manifest, apply, and verify pods";

        // Warm up
        _classifier.Classify(prompt);

        var sw = Stopwatch.StartNew();
        const int iterations = 1000;
        for (int i = 0; i < iterations; i++)
            _classifier.Classify(prompt);
        sw.Stop();

        var averageMs = sw.Elapsed.TotalMilliseconds / iterations;
        averageMs.Should().BeLessThan(1.0,
            because: $"average classification should be sub-millisecond (was {averageMs:F4}ms)");
    }

    // ── Empty / null input ──────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_Or_Null_Prompts_Return_Not_Workflow(string? prompt)
    {
        var result = _classifier.Classify(prompt!);

        result.IsWorkflow.Should().BeFalse();
        result.Confidence.Should().Be(0.0);
    }

    // ── AgentWorkflowDetector integration ───────────────────────────────

    [Fact]
    public void Detector_Delegates_To_Classifier()
    {
        var detector = new AgentWorkflowDetector();

        detector.IsWorkflowRequired(new AgentRequest(
                "Create the API, write tests, then deploy to staging"))
            .Should().BeTrue();

        detector.IsWorkflowRequired(new AgentRequest("What is dependency injection?"))
            .Should().BeFalse();

        detector.IsWorkflowRequired(new AgentRequest(""))
            .Should().BeFalse();
    }
}
