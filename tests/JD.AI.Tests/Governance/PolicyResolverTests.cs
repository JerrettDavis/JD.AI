using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class PolicyResolverTests
{
    private static PolicyDocument MakeDoc(
        string name,
        PolicyScope scope,
        int priority = 0,
        PolicySpec? spec = null) => new()
    {
        Metadata = new PolicyMetadata { Name = name, Scope = scope, Priority = priority },
        Spec = spec ?? new PolicySpec(),
    };

    [Fact]
    public void Resolve_EmptyList_ReturnsEmptySpec()
    {
        var result = PolicyResolver.Resolve([]);

        result.Should().NotBeNull();
        result.Tools.Should().BeNull();
        result.Budget.Should().BeNull();
    }

    [Fact]
    public void Resolve_SinglePolicy_ReturnsItsSpec()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Allowed = ["read_file"], Denied = ["shell_exec"] },
        };
        var doc = MakeDoc("only", PolicyScope.User, spec: spec);

        var result = PolicyResolver.Resolve([doc]);

        result.Tools.Should().NotBeNull();
        result.Tools!.Allowed.Should().Contain("read_file");
        result.Tools.Denied.Should().Contain("shell_exec");
    }

    [Fact]
    public void Resolve_ToolAllowed_TakesIntersection()
    {
        var global = MakeDoc("global", PolicyScope.Global, spec: new PolicySpec
        {
            Tools = new ToolPolicy { Allowed = ["read_file", "write_file", "search"] },
        });
        var user = MakeDoc("user", PolicyScope.User, spec: new PolicySpec
        {
            Tools = new ToolPolicy { Allowed = ["read_file", "search"] },
        });

        var result = PolicyResolver.Resolve([global, user]);

        result.Tools!.Allowed.Should().BeEquivalentTo(["read_file", "search"]);
        result.Tools.Allowed.Should().NotContain("write_file");
    }

    [Fact]
    public void Resolve_ToolDenied_TakesUnion()
    {
        var global = MakeDoc("global", PolicyScope.Global, spec: new PolicySpec
        {
            Tools = new ToolPolicy { Denied = ["shell_exec"] },
        });
        var user = MakeDoc("user", PolicyScope.User, spec: new PolicySpec
        {
            Tools = new ToolPolicy { Denied = ["network_call"] },
        });

        var result = PolicyResolver.Resolve([global, user]);

        result.Tools!.Denied.Should().Contain("shell_exec");
        result.Tools.Denied.Should().Contain("network_call");
    }

    [Fact]
    public void Resolve_ProviderAllowed_TakesIntersection()
    {
        var p1 = MakeDoc("p1", PolicyScope.Global, spec: new PolicySpec
        {
            Providers = new ProviderPolicy { Allowed = ["openai", "anthropic", "ollama"] },
        });
        var p2 = MakeDoc("p2", PolicyScope.User, spec: new PolicySpec
        {
            Providers = new ProviderPolicy { Allowed = ["openai", "ollama"] },
        });

        var result = PolicyResolver.Resolve([p1, p2]);

        result.Providers!.Allowed.Should().BeEquivalentTo(["openai", "ollama"]);
        result.Providers.Allowed.Should().NotContain("anthropic");
    }

    [Fact]
    public void Resolve_ProviderDenied_TakesUnion()
    {
        var p1 = MakeDoc("p1", PolicyScope.Global, spec: new PolicySpec
        {
            Providers = new ProviderPolicy { Denied = ["ollama"] },
        });
        var p2 = MakeDoc("p2", PolicyScope.User, spec: new PolicySpec
        {
            Providers = new ProviderPolicy { Denied = ["local-llm"] },
        });

        var result = PolicyResolver.Resolve([p1, p2]);

        result.Providers!.Denied.Should().Contain("ollama");
        result.Providers.Denied.Should().Contain("local-llm");
    }

    [Fact]
    public void Resolve_Budget_TakesMinimumLimits()
    {
        var p1 = MakeDoc("p1", PolicyScope.Global, spec: new PolicySpec
        {
            Budget = new BudgetPolicy { MaxDailyUsd = 50m, MaxMonthlyUsd = 500m, AlertThresholdPercent = 90 },
        });
        var p2 = MakeDoc("p2", PolicyScope.User, spec: new PolicySpec
        {
            Budget = new BudgetPolicy { MaxDailyUsd = 20m, MaxMonthlyUsd = 200m, AlertThresholdPercent = 70 },
        });

        var result = PolicyResolver.Resolve([p1, p2]);

        result.Budget!.MaxDailyUsd.Should().Be(20m);
        result.Budget.MaxMonthlyUsd.Should().Be(200m);
        result.Budget.AlertThresholdPercent.Should().Be(70);
    }

    [Fact]
    public void Resolve_Budget_NullLimitIgnored()
    {
        var p1 = MakeDoc("p1", PolicyScope.Global, spec: new PolicySpec
        {
            Budget = new BudgetPolicy { MaxDailyUsd = null, MaxMonthlyUsd = 300m },
        });
        var p2 = MakeDoc("p2", PolicyScope.User, spec: new PolicySpec
        {
            Budget = new BudgetPolicy { MaxDailyUsd = 25m, MaxMonthlyUsd = null },
        });

        var result = PolicyResolver.Resolve([p1, p2]);

        result.Budget!.MaxDailyUsd.Should().Be(25m);
        result.Budget.MaxMonthlyUsd.Should().Be(300m);
    }

    [Fact]
    public void Resolve_Models_TakesMinimumContextWindow()
    {
        var p1 = MakeDoc("p1", PolicyScope.Global, spec: new PolicySpec
        {
            Models = new ModelPolicy { MaxContextWindow = 128000 },
        });
        var p2 = MakeDoc("p2", PolicyScope.User, spec: new PolicySpec
        {
            Models = new ModelPolicy { MaxContextWindow = 32000 },
        });

        var result = PolicyResolver.Resolve([p1, p2]);

        result.Models!.MaxContextWindow.Should().Be(32000);
    }

    [Fact]
    public void Resolve_Models_DeniedUnioned()
    {
        var p1 = MakeDoc("p1", PolicyScope.Global, spec: new PolicySpec
        {
            Models = new ModelPolicy { Denied = ["gpt-4-turbo"] },
        });
        var p2 = MakeDoc("p2", PolicyScope.User, spec: new PolicySpec
        {
            Models = new ModelPolicy { Denied = ["claude-2"] },
        });

        var result = PolicyResolver.Resolve([p1, p2]);

        result.Models!.Denied.Should().Contain("gpt-4-turbo");
        result.Models.Denied.Should().Contain("claude-2");
    }

    [Fact]
    public void Resolve_Sessions_RequireProjectTagAnyTrue()
    {
        var p1 = MakeDoc("p1", PolicyScope.Global, spec: new PolicySpec
        {
            Sessions = new SessionPolicy { RequireProjectTag = false, RetentionDays = 90 },
        });
        var p2 = MakeDoc("p2", PolicyScope.User, spec: new PolicySpec
        {
            Sessions = new SessionPolicy { RequireProjectTag = true, RetentionDays = 30 },
        });

        var result = PolicyResolver.Resolve([p1, p2]);

        result.Sessions!.RequireProjectTag.Should().BeTrue();
        result.Sessions.RetentionDays.Should().Be(30);
    }

    [Fact]
    public void Resolve_ScopeOrdering_GlobalBeforeUser()
    {
        // Global restricts to [read_file, write_file], User allows [read_file, search]
        // Expected intersection: [read_file]
        var user = MakeDoc("user", PolicyScope.User, spec: new PolicySpec
        {
            Tools = new ToolPolicy { Allowed = ["read_file", "search"] },
        });
        var global = MakeDoc("global", PolicyScope.Global, spec: new PolicySpec
        {
            Tools = new ToolPolicy { Allowed = ["read_file", "write_file"] },
        });

        // Pass in reversed order — resolver should still order by scope
        var result = PolicyResolver.Resolve([user, global]);

        result.Tools!.Allowed.Should().BeEquivalentTo(["read_file"]);
    }

    [Fact]
    public void Resolve_PriorityOrdering_LowerPriorityFirst()
    {
        var low = MakeDoc("low", PolicyScope.User, priority: 1, spec: new PolicySpec
        {
            Budget = new BudgetPolicy { MaxDailyUsd = 50m, AlertThresholdPercent = 90 },
        });
        var high = MakeDoc("high", PolicyScope.User, priority: 10, spec: new PolicySpec
        {
            Budget = new BudgetPolicy { MaxDailyUsd = 10m, AlertThresholdPercent = 70 },
        });

        var result = PolicyResolver.Resolve([high, low]);

        result.Budget!.MaxDailyUsd.Should().Be(10m);
        result.Budget.AlertThresholdPercent.Should().Be(70);
    }

    [Fact]
    public void Resolve_NoPoliciesWithSections_ReturnsNullSections()
    {
        var p1 = MakeDoc("p1", PolicyScope.Global);
        var p2 = MakeDoc("p2", PolicyScope.User);

        var result = PolicyResolver.Resolve([p1, p2]);

        result.Tools.Should().BeNull();
        result.Budget.Should().BeNull();
        result.Models.Should().BeNull();
        result.Sessions.Should().BeNull();
    }
}
