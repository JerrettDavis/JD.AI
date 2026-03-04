using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests;

public class CapabilityToolsTests
{
    private static Kernel CreateKernelWithTools()
    {
        var builder = Kernel.CreateBuilder();
        var kernel = builder.Build();

        // Register some sample tools for testing
        kernel.Plugins.AddFromType<FakeToolsA>("file");
        kernel.Plugins.AddFromType<FakeToolsB>("git");
        kernel.Plugins.AddFromObject(new CapabilityTools(kernel), "capabilities");
        return kernel;
    }

    // ── capability_list ──────────────────────────────────────

    [Fact]
    public void ListCapabilities_ReturnsAllPlugins()
    {
        var kernel = CreateKernelWithTools();
        var tools = new CapabilityTools(kernel);

        var result = tools.ListCapabilities();

        Assert.Contains("### capabilities", result);
        Assert.Contains("### file", result);
        Assert.Contains("### git", result);
        Assert.Contains("`read_file`", result);
        Assert.Contains("`git_status`", result);
    }

    [Fact]
    public void ListCapabilities_FilterByPlugin_ReturnsOnlyThatPlugin()
    {
        var kernel = CreateKernelWithTools();
        var tools = new CapabilityTools(kernel);

        var result = tools.ListCapabilities(plugin: "git");

        Assert.Contains("### git", result);
        Assert.DoesNotContain("### file", result);
    }

    [Fact]
    public void ListCapabilities_UnknownPlugin_ReturnsEmpty()
    {
        var kernel = CreateKernelWithTools();
        var tools = new CapabilityTools(kernel);

        var result = tools.ListCapabilities(plugin: "nonexistent");

        Assert.Contains("**Total: 0 tools", result);
    }

    // ── capability_detail ────────────────────────────────────

    [Fact]
    public void GetToolDetail_ExistingTool_ReturnsMetadata()
    {
        var kernel = CreateKernelWithTools();
        var tools = new CapabilityTools(kernel);

        var result = tools.GetToolDetail("read_file");

        Assert.Contains("## read_file", result);
        Assert.Contains("**Plugin**: file", result);
        Assert.Contains("### Parameters", result);
        Assert.Contains("`path`", result);
    }

    [Fact]
    public void GetToolDetail_MissingTool_ReturnsError()
    {
        var kernel = CreateKernelWithTools();
        var tools = new CapabilityTools(kernel);

        var result = tools.GetToolDetail("nonexistent_tool");

        Assert.Contains("Error:", result);
        Assert.Contains("not found", result);
    }

    // ── capability_usage ─────────────────────────────────────

    [Fact]
    public void AnalyzeUsage_NoUsage_ReturnsNoUsageMessage()
    {
        var kernel = CreateKernelWithTools();
        var tools = new CapabilityTools(kernel);

        var result = tools.AnalyzeUsage();

        Assert.Contains("No tool usage recorded", result);
    }

    [Fact]
    public void AnalyzeUsage_WithUsage_ShowsMostUsed()
    {
        var kernel = CreateKernelWithTools();
        var tools = new CapabilityTools(kernel);

        tools.RecordUsage("read_file");
        tools.RecordUsage("read_file");
        tools.RecordUsage("git_status");

        var result = tools.AnalyzeUsage();

        Assert.Contains("`read_file`: 2 call(s)", result);
        Assert.Contains("`git_status`: 1 call(s)", result);
        Assert.Contains("3 total calls", result);
    }

    [Fact]
    public void AnalyzeUsage_WithUsage_ShowsUnusedTools()
    {
        var kernel = CreateKernelWithTools();
        var tools = new CapabilityTools(kernel);

        tools.RecordUsage("read_file");

        var result = tools.AnalyzeUsage();

        Assert.Contains("### Unused Tools", result);
        Assert.Contains("`git_status`", result);
    }

    // ── capability_gaps ──────────────────────────────────────

    [Fact]
    public void AnalyzeGaps_ReturnsCategories()
    {
        var kernel = CreateKernelWithTools();
        var tools = new CapabilityTools(kernel);

        var result = tools.AnalyzeGaps();

        Assert.Contains("## Capability Gap Analysis", result);
        Assert.Contains("File Operations", result);
        Assert.Contains("Version Control", result);
        Assert.Contains("**Coverage**:", result);
    }

    [Fact]
    public void AnalyzeGaps_ShowsPartialCoverage()
    {
        var kernel = CreateKernelWithTools();
        var tools = new CapabilityTools(kernel);

        var result = tools.AnalyzeGaps();

        // Should have partial coverage for File Operations (only read_file)
        Assert.Contains("◐", result);
    }

    // ── capability_scaffold ──────────────────────────────────

    [Fact]
    public void ScaffoldTool_GeneratesValidCSharp()
    {
        var result = CapabilityTools.ScaffoldTool("WeatherTools");

        Assert.Contains("public sealed class WeatherTools", result);
        Assert.Contains("[KernelFunction(\"example_action\")]", result);
        Assert.Contains("using Microsoft.SemanticKernel;", result);
        Assert.Contains("namespace JD.AI.Core.Tools;", result);
    }

    [Fact]
    public void ScaffoldTool_WithFunctions_GeneratesAll()
    {
        var result = CapabilityTools.ScaffoldTool(
            "ApiTools", functions: "fetch_data,post_data,delete_record");

        Assert.Contains("[KernelFunction(\"fetch_data\")]", result);
        Assert.Contains("[KernelFunction(\"post_data\")]", result);
        Assert.Contains("[KernelFunction(\"delete_record\")]", result);
        Assert.Contains("public static string FetchData(", result);
        Assert.Contains("public static string PostData(", result);
        Assert.Contains("public static string DeleteRecord(", result);
    }

    [Fact]
    public void ScaffoldTool_IncludesRegistrationHint()
    {
        var result = CapabilityTools.ScaffoldTool("WeatherTools");

        Assert.Contains("Register with:", result);
        Assert.Contains("AddFromType<WeatherTools>", result);
    }

    // ── RecordUsage ──────────────────────────────────────────

    [Fact]
    public void RecordUsage_IncrementsCounts()
    {
        var kernel = CreateKernelWithTools();
        var tools = new CapabilityTools(kernel);

        tools.RecordUsage("read_file");
        tools.RecordUsage("read_file");
        tools.RecordUsage("read_file");

        var result = tools.AnalyzeUsage();
        Assert.Contains("`read_file`: 3 call(s)", result);
    }

    // ── Fake tool plugins for testing ────────────────────────

    public sealed class FakeToolsA
    {
        [Microsoft.SemanticKernel.KernelFunction("read_file")]
        [System.ComponentModel.Description("Read a file from disk")]
        public static string ReadFile(
            [System.ComponentModel.Description("Path to the file")] string path)
            => $"Contents of {path}";

        [Microsoft.SemanticKernel.KernelFunction("write_file")]
        [System.ComponentModel.Description("Write content to a file")]
        public static string WriteFile(
            [System.ComponentModel.Description("Path to the file")] string path,
            [System.ComponentModel.Description("Content to write")] string content)
            => "Written";
    }

    public sealed class FakeToolsB
    {
        [Microsoft.SemanticKernel.KernelFunction("git_status")]
        [System.ComponentModel.Description("Show git status")]
        public static string GitStatus() => "clean";

        [Microsoft.SemanticKernel.KernelFunction("git_diff")]
        [System.ComponentModel.Description("Show git diff")]
        public static string GitDiff() => "no changes";
    }
}
