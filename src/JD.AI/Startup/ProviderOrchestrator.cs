using JD.AI.Core.Config;
using JD.AI.Core.LocalModels;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Core.Providers.Metadata;
using Microsoft.SemanticKernel;
using Spectre.Console;

namespace JD.AI.Startup;

/// <summary>
///     Result of provider detection and model selection.
/// </summary>
internal sealed record ProviderSetup(
    ProviderRegistry Registry,
    ProviderConfigurationManager ProviderConfig,
    ModelMetadataProvider MetadataProvider,
    IReadOnlyList<ProviderModelInfo> AllModels,
    ProviderModelInfo SelectedModel,
    Kernel Kernel);

/// <summary>
///     Detects available AI providers, lists models, and handles model selection.
///     Extracted from Program.cs lines 202-339.
/// </summary>
internal static class ProviderOrchestrator
{
    private sealed record ProviderDetectorRegistration(
        string Name,
        Func<ProviderConfigurationManager, IProviderDetector> Factory);

    private sealed record ModelSelectionContext(
        CliOptions Options,
        IReadOnlyList<ProviderModelInfo> Models,
        string? DefaultProvider,
        string? DefaultModel,
        Func<IReadOnlyList<ProviderModelInfo>, ProviderModelInfo> PromptSelector);

