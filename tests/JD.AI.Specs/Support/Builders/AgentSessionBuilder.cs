using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;

namespace JD.AI.Specs.Support.Builders;

/// <summary>
/// Fluent builder for constructing <see cref="AgentSession"/> instances in tests.
/// </summary>
public sealed class AgentSessionBuilder
{
    private IProviderRegistry? _registry;
    private Kernel? _kernel;
    private ProviderModelInfo? _model;
    private IChatCompletionService? _chatService;
    private bool _noSessionPersistence = true;

    public static AgentSessionBuilder Create() => new();

    public AgentSessionBuilder WithRegistry(IProviderRegistry registry)
    {
        _registry = registry;
        return this;
    }

    public AgentSessionBuilder WithKernel(Kernel kernel)
    {
        _kernel = kernel;
        return this;
    }

    public AgentSessionBuilder WithModel(ProviderModelInfo model)
    {
        _model = model;
        return this;
    }

    public AgentSessionBuilder WithModel(string modelId, string providerName = "test-provider")
    {
        _model = new ProviderModelInfo(modelId, modelId, providerName);
        return this;
    }

    public AgentSessionBuilder WithChatService(IChatCompletionService chatService)
    {
        _chatService = chatService;
        return this;
    }

    public AgentSessionBuilder WithSessionPersistence(bool enabled = true)
    {
        _noSessionPersistence = !enabled;
        return this;
    }

    public AgentSession Build()
    {
        var model = _model ?? new ProviderModelInfo("test-model", "Test Model", "test-provider");

        var registry = _registry ?? CreateDefaultRegistry(model);

        var kernel = _kernel ?? CreateKernel(registry, model);

        var session = new AgentSession(registry, kernel, model)
        {
            NoSessionPersistence = _noSessionPersistence,
        };

        return session;
    }

    internal static Kernel BuildKernelWithChatService(IChatCompletionService chatService)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        return builder.Build();
    }

    private Kernel CreateKernel(IProviderRegistry registry, ProviderModelInfo model)
    {
        if (_chatService != null)
        {
            var kernel = BuildKernelWithChatService(_chatService);
            registry.BuildKernel(model).Returns(kernel);
            return kernel;
        }

        var mockKernel = registry.BuildKernel(model);
        return mockKernel ?? Kernel.CreateBuilder().Build();
    }

    private IProviderRegistry CreateDefaultRegistry(ProviderModelInfo model)
    {
        var registry = Substitute.For<IProviderRegistry>();

        if (_chatService != null)
        {
            var kernel = BuildKernelWithChatService(_chatService);
            registry.BuildKernel(model).Returns(kernel);
            registry.BuildKernel(Arg.Any<ProviderModelInfo>()).Returns(kernel);
        }
        else
        {
            registry.BuildKernel(Arg.Any<ProviderModelInfo>())
                .Returns(_ => Kernel.CreateBuilder().Build());
        }

        registry.GetModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderModelInfo>>([model]));

        return registry;
    }
}
