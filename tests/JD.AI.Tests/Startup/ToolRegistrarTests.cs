using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

public sealed class ToolRegistrarTests
{
    [Fact]
    public void RegisterAll_RegistersStatefulAndChannelPlugins_AndTracksModelSwitches()
    {
        var modelA = new ProviderModelInfo("model-a", "Model A", "TestProvider");
        var modelB = new ProviderModelInfo("model-b", "Model B", "TestProvider");
        var (registry, _, _) = StartupTestProviderFactory.CreateRegistry(
            StartupTestProviderFactory.AvailableProvider("TestProvider", modelA, modelB));
        var kernel = registry.BuildKernel(modelA);
        var session = new AgentSession(registry, kernel, modelA);

        var registration = ToolRegistrar.RegisterAll(kernel, session, modelA);
        registration.UsageTools.RecordUsage(42, 24, 3);

        session.SwitchModel(modelB);
        var usageReport = registration.UsageTools.GetUsage();

        Assert.Contains("Model B", usageReport, StringComparison.Ordinal);
        Assert.Contains("Turns: 1", usageReport, StringComparison.Ordinal);

        Assert.Contains(kernel.Plugins, p => p.Name.Equals("memory", StringComparison.Ordinal));
        Assert.Contains(kernel.Plugins, p => p.Name.Equals("tasks", StringComparison.Ordinal));
        Assert.Contains(kernel.Plugins, p => p.Name.Equals("usage", StringComparison.Ordinal));
        Assert.Contains(kernel.Plugins, p => p.Name.Equals("runtime", StringComparison.Ordinal));
        Assert.Contains(kernel.Plugins, p => p.Name.Equals("channels", StringComparison.Ordinal));
        Assert.Contains(kernel.Plugins, p => p.Name.Equals("gateway", StringComparison.Ordinal));
        Assert.Contains(kernel.Plugins, p => p.Name.Equals("openclaw", StringComparison.Ordinal));
    }
}
