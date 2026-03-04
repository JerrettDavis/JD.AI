using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using NSubstitute;
using Xunit;

namespace JD.AI.Tests;

public sealed class PermissionModeTests
{
    private readonly IProviderRegistry _registry = Substitute.For<IProviderRegistry>();

    private AgentSession CreateSession(PermissionMode mode = PermissionMode.Normal)
    {
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        return new AgentSession(_registry, kernel, model) { PermissionMode = mode };
    }

    [Fact]
    public void DefaultPermissionMode_IsNormal()
    {
        var session = CreateSession();
        Assert.Equal(PermissionMode.Normal, session.PermissionMode);
    }

    [Theory]
    [InlineData(PermissionMode.Plan)]
    [InlineData(PermissionMode.AcceptEdits)]
    [InlineData(PermissionMode.BypassAll)]
    [InlineData(PermissionMode.Normal)]
    public void PermissionMode_CanBeSet(PermissionMode mode)
    {
        var session = CreateSession(mode);
        Assert.Equal(mode, session.PermissionMode);
    }

    [Fact]
    public void FallbackModels_DefaultEmpty()
    {
        var session = CreateSession();
        Assert.Empty(session.FallbackModels);
    }

    [Fact]
    public void FallbackModels_CanBeSet()
    {
        var session = CreateSession();
        session.FallbackModels = ["ollama/qwen2.5", "claude-haiku"];
        Assert.Equal(2, session.FallbackModels.Count);
        Assert.Equal("ollama/qwen2.5", session.FallbackModels[0]);
    }

    [Fact]
    public void NoSessionPersistence_DefaultFalse()
    {
        var session = CreateSession();
        Assert.False(session.NoSessionPersistence);
    }

    [Fact]
    public void NoSessionPersistence_CanBeEnabled()
    {
        var session = CreateSession();
        session.NoSessionPersistence = true;
        Assert.True(session.NoSessionPersistence);
    }
}
