using JD.AI.Core.Plugins;
using JD.AI.Plugins.SDK;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests.Plugins;

public sealed class PermissionEnforcedPluginContextTests
{
    [Fact]
    public void GetService_WithoutPermission_Throws()
    {
        var inner = new FakePluginContext();
        var context = new PermissionEnforcedPluginContext(
            inner,
            pluginId: "sample.plugin",
            declaredPermissions: [],
            NullLogger.Instance);

        var ex = Assert.Throws<InvalidOperationException>(() => context.GetService<object>());
        Assert.Contains("permission denied", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("service:*", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetService_WithServicePermission_ReturnsValue()
    {
        var expected = new object();
        var inner = new FakePluginContext
        {
            ServiceFactory = type => type == typeof(object) ? expected : null,
        };

        var context = new PermissionEnforcedPluginContext(
            inner,
            pluginId: "sample.plugin",
            declaredPermissions: [$"service:{typeof(object).FullName}"],
            NullLogger.Instance);

        var resolved = context.GetService<object>();
        Assert.Same(expected, resolved);
    }

    [Fact]
    public void OnEvent_WithoutPermission_Throws()
    {
        var inner = new FakePluginContext();
        var context = new PermissionEnforcedPluginContext(
            inner,
            pluginId: "sample.plugin",
            declaredPermissions: [],
            NullLogger.Instance);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            context.OnEvent("agent.spawned", _ => Task.CompletedTask));

        Assert.Contains("event:*", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OnEvent_WithLegacyReadEventsPermission_AllowsSubscription()
    {
        var inner = new FakePluginContext();
        var context = new PermissionEnforcedPluginContext(
            inner,
            pluginId: "sample.plugin",
            declaredPermissions: ["read-events"],
            NullLogger.Instance);

        context.OnEvent("agent.spawned", _ => Task.CompletedTask);

        Assert.Equal("agent.spawned", inner.LastEventType);
    }

    private sealed class FakePluginContext : IPluginContext
    {
        public Kernel Kernel { get; } = new();
        public IReadOnlyDictionary<string, string> Configuration { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public Func<Type, object?>? ServiceFactory { get; init; }
        public string? LastEventType { get; private set; }

        public void OnEvent(string eventType, Func<object?, Task> handler)
        {
            LastEventType = eventType;
        }

        public T? GetService<T>() where T : class
        {
            return ServiceFactory?.Invoke(typeof(T)) as T;
        }

        public void Log(PluginLogLevel level, string message) { }
    }
}
