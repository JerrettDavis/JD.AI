using FluentAssertions;
using JD.AI.Core.Events;
using JD.AI.Core.Providers;
using JD.AI.Core.Sessions;
using JD.AI.Gateway.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using NSubstitute;

namespace JD.AI.Gateway.Tests.Services;

public sealed class AgentPoolServiceSessionTests
{
    [Fact]
    public async Task SpawnAgentAsync_WithPreferredId_UsesStableId_AndCreatesMainSession()
    {
        var dbPath = MakeTempDbPath();
        var store = new SessionStore(dbPath);
        try
        {
            var pool = CreatePool(store);

            var id = await pool.SpawnAgentAsync(
                provider: "fake",
                model: "model-a",
                systemPrompt: "You are stable.",
                ct: CancellationToken.None,
                preferredAgentId: "assistant");

            id.Should().Be("assistant");

            var session = await store.GetSessionAsync("agent:assistant:main");
            session.Should().NotBeNull();
            session!.IsActive.Should().BeTrue();
            session.Turns.Should().ContainSingle(t =>
                t.Role == "system" && t.Content == "You are stable.");
        }
        finally
        {
            store.Dispose();
            TryDelete(dbPath);
        }
    }

    [Fact]
    public async Task SpawnAgentAsync_WhenPreferredIdAlreadyInUse_AppendsNumericSuffix()
    {
        var dbPath = MakeTempDbPath();
        var store = new SessionStore(dbPath);
        try
        {
            var pool = CreatePool(store);

            var first = await pool.SpawnAgentAsync("fake", "model-a", null, CancellationToken.None, preferredAgentId: "assistant");
            var second = await pool.SpawnAgentAsync("fake", "model-a", null, CancellationToken.None, preferredAgentId: "assistant");

            first.Should().Be("assistant");
            second.Should().Be("assistant-2");

            (await store.GetSessionAsync("agent:assistant:main")).Should().NotBeNull();
            (await store.GetSessionAsync("agent:assistant-2:main")).Should().NotBeNull();
        }
        finally
        {
            store.Dispose();
            TryDelete(dbPath);
        }
    }

    [Fact]
    public async Task StopAgent_ClosesAssociatedSession()
    {
        var dbPath = MakeTempDbPath();
        var store = new SessionStore(dbPath);
        try
        {
            var pool = CreatePool(store);
            var id = await pool.SpawnAgentAsync("fake", "model-a", null, CancellationToken.None, preferredAgentId: "assistant");

            pool.StopAgent(id);
            await WaitForSessionInactiveAsync(store, "agent:assistant:main");

            var session = await store.GetSessionAsync("agent:assistant:main");
            session.Should().NotBeNull();
            session!.IsActive.Should().BeFalse();
        }
        finally
        {
            store.Dispose();
            TryDelete(dbPath);
        }
    }

    [Fact]
    public async Task SpawnAgentAsync_WhenSessionAlreadyExists_DoesNotDuplicateSystemPromptTurn()
    {
        var dbPath = MakeTempDbPath();
        var store = new SessionStore(dbPath);
        try
        {
            var pool = CreatePool(store);
            var prompt = "You are persistent.";

            var id = await pool.SpawnAgentAsync("fake", "model-a", prompt, CancellationToken.None, preferredAgentId: "assistant");
            pool.StopAgent(id);
            await WaitForSessionInactiveAsync(store, "agent:assistant:main");

            await pool.SpawnAgentAsync("fake", "model-a", prompt, CancellationToken.None, preferredAgentId: "assistant");

            var session = await store.GetSessionAsync("agent:assistant:main");
            session.Should().NotBeNull();
            session!.Turns.Should().ContainSingle(t => t.Role == "system" && t.Content == prompt);
        }
        finally
        {
            store.Dispose();
            TryDelete(dbPath);
        }
    }

    private static AgentPoolService CreatePool(SessionStore sessionStore)
    {
        var model = new ProviderModelInfo("model-a", "Model A", "fake");
        var detector = Substitute.For<IProviderDetector>();
        detector.ProviderName.Returns("fake");
        detector.BuildKernel(Arg.Any<ProviderModelInfo>()).Returns(_ => CreateKernel());

        var providers = Substitute.For<IProviderRegistry>();
        providers.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderInfo>>(
            [
                new ProviderInfo("fake", true, "ready", [model]),
            ]));
        providers.GetDetector("fake").Returns(detector);

        var events = Substitute.For<IEventBus>();
        return new AgentPoolService(providers, events, NullLogger<AgentPoolService>.Instance, sessionStore);
    }

    private static Kernel CreateKernel()
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>());
        return builder.Build();
    }

    private static string MakeTempDbPath() =>
        Path.Combine(Path.GetTempPath(), $"jdai-gateway-agentpool-{Guid.NewGuid():N}.db");

    private static void TryDelete(string dbPath)
    {
        try
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
        catch
        {
            // Best-effort cleanup for test temp files.
        }
    }

    private static async Task WaitForSessionInactiveAsync(SessionStore store, string sessionId)
    {
        for (var i = 0; i < 20; i++)
        {
            var session = await store.GetSessionAsync(sessionId);
            if (session is not null && !session.IsActive)
            {
                return;
            }

            await Task.Delay(25);
        }
    }
}
