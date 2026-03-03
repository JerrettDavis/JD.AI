using System.Runtime.CompilerServices;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using AuthorRole = Microsoft.SemanticKernel.ChatCompletion.AuthorRole;
using ChatHistory = Microsoft.SemanticKernel.ChatCompletion.ChatHistory;

namespace JD.AI.Core.LocalModels;

/// <summary>
/// Options for configuring the LLama inference engine.
/// </summary>
public sealed class LocalModelOptions
{
    /// <summary>Context window size in tokens.</summary>
    public uint ContextSize { get; set; } = 4096;

    /// <summary>
    /// Number of GPU layers to offload. -1 = all (when GPU available), 0 = CPU only.
    /// </summary>
    public int GpuLayers { get; set; } = -1;

    /// <summary>Maximum tokens to generate per response.</summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>Temperature for sampling (0 = deterministic).</summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>Top-P nucleus sampling.</summary>
    public float TopP { get; set; } = 0.9f;
}

/// <summary>
/// Wraps LLamaSharp to provide Semantic Kernel's <see cref="IChatCompletionService"/>.
/// Manages model loading/unloading and streaming inference.
/// </summary>
public sealed class LlamaInferenceEngine : IChatCompletionService, IDisposable
{
    private readonly ModelMetadata _modelInfo;
    private readonly LocalModelOptions _options;
    private readonly ILogger? _logger;
    private LLamaWeights? _model;
    private LLamaContext? _context;
    private InteractiveExecutor? _executor;
    private bool _disposed;

    public LlamaInferenceEngine(
        ModelMetadata modelInfo,
        LocalModelOptions? options = null,
        ILogger? logger = null)
    {
        _modelInfo = modelInfo;
        _options = options ?? new();
        _logger = logger;
    }

    public IReadOnlyDictionary<string, object?> Attributes { get; } =
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["model_id"] = "local",
            ["provider"] = "Local",
        };

    /// <summary>
    /// Whether the model is currently loaded.
    /// </summary>
    public bool IsLoaded => _model is not null;

    /// <summary>
    /// Load the model into memory. Call before first inference.
    /// </summary>
    public void Load()
    {
        if (_disposed) ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsLoaded) return;

        var gpuLayers = _options.GpuLayers;
        if (gpuLayers == -1)
        {
            var backend = GpuDetector.Detect();
            gpuLayers = GpuDetector.RecommendGpuLayers(backend, _modelInfo.FileSizeBytes);
            if (gpuLayers == -1) gpuLayers = 99; // offload as many as possible
        }

        _logger?.LogInformation(
            "Loading model {Model} (GPU layers: {GpuLayers}, context: {ContextSize})",
            _modelInfo.DisplayName, gpuLayers, _options.ContextSize);

        var parameters = new ModelParams(_modelInfo.FilePath)
        {
            ContextSize = _options.ContextSize,
            GpuLayerCount = gpuLayers,
        };

        try
        {
            _model = LLamaWeights.LoadFromFile(parameters);
            _context = _model.CreateContext(parameters);
            _executor = new InteractiveExecutor(_context);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "GPU loading failed, retrying CPU-only");
            Unload();

            parameters = new ModelParams(_modelInfo.FilePath)
            {
                ContextSize = _options.ContextSize,
                GpuLayerCount = 0,
            };

            _model = LLamaWeights.LoadFromFile(parameters);
            _context = _model.CreateContext(parameters);
            _executor = new InteractiveExecutor(_context);
        }
    }

    /// <summary>
    /// Unload the model and free memory.
    /// </summary>
    public void Unload()
    {
        _executor = null;
        _context?.Dispose();
        _context = null;
        _model?.Dispose();
        _model = null;
    }

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        await foreach (var chunk in GetStreamingChatMessageContentsAsync(
            chatHistory, executionSettings, kernel, cancellationToken).ConfigureAwait(false))
        {
            sb.Append(chunk.Content);
        }

        return [new ChatMessageContent(AuthorRole.Assistant, sb.ToString())];
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!IsLoaded) Load();

        var prompt = FormatChatHistory(chatHistory);
        var inferenceParams = new InferenceParams
        {
            MaxTokens = _options.MaxTokens,
            AntiPrompts = ["User:", "\nUser:", "<|eot_id|>", "<|end|>"],
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = _options.Temperature,
                TopP = _options.TopP,
            },
        };

        await foreach (var text in _executor!.InferAsync(prompt, inferenceParams, cancellationToken)
            .ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(text))
            {
                yield return new StreamingChatMessageContent(AuthorRole.Assistant, text);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unload();
    }

    /// <summary>
    /// Formats SK ChatHistory into a prompt string for the LLM.
    /// Uses a generic chat template compatible with most GGUF models.
    /// </summary>
    private static string FormatChatHistory(ChatHistory history)
    {
        var sb = new StringBuilder();

        foreach (var msg in history)
        {
            var role = msg.Role == AuthorRole.System ? "System"
                : msg.Role == AuthorRole.User ? "User"
                : "Assistant";

            sb.AppendLine($"{role}: {msg.Content}");
        }

        sb.Append("Assistant: ");
        return sb.ToString();
    }
}
