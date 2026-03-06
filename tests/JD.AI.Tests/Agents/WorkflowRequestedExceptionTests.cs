using FluentAssertions;
using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

public sealed class WorkflowRequestedExceptionTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultMessage()
    {
        var ex = new WorkflowRequestedException();
        ex.Message.Should().Be("Workflow requested");
        ex.TriggeringTool.Should().BeEmpty();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void ToolConstructor_SetsToolAndMessage()
    {
        var ex = new WorkflowRequestedException("shell_exec");
        ex.TriggeringTool.Should().Be("shell_exec");
        ex.Message.Should().Contain("shell_exec");
    }

    [Fact]
    public void InnerExceptionConstructor_SetsBoth()
    {
        var inner = new InvalidOperationException("root");
        var ex = new WorkflowRequestedException("workflow failed", inner);
        ex.Message.Should().Be("workflow failed");
        ex.InnerException.Should().BeSameAs(inner);
        ex.TriggeringTool.Should().BeEmpty();
    }

    [Fact]
    public void IsException_Derived()
    {
        var ex = new WorkflowRequestedException("test");
        ex.Should().BeAssignableTo<Exception>();
    }
}
