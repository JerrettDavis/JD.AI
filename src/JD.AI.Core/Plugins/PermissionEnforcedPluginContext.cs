using JD.AI.Plugins.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Plugins;

internal sealed class PermissionEnforcedPluginContext : IPluginContext
{
    private const string AllServicesPermission = "service:*";
    private const string AllEventsPermission = "event:*";
    private const string LegacyReadEventsPermission = "read-events";

    private readonly IPluginContext _inner;
    private readonly string _pluginId;
    private readonly ILogger _logger;
    private readonly HashSet<string> _permissions;

    public PermissionEnforcedPluginContext(
        IPluginContext inner,
        string pluginId,
        IEnumerable<string> declaredPermissions,
        ILogger logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _pluginId = string.IsNullOrWhiteSpace(pluginId)
            ? "unknown-plugin"
            : pluginId.Trim();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _permissions = declaredPermissions
            .Select(static p => p?.Trim())
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public Kernel Kernel => _inner.Kernel;

    public IReadOnlyDictionary<string, string> Configuration => _inner.Configuration;

    public void OnEvent(string eventType, Func<object?, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(handler);

        var normalizedEvent = eventType.Trim();
        var eventPermission = $"event:{normalizedEvent}";
        if (!HasAnyPermission(AllEventsPermission, eventPermission, LegacyReadEventsPermission))
        {
            throw Denied(
                operation: "event subscription",
                requiredPermissions: ["event:*", eventPermission, LegacyReadEventsPermission]);
        }

        Audit(operation: "event subscription", resource: normalizedEvent, allowed: true);
        _inner.OnEvent(normalizedEvent, handler);
    }

    public T? GetService<T>() where T : class
    {
        var serviceType = typeof(T);
        var fullName = serviceType.FullName ?? serviceType.Name;
        var requiredFull = $"service:{fullName}";
        var requiredShort = $"service:{serviceType.Name}";

        if (!HasAnyPermission(AllServicesPermission, requiredFull, requiredShort))
        {
            throw Denied(
                operation: "service resolution",
                requiredPermissions: ["service:*", requiredFull, requiredShort]);
        }

        Audit(operation: "service resolution", resource: fullName, allowed: true);
        return _inner.GetService<T>();
    }

    public void Log(PluginLogLevel level, string message)
    {
        _inner.Log(level, message);
    }

    private bool HasAnyPermission(params string[] requiredPermissions)
    {
        return requiredPermissions.Any(p => _permissions.Contains(p));
    }

    private InvalidOperationException Denied(string operation, IReadOnlyList<string> requiredPermissions)
    {
        Audit(operation, resource: string.Join(", ", requiredPermissions), allowed: false);
        return new InvalidOperationException(
            $"Plugin '{_pluginId}' permission denied for {operation}. " +
            $"Declare one of: {string.Join(", ", requiredPermissions)}.");
    }

    private void Audit(string operation, string resource, bool allowed)
    {
        _logger.LogInformation(
            "Plugin security audit: plugin={PluginId} operation={Operation} resource={Resource} allowed={Allowed}",
            _pluginId,
            operation,
            Sanitize(resource),
            allowed);
    }

    private static string Sanitize(string value)
    {
        return value
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
