using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using JD.AI.Startup;
using Microsoft.SemanticKernel;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Startup;

[Feature("Tool Registration")]
public sealed class ToolRegistrarBddTests : TinyBddXunitBase
{
    public ToolRegistrarBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("RegisterAll wires core plugin surfaces for interactive runs"), Fact]
    public async Task RegisterAll_WiresCorePluginSurfaces()
    {
        ToolRegistration? registration = null;
        Kernel? kernel = null;

        await Given("a kernel, session, and selected model", () =>
            {
                var registry = Substitute.For<IProviderRegistry>();
                kernel = Kernel.CreateBuilder().Build();
                var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
                var session = new AgentSession(registry, kernel, model);
                return (kernel, session, model);
            })
            .When("registering all built-in tools", ctx =>
            {
                registration = ToolRegistrar.RegisterAll(ctx.kernel, ctx.session, ctx.model);
                return Task.CompletedTask;
            })
            .Then("stateful tool references are returned and key plugins exist", _ =>
            {
                registration.Should().NotBeNull();
                registration!.UsageTools.Should().NotBeNull();
                registration.TaskTools.Should().NotBeNull();
                registration.WebSearchTools.Should().NotBeNull();
                registration.ProcessSessionManager.Should().NotBeNull();

                kernel!.Plugins.Should().Contain(p => p.Name == "tasks");
                kernel.Plugins.Should().Contain(p => p.Name == "usage");
                kernel.Plugins.Should().Contain(p => p.Name == "runtime");
                kernel.Plugins.Should().Contain(p => p.Name == "openclaw");
                kernel.Plugins.Should().Contain(p => p.Name == "sessions");
                kernel.Plugins.Should().Contain(p => p.Name == "channels");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Usage tool is initialized with the selected model metadata"), Fact]
    public async Task UsageTool_IsInitializedWithSelectedModelMetadata()
    {
        string? usage = null;

        await Given("a selected model with explicit cost metadata", () =>
            {
                var registry = Substitute.For<IProviderRegistry>();
                var kernel = Kernel.CreateBuilder().Build();
                var model = new ProviderModelInfo(
                    Id: "model-pro",
                    DisplayName: "Model Pro",
                    ProviderName: "TestProvider",
                    InputCostPerToken: 0.001m,
                    OutputCostPerToken: 0.002m,
                    HasMetadata: true);
                var session = new AgentSession(registry, kernel, model);
                return (kernel, session, model);
            })
            .When("usage is recorded after registration", ctx =>
            {
                var registration = ToolRegistrar.RegisterAll(ctx.kernel, ctx.session, ctx.model);
                registration.UsageTools.RecordUsage(promptTokens: 100, completionTokens: 50, toolCalls: 1);
                usage = registration.UsageTools.GetUsage();
                return Task.CompletedTask;
            })
            .Then("the usage report references the selected model", _ =>
            {
                usage.Should().NotBeNull();
                usage.Should().Contain("Model Pro");
                usage.Should().Contain("Total:");
                return true;
            })
            .AssertPassed();
    }
}
