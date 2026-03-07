namespace JD.AI.Core.Providers;

/// <summary>
/// Heuristic rules for inferring model capabilities from model names.
/// Used for providers that don't expose capability metadata (Foundry Local, GGUF).
/// </summary>
internal static class ModelCapabilityHeuristics
{
    /// <summary>
    /// Known model families that support tool/function calling.
    /// Names are matched case-insensitively against the model ID or display name.
    /// </summary>
    private static readonly string[] ToolCapableFamilies =
    [
        // Meta Llama 3.1+ and 3.2+ support tools
        "llama-3.1", "llama-3.2", "llama-3.3", "llama3.1", "llama3.2", "llama3.3",
        "llama-4", "llama4",

        // Qwen 2.5+ supports tools
        "qwen2.5", "qwen-2.5", "qwen3", "qwen-3",
        "qwq",

        // Mistral tool-capable variants
        "mistral-large", "mistral-small", "mistral-nemo", "pixtral",
        "mixtral",

        // Google Gemma 2+ with tool support
        "gemma2", "gemma-2", "gemma3", "gemma-3",

        // Command R+ by Cohere
        "command-r", "command-r-plus",

        // Microsoft Phi-3/4 with tools
        "phi-3", "phi3", "phi-4", "phi4",

        // DeepSeek with tool support
        "deepseek-v2", "deepseek-v3", "deepseek-r1",

        // Nous Hermes (function calling fine-tuned)
        "hermes-2", "hermes-3",

        // Functionary (purpose-built for tools)
        "functionary",

        // Firefunction
        "firefunction",

        // NexusRaven
        "nexusraven",

        // GLM-4
        "glm-4", "glm4",

        // Granite
        "granite",
    ];

    /// <summary>
    /// Known model families that support vision/multi-modal input.
    /// </summary>
    private static readonly string[] VisionCapableFamilies =
    [
        "llava", "bakllava",
        "moondream",
        "pixtral",
        "llama-3.2-vision", "llama3.2-vision",
        "minicpm-v",
        "gemma3", "gemma-3",
    ];

    /// <summary>
    /// Infer capabilities from a model name/ID using heuristic matching.
    /// Always returns at least <see cref="ModelCapabilities.Chat"/>.
    /// </summary>
    public static ModelCapabilities InferFromName(string modelName)
    {
        var caps = ModelCapabilities.Chat;
        var name = modelName.ToLowerInvariant();

        foreach (var family in ToolCapableFamilies)
        {
            if (name.Contains(family, StringComparison.OrdinalIgnoreCase))
            {
                caps |= ModelCapabilities.ToolCalling;
                break;
            }
        }

        foreach (var family in VisionCapableFamilies)
        {
            if (name.Contains(family, StringComparison.OrdinalIgnoreCase))
            {
                caps |= ModelCapabilities.Vision;
                break;
            }
        }

        return caps;
    }
}
