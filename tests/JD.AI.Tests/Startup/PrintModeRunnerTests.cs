using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using JD.AI.Core.Skills;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

public sealed class PrintModeRunnerTests
{
    [Fact]
    public async Task RunAsync_ReturnsErrorWhenNoQueryOrPipedInputProvided()
    {
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        var (registry, _, _) = StartupTestProviderFactory.CreateRegistry(
            StartupTestProviderFactory.AvailableProvider("TestProvider", model));
        var session = new AgentSession(registry, registry.BuildKernel(model), model);
        using var skills = new SkillLifecycleManager([]);

        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);

        try
        {
            var exitCode = await PrintModeRunner.RunAsync(new CliOptions(), session, model, skills);
            Assert.Equal(1, exitCode);
            Assert.Contains("--print requires a query argument or piped input", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_StopsBeforeTurnExecutionWhenMaxTurnsExceeded()
    {
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        var (registry, _, _) = StartupTestProviderFactory.CreateRegistry(
            StartupTestProviderFactory.AvailableProvider("TestProvider", model));
        var session = new AgentSession(registry, registry.BuildKernel(model), model);
        using var skills = new SkillLifecycleManager([]);

        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);

        try
        {
            var options = new CliOptions
            {
                PrintQuery = "hello",
                MaxTurns = 0,
            };

            var exitCode = await PrintModeRunner.RunAsync(options, session, model, skills);

            Assert.Equal(1, exitCode);
            Assert.Contains("max turns (0) exceeded", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void JsonOptions_Indented_IsWriteIndented()
    {
        Assert.True(JsonOptions.Indented.WriteIndented);
    }
}
