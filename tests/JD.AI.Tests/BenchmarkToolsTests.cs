using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests;

public class BenchmarkToolsTests
{
    private static Kernel CreateKernelWithTools()
    {
        var builder = Kernel.CreateBuilder();
        var kernel = builder.Build();

        // Register representative tools matching canonical registry
        kernel.Plugins.AddFromType<FakeFileTools>("file");
        kernel.Plugins.AddFromType<FakeGitTools>("git");
        kernel.Plugins.AddFromType<FakeSearchTools>("search");
        kernel.Plugins.AddFromType<FakeMetaTools>("meta");
        kernel.Plugins.AddFromObject(new BenchmarkTools(kernel), "benchmark");
        return kernel;
    }

    // ── benchmark_scorecard ──────────────────────────────────

    [Fact]
    public void Scorecard_ReturnsFormattedReport()
    {
        var kernel = CreateKernelWithTools();
        var tools = new BenchmarkTools(kernel);

        var result = tools.GenerateScorecard();

        Assert.Contains("## JD.AI Parity Scorecard", result);
        Assert.Contains("**Overall:", result);
        Assert.Contains("**Tools registered:", result);
    }

    [Fact]
    public void Scorecard_ShowsCoveredCapabilities()
    {
        var kernel = CreateKernelWithTools();
        var tools = new BenchmarkTools(kernel);

        var result = tools.GenerateScorecard();

        // read_file is registered, so file.read should be covered
        Assert.Contains("✓ `file.read`", result);
    }

    [Fact]
    public void Scorecard_ShowsMissingCapabilities()
    {
        var kernel = CreateKernelWithTools();
        var tools = new BenchmarkTools(kernel);

        var result = tools.GenerateScorecard();

        // web_fetch is NOT registered, so web.fetch should be missing
        Assert.Contains("✗ `web.fetch`", result);
    }

    [Fact]
    public void Scorecard_ShowsCategoryPercentages()
    {
        var kernel = CreateKernelWithTools();
        var tools = new BenchmarkTools(kernel);

        var result = tools.GenerateScorecard();

        // Should contain percentage indicators
        Assert.Contains("%)", result);
    }

    // ── benchmark_export ─────────────────────────────────────

    [Fact]
    public void Export_ReturnsValidJson()
    {
        var kernel = CreateKernelWithTools();
        var tools = new BenchmarkTools(kernel);

        var json = tools.ExportRegistry();

        Assert.Contains("\"totalCapabilities\"", json);
        Assert.Contains("\"covered\"", json);
        Assert.Contains("\"capabilities\"", json);

        // Should be valid JSON
        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public void Export_IncludesCoverageStatus()
    {
        var kernel = CreateKernelWithTools();
        var tools = new BenchmarkTools(kernel);

        var json = tools.ExportRegistry();

        // file.read should be covered (read_file registered)
        Assert.Contains("\"id\": \"file.read\"", json);
        Assert.Contains("\"covered\": true", json);
    }

    [Fact]
    public void Export_IncludesMissingTools()
    {
        var kernel = CreateKernelWithTools();
        var tools = new BenchmarkTools(kernel);

        var json = tools.ExportRegistry();

        // Should list missing tools for uncovered capabilities
        Assert.Contains("\"missingTools\"", json);
    }

    // ── benchmark_regression ─────────────────────────────────

    [Fact]
    public void Regression_WithValidBaseline_ReturnsReport()
    {
        var baseline = """
        {
            "capabilities": [
                { "id": "file.read", "covered": true, "requiredTools": ["read_file"] },
                { "id": "web.fetch", "covered": true, "requiredTools": ["web_fetch"] }
            ]
        }
        """;

        var result = BenchmarkTools.CheckRegression(baseline);

        Assert.Contains("## Regression Check", result);
    }

    [Fact]
    public void Regression_WithInvalidJson_ReturnsError()
    {
        var result = BenchmarkTools.CheckRegression("not-json");

        Assert.Contains("Error:", result);
    }

    [Fact]
    public void Regression_WithMissingCapabilities_ReturnsError()
    {
        var result = BenchmarkTools.CheckRegression("{}");

        Assert.Contains("Error:", result);
        Assert.Contains("missing 'capabilities'", result);
    }

    // ── benchmark_run ────────────────────────────────────────

    [Fact]
    public async Task SmokeBenchmark_ReturnsResults()
    {
        var kernel = CreateKernelWithTools();
        var tools = new BenchmarkTools(kernel);

        var result = await tools.RunSmokeBenchmark();

        Assert.Contains("## Smoke Benchmark Results", result);
        Assert.Contains("| Tool | Status |", result);
    }

    [Fact]
    public async Task SmokeBenchmark_ReportsPassForThink()
    {
        var kernel = CreateKernelWithTools();
        var tools = new BenchmarkTools(kernel);

        var result = await tools.RunSmokeBenchmark();

        Assert.Contains("`think`", result);
        Assert.Contains("PASS", result);
    }

    [Fact]
    public async Task SmokeBenchmark_SkipsMissingTools()
    {
        var kernel = CreateKernelWithTools();
        var tools = new BenchmarkTools(kernel);

        var result = await tools.RunSmokeBenchmark();

        // get_environment is NOT registered in our fake kernel
        Assert.Contains("SKIP", result);
    }

    // ── Fake tool plugins for testing ────────────────────────

    internal sealed class FakeFileTools
    {
        [KernelFunction("read_file")]
        [System.ComponentModel.Description("Read a file")]
        public static string ReadFile(
            [System.ComponentModel.Description("Path")] string path)
            => $"Contents of {path}";

        [KernelFunction("write_file")]
        [System.ComponentModel.Description("Write a file")]
        public static string WriteFile(
            [System.ComponentModel.Description("Path")] string path,
            [System.ComponentModel.Description("Content")] string content)
            => "Written";

        [KernelFunction("edit_file")]
        [System.ComponentModel.Description("Edit a file")]
        public static string EditFile(
            [System.ComponentModel.Description("Path")] string path)
            => "Edited";

        [KernelFunction("list_directory")]
        [System.ComponentModel.Description("List directory")]
        public static string ListDir(
            [System.ComponentModel.Description("Path")] string path)
            => "dir listing";
    }

    internal sealed class FakeGitTools
    {
        [KernelFunction("git_status")]
        [System.ComponentModel.Description("Git status")]
        public static string Status() => "clean";

        [KernelFunction("git_diff")]
        [System.ComponentModel.Description("Git diff")]
        public static string Diff() => "no changes";

        [KernelFunction("git_log")]
        [System.ComponentModel.Description("Git log")]
        public static string Log() => "log";

        [KernelFunction("git_commit")]
        [System.ComponentModel.Description("Git commit")]
        public static string Commit(
            [System.ComponentModel.Description("Message")] string message)
            => "committed";
    }

    internal sealed class FakeSearchTools
    {
        [KernelFunction("grep")]
        [System.ComponentModel.Description("Search")]
        public static string Grep(
            [System.ComponentModel.Description("Pattern")] string pattern)
            => "results";

        [KernelFunction("glob")]
        [System.ComponentModel.Description("Find files")]
        public static string Glob(
            [System.ComponentModel.Description("Pattern")] string pattern)
            => "files";
    }

    internal sealed class FakeMetaTools
    {
        [KernelFunction("think")]
        [System.ComponentModel.Description("Think")]
        public static string Think(
            [System.ComponentModel.Description("Thought")] string thought)
            => "thought recorded";

        [KernelFunction("get_usage")]
        [System.ComponentModel.Description("Usage")]
        public static string GetUsage() => "0 tokens";
    }
}
