using JD.AI.Core.Config;
using JD.AI.Core.LocalModels;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Core.Providers.Metadata;
using JD.AI.Rendering;
using Microsoft.SemanticKernel;
using Spectre.Console;

namespace JD.AI.Startup;

/// <summary>
/// Result of provider detection and model selection.
/// </summary>
internal sealed record ProviderSetup(
    ProviderRegistry Registry,
    ProviderConfigurationManager ProviderConfig,
    ModelMetadataProvider MetadataProvider,
    IReadOnlyList<ProviderModelInfo> AllModels,
    ProviderModelInfo SelectedModel,
    Kernel Kernel);

/// <summary>
/// Detects available AI providers, lists models, and handles model selection.
/// Extracted from Program.cs lines 202-339.
/// </summary>
internal static class ProviderOrchestrator
{
    public static async Task<ProviderSetup?> DetectAndSelectAsync(CliOptions opts, AtomicConfigStore configStore)
    {
        if (!opts.PrintMode)
        {
            AnsiConsole.MarkupLine("[dim]Detecting providers...[/]");
        }

        var credentialStore = new EncryptedFileStore();
        var providerConfig = new ProviderConfigurationManager(credentialStore);

        var detectors = new IProviderDetector[]
        {
            new ClaudeCodeDetector(),
            new CopilotDetector(),
            new OpenAICodexDetector(),
            new OllamaDetector(),
            new FoundryLocalDetector(),
            new LocalModelDetector(),
            new OpenAIDetector(providerConfig),
            new AzureOpenAIDetector(providerConfig),
            new AnthropicDetector(providerConfig),
            new GoogleGeminiDetector(providerConfig),
            new MistralDetector(providerConfig),
            new AmazonBedrockDetector(providerConfig),
            new HuggingFaceDetector(providerConfig),
            new OpenAICompatibleDetector(providerConfig),
        };
        var metadataProvider = new ModelMetadataProvider();
        var registry = new ProviderRegistry(detectors, metadataProvider);

        var providers = await registry.DetectProvidersAsync().ConfigureAwait(false);
        if (!opts.PrintMode)
        {
            foreach (var p in providers)
            {
                var icon = p.IsAvailable ? "[green]✓[/]" : "[red]✗[/]";
                AnsiConsole.MarkupLine($"  {icon} [bold]{Markup.Escape(p.Name)}[/]: {Markup.Escape(p.StatusMessage ?? "Unknown")}");
            }
        }

        var allModels = await registry.GetModelsAsync().ConfigureAwait(false);
        if (allModels.Count == 0)
        {
            Console.Error.WriteLine("No AI providers available.");
            return null;
        }

        var selectedModel = SelectModel(opts, allModels, configStore);
        if (selectedModel is null)
        {
            return null;
        }

        var kernel = registry.BuildKernel(selectedModel);

        return new ProviderSetup(registry, providerConfig, metadataProvider, allModels, selectedModel, kernel);
    }

    private static ProviderModelInfo? SelectModel(
        CliOptions opts,
        IReadOnlyList<ProviderModelInfo> allModels,
        AtomicConfigStore configStore)
    {
        if (opts.CliModel != null)
        {
            var candidates = allModels.Where(m =>
                m.DisplayName.Contains(opts.CliModel, StringComparison.OrdinalIgnoreCase) ||
                m.Id.Contains(opts.CliModel, StringComparison.OrdinalIgnoreCase)).ToList();

            if (opts.CliProvider != null)
            {
                candidates = candidates.Where(m =>
                    m.ProviderName.Contains(opts.CliProvider, StringComparison.OrdinalIgnoreCase)).ToList();
            }

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
                m.ProviderName.Contains(opts.CliProvider, StringComparison.OrdinalIgnoreCase)).ToList();

            if (candidates.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No models from provider '{Markup.Escape(opts.CliProvider)}' found.[/]");
                return null;
            }

            return candidates.Count == 1 || opts.PrintMode
                ? candidates[0]
                : PromptForModel(candidates);
        }

        // Check per-project then global defaults
        var cfgProjectPath = Directory.GetCurrentDirectory();
        var defaultModel = configStore.GetDefaultModelAsync(cfgProjectPath).GetAwaiter().GetResult();
        var defaultProvider = configStore.GetDefaultProviderAsync(cfgProjectPath).GetAwaiter().GetResult();

        List<ProviderModelInfo>? defaultCandidates = null;

        if (defaultModel is not null)
        {
            defaultCandidates = allModels.Where(m =>
                m.DisplayName.Contains(defaultModel, StringComparison.OrdinalIgnoreCase) ||
                m.Id.Contains(defaultModel, StringComparison.OrdinalIgnoreCase)).ToList();

            if (defaultProvider is not null)
            {
                defaultCandidates = defaultCandidates.Where(m =>
                    m.ProviderName.Contains(defaultProvider, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }
        else if (defaultProvider is not null)
        {
            defaultCandidates = allModels.Where(m =>
                m.ProviderName.Contains(defaultProvider, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (defaultCandidates is { Count: > 0 })
        {
            return defaultCandidates[0];
        }

        if (allModels.Count == 1 || opts.PrintMode)
        {
            return allModels[0];
        }

        return PromptForModel(allModels);
    }

    private static ProviderModelInfo PromptForModel(IReadOnlyList<ProviderModelInfo> models)
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<ProviderModelInfo>()
                .Title("[bold]Select a model[/] [dim](💬=Chat 🔧=Tools 👁=Vision 📐=Embed)[/]")
                .PageSize(15)
                .UseConverter(m =>
                {
                    var badge = m.Capabilities.ToBadge();
                    return $"{badge} [dim][[{Markup.Escape(m.ProviderName)}]][/] {Markup.Escape(m.DisplayName)}";
                })
                .AddChoices(models));
    }
}
