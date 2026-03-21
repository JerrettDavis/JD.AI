using System.Text.Json;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using JD.AI.Core.PromptCaching;
using Microsoft.Extensions.AI;

namespace JD.AI.Core.Providers;

/// <summary>
/// Wraps Anthropic chat calls and injects native prompt caching directives.
/// </summary>
internal sealed class AnthropicPromptCachingChatClient : IChatClient
{
    private readonly IChatClient _inner;

    public AnthropicPromptCachingChatClient(IChatClient inner)
    {
        _inner = inner;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        _inner.GetService(serviceType, serviceKey);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var (preparedMessages, preparedOptions) = PrepareRequest(messages, options);
        return _inner.GetResponseAsync(preparedMessages, preparedOptions, cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var (preparedMessages, preparedOptions) = PrepareRequest(messages, options);
        return _inner.GetStreamingResponseAsync(preparedMessages, preparedOptions, cancellationToken);
    }

    public void Dispose()
    {
        if (_inner is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private (IEnumerable<ChatMessage> Messages, ChatOptions? Options) PrepareRequest(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options)
    {
        if (options is null || !TryGetCachingDirective(options, out var ttl))
        {
            return (messages, options);
        }

        MessageParameters parameters;
        try
        {
            parameters = ChatClientHelper.CreateMessageParameters(_inner, messages, options);
        }
        catch (MissingMethodException)
        {
            // Some Microsoft.Extensions.AI package combinations can surface
            // runtime API mismatches in ChatClientHelper's tool projection path.
            // Fall back to normal request execution rather than failing the call.
            return (messages, options);
        }

        parameters.PromptCaching = PromptCacheType.AutomaticToolsAndSystem;
        ApplyCacheControls(parameters, ttl);

        // Preserve all configured chat options (temperature, max tokens, tools, etc.)
        // while overriding the raw request payload with Anthropic-native parameters.
        var passthroughOptions = options.Clone();
        passthroughOptions.RawRepresentationFactory = _ => parameters;

        return (Array.Empty<ChatMessage>(), passthroughOptions);
    }

    private static bool TryGetCachingDirective(
        ChatOptions options,
        out PromptCacheTtl ttl)
    {
        ttl = PromptCacheTtl.FiveMinutes;

        var additional = options.AdditionalProperties;
        if (additional is null)
        {
            return false;
        }

        if (!TryGetBoolean(additional, PromptCachePolicy.EnabledExtensionKey, out var enabled) ||
            !enabled)
        {
            return false;
        }

        if (TryGetString(additional, PromptCachePolicy.TtlExtensionKey, out var token) &&
            PromptCachePolicy.TryParseTtl(token, out var parsed))
        {
            ttl = parsed;
        }

        return true;
    }

    private static void ApplyCacheControls(
        MessageParameters parameters,
        PromptCacheTtl ttl)
    {
        var duration = ttl == PromptCacheTtl.OneHour
            ? CacheDuration.OneHour
            : CacheDuration.FiveMinutes;

        if (parameters.System is { Count: > 0 })
        {
            var lastSystem = parameters.System[^1];
            lastSystem.CacheControl ??= new CacheControl();
            lastSystem.CacheControl.Type = CacheControlType.ephemeral;
            lastSystem.CacheControl.TTL = duration;
        }

        if (parameters.Tools is { Count: > 0 } &&
            parameters.Tools[^1].Function is Function function)
        {
            function.CacheControl ??= new CacheControl();
            function.CacheControl.Type = CacheControlType.ephemeral;
            function.CacheControl.TTL = duration;
        }
    }

    private static bool TryGetBoolean(
        IDictionary<string, object?> additionalProperties,
        string key,
        out bool value)
    {
        if (!additionalProperties.TryGetValue(key, out var raw) || raw is null)
        {
            value = false;
            return false;
        }

        switch (raw)
        {
            case bool b:
                value = b;
                return true;

            case string s when bool.TryParse(s, out var parsed):
                value = parsed;
                return true;

            case JsonElement { ValueKind: JsonValueKind.True }:
                value = true;
                return true;

            case JsonElement { ValueKind: JsonValueKind.False }:
                value = false;
                return true;

            default:
                value = false;
                return false;
        }
    }

    private static bool TryGetString(
        IDictionary<string, object?> additionalProperties,
        string key,
        out string? value)
    {
        if (!additionalProperties.TryGetValue(key, out var raw) || raw is null)
        {
            value = null;
            return false;
        }

        switch (raw)
        {
            case string s:
                value = s;
                return true;

            case JsonElement { ValueKind: JsonValueKind.String } element:
                value = element.GetString();
                return value is not null;

            default:
                value = null;
                return false;
        }
    }
}
