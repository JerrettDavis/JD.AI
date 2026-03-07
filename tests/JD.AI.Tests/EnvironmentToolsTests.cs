using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class EnvironmentToolsTests
{
    [Fact]
    public async Task GetEnvironment_ReturnsSystemInfo()
    {
        var result = await EnvironmentTools.GetEnvironmentAsync();

        Assert.Contains("Environment Info", result, StringComparison.Ordinal);
        Assert.Contains("OS:", result, StringComparison.Ordinal);
        Assert.Contains("Architecture:", result, StringComparison.Ordinal);
        Assert.Contains(".NET Runtime:", result, StringComparison.Ordinal);
        Assert.Contains("Working Directory:", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetEnvironment_WithEnvVars_MasksSecrets()
    {
        // Set a test env var that looks like a secret
        Environment.SetEnvironmentVariable("JDAI_TEST_API_KEY", "super-secret-123");
        try
        {
            var result = await EnvironmentTools.GetEnvironmentAsync(includeEnvVars: true);

            Assert.Contains("Environment Variables", result, StringComparison.Ordinal);
            Assert.Contains("JDAI_TEST_API_KEY=***", result, StringComparison.Ordinal);
            Assert.DoesNotContain("super-secret-123", result, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JDAI_TEST_API_KEY", null);
        }
    }

    [Fact]
    public async Task GetEnvironment_WithoutEnvVars_ExcludesSection()
    {
        var result = await EnvironmentTools.GetEnvironmentAsync(includeEnvVars: false);

        Assert.DoesNotContain("Environment Variables", result, StringComparison.Ordinal);
    }
}
