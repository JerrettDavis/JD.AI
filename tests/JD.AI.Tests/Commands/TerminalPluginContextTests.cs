using JD.AI.Commands;
using JD.AI.Plugins.SDK;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests.Commands;

public sealed class TerminalPluginContextTests
{
    [Fact]
    public void Constructor_ExposesKernelAndEmptyConfig()
    {
        var kernel = Kernel.CreateBuilder().Build();

        var context = new TerminalPluginContext(kernel);

        Assert.Same(kernel, context.Kernel);
        Assert.Empty(context.Configuration);
    }

    [Fact]
    public void GetService_ReturnsNullForAnyType()
    {
        var context = new TerminalPluginContext(Kernel.CreateBuilder().Build());

        Assert.Null(context.GetService<object>());
        Assert.Null(context.GetService<IPluginContext>());
    }

    [Fact]
    public void OnEventAndLog_AreNoOps()
    {
        var context = new TerminalPluginContext(Kernel.CreateBuilder().Build());
        var invoked = false;

        context.OnEvent("test", _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });
        context.Log(PluginLogLevel.Info, "message");

        Assert.False(invoked);
    }
}