    private sealed record ProviderModelSpecification(
        string? ModelQuery = null,
        string? ProviderQuery = null)
    {
        public bool IsSatisfiedBy(ProviderModelInfo model)
        {
            if (!string.IsNullOrWhiteSpace(ModelQuery) &&
                !ContainsIgnoreCase(model.DisplayName, ModelQuery) &&
                !ContainsIgnoreCase(model.Id, ModelQuery))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(ProviderQuery) &&
                !ContainsIgnoreCase(model.ProviderName, ProviderQuery))
            {
                return false;
            }

            return true;
        }
    }

    internal sealed record ModelSelectionDecision(
        bool Handled,
        ProviderModelInfo? SelectedModel = null,
        string? ErrorMessage = null)
    {
        public static ModelSelectionDecision Continue => new(false);
        public static ModelSelectionDecision Select(ProviderModelInfo model) => new(true, model);
        public static ModelSelectionDecision Error(string message) => new(true, null, message);
    }

    private delegate ModelSelectionDecision SelectionPolicy(ModelSelectionContext context);

    private static readonly IReadOnlyList<ProviderDetectorRegistration> DetectorManifest =
    [
        new(nameof(ClaudeCodeDetector), _ => new ClaudeCodeDetector()),
        new(nameof(CopilotDetector), _ => new CopilotDetector()),
        new(nameof(OpenAICodexDetector), _ => new OpenAICodexDetector()),
        new(nameof(OllamaDetector), _ => new OllamaDetector()),
        new(nameof(FoundryLocalDetector), _ => new FoundryLocalDetector()),
        new(nameof(LocalModelDetector), _ => new LocalModelDetector()),
        new(nameof(OpenAIDetector), config => new OpenAIDetector(config)),
        new(nameof(AzureOpenAIDetector), config => new AzureOpenAIDetector(config)),
        new(nameof(AnthropicDetector), config => new AnthropicDetector(config)),
        new(nameof(GoogleGeminiDetector), config => new GoogleGeminiDetector(config)),
        new(nameof(MistralDetector), config => new MistralDetector(config)),
        new(nameof(AmazonBedrockDetector), config => new AmazonBedrockDetector(config)),
        new(nameof(HuggingFaceDetector), config => new HuggingFaceDetector(config)),
        new(nameof(OpenRouterDetector), config => new OpenRouterDetector(config)),
        new(nameof(OpenAICompatibleDetector), config => new OpenAICompatibleDetector(config)),
    ];

    private static readonly IReadOnlyList<SelectionPolicy> SelectionPolicies =
    [
        EvaluateCliModelPolicy,
        EvaluateCliProviderPolicy,
        EvaluatePersistedDefaultPolicy,
        EvaluateNonInteractivePolicy,
        EvaluateInteractivePolicy,
    ];

    internal static (ProviderRegistry Registry, ProviderConfigurationManager ProviderConfig, ModelMetadataProvider
        MetadataProvider)
        CreateRegistry()
    {
        var credentialStore = new EncryptedFileStore();
        var providerConfig = new ProviderConfigurationManager(credentialStore);
        var metadataProvider = new ModelMetadataProvider();

        var detectors = DetectorManifest
            .Select(registration => registration.Factory(providerConfig))
            .ToArray();

        var registry = new ProviderRegistry(detectors, metadataProvider);
        return (registry, providerConfig, metadataProvider);
    }

    public static async Task<ProviderSetup?> DetectAndSelectAsync(CliOptions opts, AtomicConfigStore configStore)
    {
        var projectPath = Directory.GetCurrentDirectory();
        var defaultProvider = await configStore.GetDefaultProviderAsync(projectPath).ConfigureAwait(false);
        var defaultModel = await configStore.GetDefaultModelAsync(projectPath).ConfigureAwait(false);

        if (!opts.PrintMode) AnsiConsole.MarkupLine("[dim]Detecting providers...[/]");

        var (registry, providerConfig, metadataProvider) = CreateRegistry();

        // Fast path: prefer the persisted provider/model and refresh auth only for that provider.
        if (opts.CliModel is null
            && opts.CliProvider is null
            && !string.IsNullOrWhiteSpace(defaultProvider))
        {
            var preferred = await registry.DetectProviderAsync(defaultProvider, true).ConfigureAwait(false);

            if (preferred is { IsAvailable: true } && preferred.Models.Count > 0)
            {
                var fastSelection = EvaluateSelection(
                    opts,
                    preferred.Models,
                    defaultProvider,
                    defaultModel);

                if (fastSelection.ErrorMessage is not null)
                {
                    RenderSelectionError(opts, fastSelection.ErrorMessage);
                    return null;
                }

                if (fastSelection.SelectedModel is not null)
                {
                    if (!opts.PrintMode)
                        AnsiConsole.MarkupLine(
                            $"  [green]✓[/] [bold]{Markup.Escape(preferred.Name)}[/]: " +
                            $"{Markup.Escape(preferred.StatusMessage ?? "Using saved default")}");

                    await PersistSelectionAsync(configStore, projectPath, fastSelection.SelectedModel).ConfigureAwait(false);
                    var kernelFast = registry.BuildKernel(fastSelection.SelectedModel);
                    return new ProviderSetup(
                        registry,
                        providerConfig,
                        metadataProvider,
                        preferred.Models,
                        fastSelection.SelectedModel,
                        kernelFast);
                }
            }
        }

        var providers = await registry.DetectProvidersAsync(true).ConfigureAwait(false);
        if (!opts.PrintMode)
            foreach (var p in providers)
            {
                var icon = p.IsAvailable ? "[green]✓[/]" : "[red]✗[/]";
                AnsiConsole.MarkupLine(
                    $"  {icon} [bold]{Markup.Escape(p.Name)}[/]: {Markup.Escape(p.StatusMessage ?? "Unknown")}");
            }

        var allModels = await registry.GetModelsAsync(true).ConfigureAwait(false);
        if (allModels.Count == 0)
        {
            Console.Error.WriteLine("No AI providers available.");
            return null;
        }

        var selection = EvaluateSelection(opts, allModels, defaultProvider, defaultModel);
        if (selection.ErrorMessage is not null)
        {
            RenderSelectionError(opts, selection.ErrorMessage);
            return null;
        }

        if (selection.SelectedModel is null)
            return null;

        await PersistSelectionAsync(configStore, projectPath, selection.SelectedModel).ConfigureAwait(false);
        var kernel = registry.BuildKernel(selection.SelectedModel);

        return new ProviderSetup(registry, providerConfig, metadataProvider, allModels, selection.SelectedModel, kernel);
    }

    internal static ModelSelectionDecision EvaluateSelection(
        CliOptions opts,
        IReadOnlyList<ProviderModelInfo> allModels,
        string? defaultProvider,
        string? defaultModel,
        Func<IReadOnlyList<ProviderModelInfo>, ProviderModelInfo>? promptSelector = null)
    {
        var context = new ModelSelectionContext(
            opts,
            allModels,
            defaultProvider,
            defaultModel,
            promptSelector ?? PromptForModel);

        foreach (var policy in SelectionPolicies)
        {
            var decision = policy(context);
            if (decision.Handled)
                return decision;
        }

        return ModelSelectionDecision.Error("Unable to select a model.");
    }

    private static ModelSelectionDecision EvaluateCliModelPolicy(ModelSelectionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Options.CliModel))
            return ModelSelectionDecision.Continue;

        var candidates = FilterCandidates(
            context.Models,
            new ProviderModelSpecification(
                ModelQuery: context.Options.CliModel,
                ProviderQuery: context.Options.CliProvider));

        if (candidates.Count == 0)
            return ModelSelectionDecision.Error($"No model matching '{context.Options.CliModel}' found.");

        return ModelSelectionDecision.Select(candidates[0]);
    }

    private static ModelSelectionDecision EvaluateCliProviderPolicy(ModelSelectionContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.Options.CliModel) ||
            string.IsNullOrWhiteSpace(context.Options.CliProvider))
        {
            return ModelSelectionDecision.Continue;
        }

        var candidates = FilterCandidates(
            context.Models,
            new ProviderModelSpecification(ProviderQuery: context.Options.CliProvider));
        if (candidates.Count == 0)
        {
            return ModelSelectionDecision.Error(
                $"No models from provider '{context.Options.CliProvider}' found.");
        }

        if (candidates.Count == 1 || context.Options.PrintMode)
            return ModelSelectionDecision.Select(candidates[0]);

        return ModelSelectionDecision.Select(context.PromptSelector(candidates));
    }

    private static ModelSelectionDecision EvaluatePersistedDefaultPolicy(ModelSelectionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.DefaultModel) &&
            string.IsNullOrWhiteSpace(context.DefaultProvider))
        {
            return ModelSelectionDecision.Continue;
        }

        var defaultCandidates = FilterCandidates(
            context.Models,
            new ProviderModelSpecification(
                ModelQuery: context.DefaultModel,
                ProviderQuery: context.DefaultProvider));

        return defaultCandidates.Count > 0
            ? ModelSelectionDecision.Select(defaultCandidates[0])
            : ModelSelectionDecision.Continue;
    }

    private static ModelSelectionDecision EvaluateNonInteractivePolicy(ModelSelectionContext context)
    {
        if (context.Models.Count == 1 || context.Options.PrintMode)
            return ModelSelectionDecision.Select(context.Models[0]);

        return ModelSelectionDecision.Continue;
    }

    private static ModelSelectionDecision EvaluateInteractivePolicy(ModelSelectionContext context)
    {
        if (context.Models.Count == 0)
            return ModelSelectionDecision.Error("No models available.");

        return ModelSelectionDecision.Select(context.PromptSelector(context.Models));
    }

    private static List<ProviderModelInfo> FilterCandidates(
        IReadOnlyList<ProviderModelInfo> models,
        ProviderModelSpecification specification) =>
        models.Where(specification.IsSatisfiedBy).ToList();

    private static bool ContainsIgnoreCase(string source, string value) =>
        source.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static void RenderSelectionError(CliOptions opts, string message)
    {
        if (opts.PrintMode)
            Console.Error.WriteLine(message);
        else
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
    }

    private static async Task PersistSelectionAsync(
        AtomicConfigStore configStore,
        string projectPath,
        ProviderModelInfo selectedModel)
    {
        try
        {
            await configStore.SetDefaultProviderAsync(selectedModel.ProviderName, projectPath).ConfigureAwait(false);
            await configStore.SetDefaultModelAsync(selectedModel.Id, projectPath).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // selection persistence should never block startup
        catch
#pragma warning restore CA1031
        {
            // Best-effort persistence
        }
    }

    private static ProviderModelInfo PromptForModel(IReadOnlyList<ProviderModelInfo> models)
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<ProviderModelInfo>().
                Title("[bold]Select a model[/] [dim](💬=Chat 🔧=Tools 👁=Vision 📐=Embed)[/]").
                PageSize(15).
                UseConverter(m =>
                {
                    var badge = m.Capabilities.ToBadge();
                    return $"{badge} [dim][[{Markup.Escape(m.ProviderName)}]][/] {Markup.Escape(m.DisplayName)}";
                }).
                AddChoices(models));
    }
}
