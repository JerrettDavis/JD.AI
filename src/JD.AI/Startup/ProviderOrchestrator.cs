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
    internal static (ProviderRegistry Registry, ProviderConfigurationManager ProviderConfig, ModelMetadataProvider
        MetadataProvider)
        CreateRegistry()
    {
        var credentialStore = new EncryptedFileStore();
        var providerConfig = new ProviderConfigurationManager(credentialStore);
        var metadataProvider = new ModelMetadataProvider();

        var detectors = new IProviderDetector[]
        {
            new ClaudeCodeDetector(), new CopilotDetector(), new OpenAICodexDetector(), new OllamaDetector(),
            new FoundryLocalDetector(), new LocalModelDetector(), new OpenAIDetector(providerConfig),
            new AzureOpenAIDetector(providerConfig), new AnthropicDetector(providerConfig),
            new GoogleGeminiDetector(providerConfig), new MistralDetector(providerConfig),
            new AmazonBedrockDetector(providerConfig), new HuggingFaceDetector(providerConfig),
            new OpenRouterDetector(providerConfig), new OpenAICompatibleDetector(providerConfig)
        };

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
                var selected = SelectModel(
                    opts,
                    preferred.Models,
                    defaultProvider,
                    defaultModel);

                if (selected is not null)
                {
                    if (!opts.PrintMode)
                        AnsiConsole.MarkupLine(
                            $"  [green]✓[/] [bold]{Markup.Escape(preferred.Name)}[/]: " +
                            $"{Markup.Escape(preferred.StatusMessage ?? "Using saved default")}");

                    await PersistSelectionAsync(configStore, projectPath, selected).ConfigureAwait(false);
                    var kernelFast = registry.BuildKernel(selected);
                    return new ProviderSetup(
                        registry,
                        providerConfig,
                        metadataProvider,
                        preferred.Models,
                        selected,
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

        var selectedModel = SelectModel(opts, allModels, defaultProvider, defaultModel);
        if (selectedModel is null) return null;

        await PersistSelectionAsync(configStore, projectPath, selectedModel).ConfigureAwait(false);
        var kernel = registry.BuildKernel(selectedModel);

        return new ProviderSetup(registry, providerConfig, metadataProvider, allModels, selectedModel, kernel);
    }

    private static ProviderModelInfo? SelectModel(
        CliOptions opts,
        IReadOnlyList<ProviderModelInfo> allModels,
        string? defaultProvider,
        string? defaultModel)
    {
        if (opts.CliModel != null)
        {
            var candidates = allModels.Where(m =>
                    m.DisplayName.Contains(opts.CliModel, StringComparison.OrdinalIgnoreCase) ||
                    m.Id.Contains(opts.CliModel, StringComparison.OrdinalIgnoreCase)).
                ToList();

            if (opts.CliProvider != null)
                candidates = candidates.Where(m =>
                        m.ProviderName.Contains(opts.CliProvider, StringComparison.OrdinalIgnoreCase)).
                    ToList();

            if (candidates.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No model matching '{Markup.Escape(opts.CliModel)}' found.[/]");
                return null;
            }

            return candidates[0];
        }

        if (opts.CliProvider != null)
        {
            var candidates = allModels.Where(m =>
                    m.ProviderName.Contains(opts.CliProvider, StringComparison.OrdinalIgnoreCase)).
                ToList();

            if (candidates.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No models from provider '{Markup.Escape(opts.CliProvider)}' found.[/]");
                return null;
            }

            return candidates.Count == 1 || opts.PrintMode
                ? candidates[0]
                : PromptForModel(candidates);
        }

        List<ProviderModelInfo>? defaultCandidates = null;

        if (defaultModel is not null)
        {
            defaultCandidates = allModels.Where(m =>
                    m.DisplayName.Contains(defaultModel, StringComparison.OrdinalIgnoreCase) ||
                    m.Id.Contains(defaultModel, StringComparison.OrdinalIgnoreCase)).
                ToList();

            if (defaultProvider is not null)
                defaultCandidates = defaultCandidates.Where(m =>
                        m.ProviderName.Contains(defaultProvider, StringComparison.OrdinalIgnoreCase)).
                    ToList();
        }
        else if (defaultProvider is not null)
            defaultCandidates = allModels.Where(m =>
                    m.ProviderName.Contains(defaultProvider, StringComparison.OrdinalIgnoreCase)).
                ToList();

        if (defaultCandidates is { Count: > 0 }) return defaultCandidates[0];

        if (allModels.Count == 1 || opts.PrintMode) return allModels[0];

        return PromptForModel(allModels);
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
