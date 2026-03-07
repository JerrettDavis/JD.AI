using JD.AI.Connectors.Jira;
using JD.AI.Connectors.Sdk;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Tests.Connectors;

public sealed class ConnectorRegistryTests
{
    [Fact]
    public void Register_WithValidConnector_ReturnsDescriptor()
    {
        var services = new ServiceCollection();
        var registry = new ConnectorRegistry();

        var descriptor = registry.Register(new JiraConnector(), services);

        Assert.Equal("jira", descriptor.Name);
        Assert.Equal("Atlassian Jira", descriptor.DisplayName);
        Assert.Equal("1.0.0", descriptor.Version);
    }

    [Fact]
    public void Register_WithValidConnector_AddsToolPluginTypes()
    {
        var services = new ServiceCollection();
        var registry = new ConnectorRegistry();

        var descriptor = registry.Register(new JiraConnector(), services);

        Assert.Contains(typeof(JiraIssueTool), descriptor.ToolPluginTypes);
    }

    [Fact]
    public void Register_WithValidConnector_AddsReadonlyLoadout()
    {
        var services = new ServiceCollection();
        var registry = new ConnectorRegistry();

        var descriptor = registry.Register(new JiraConnector(), services);

        Assert.True(descriptor.Loadouts.ContainsKey("jira-readonly"));
    }

    [Fact]
    public void Register_WithoutAttribute_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var registry = new ConnectorRegistry();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new NoAttributeConnector(), services));
    }

    [Fact]
    public void Get_AfterRegister_ReturnsDescriptor()
    {
        var services = new ServiceCollection();
        var registry = new ConnectorRegistry();
        registry.Register(new JiraConnector(), services);

        var result = registry.Get("jira");

        Assert.NotNull(result);
        Assert.Equal("jira", result.Name);
    }

    [Fact]
    public void Get_CaseInsensitive_ReturnsDescriptor()
    {
        var services = new ServiceCollection();
        var registry = new ConnectorRegistry();
        registry.Register(new JiraConnector(), services);

        Assert.NotNull(registry.Get("JIRA"));
        Assert.NotNull(registry.Get("Jira"));
    }

    [Fact]
    public void Get_UnknownName_ReturnsNull()
    {
        var registry = new ConnectorRegistry();

        Assert.Null(registry.Get("unknown"));
    }

    [Fact]
    public void SetEnabled_ExistingConnector_UpdatesFlag()
    {
        var services = new ServiceCollection();
        var registry = new ConnectorRegistry();
        var descriptor = registry.Register(new JiraConnector(), services);

        Assert.True(descriptor.IsEnabled); // default

        var updated = registry.SetEnabled("jira", false);

        Assert.True(updated);
        Assert.False(descriptor.IsEnabled);
    }

    [Fact]
    public void SetEnabled_UnknownConnector_ReturnsFalse()
    {
        var registry = new ConnectorRegistry();

        Assert.False(registry.SetEnabled("nonexistent", false));
    }

    [Fact]
    public void All_ReturnsAllRegisteredConnectors()
    {
        var services = new ServiceCollection();
        var registry = new ConnectorRegistry();
        registry.Register(new JiraConnector(), services);

        Assert.Single(registry.All);
    }

    [Fact]
    public void ScanAndRegister_FindsJiraConnector()
    {
        var services = new ServiceCollection();
        var registry = new ConnectorRegistry();

        registry.ScanAndRegister([typeof(JiraConnector).Assembly], services);

        Assert.NotNull(registry.Get("jira"));
    }

    [Fact]
    public void JiraLoadout_ReadonlyFilter_OnlyMatchesReadMethods()
    {
        var services = new ServiceCollection();
        var registry = new ConnectorRegistry();
        var descriptor = registry.Register(new JiraConnector(), services);

        var filter = descriptor.Loadouts["jira-readonly"];

        Assert.True(filter("jira_get_issue"));
        Assert.True(filter("jira_search_issues"));
        Assert.False(filter("jira_create_issue"));
    }

    /// <summary>Connector without [JdAiConnector] attribute — used to test error path.</summary>
    private sealed class NoAttributeConnector : IConnector
    {
        public void Configure(IConnectorBuilder builder) { }
    }
}
