using JD.AI.Core.Governance;
using Xunit;

namespace JD.AI.Tests;

public sealed class PolicyRbacTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private static PolicyEvaluator BuildEvaluator(PolicySpec spec) =>
        new(spec);

    private static PolicySpec WithRoles(Dictionary<string, RoleDefinition> definitions) =>
        new() { Roles = new RolePolicy { Definitions = definitions } };

    // ── EvaluateTool ────────────────────────────────────────────────────────

    [Fact]
    public void EvaluateTool_NoRolePolicy_AllowsEverything()
    {
        var ev = BuildEvaluator(new PolicySpec());
        var result = ev.EvaluateTool("ReadFile", new PolicyContext());
        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    [Fact]
    public void EvaluateTool_RoleDenyWins_EvenIfBaseAllows()
    {
        var spec = WithRoles(new Dictionary<string, RoleDefinition>
        {
            ["guest"] = new() { DenyTools = ["ReadFile"] }
        });

        var ev = BuildEvaluator(spec);
        var ctx = new PolicyContext(RoleName: "guest");
        var result = ev.EvaluateTool("ReadFile", ctx);

        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    [Fact]
    public void EvaluateTool_RoleAllowOverridesBaseDeny()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Denied = ["ReadFile"] },
            Roles = new RolePolicy
            {
                Definitions = new Dictionary<string, RoleDefinition>
                {
                    ["superuser"] = new() { AllowTools = ["ReadFile"] }
                }
            }
        };

        var ev = BuildEvaluator(spec);
        var ctx = new PolicyContext(RoleName: "superuser");
        var result = ev.EvaluateTool("ReadFile", ctx);

        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    [Fact]
    public void EvaluateTool_RoleAllowExtendBaseAllowedList()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Allowed = ["ListFiles"] },
            Roles = new RolePolicy
            {
                Definitions = new Dictionary<string, RoleDefinition>
                {
                    ["developer"] = new() { AllowTools = ["ReadFile", "WriteFile"] }
                }
            }
        };

        var ev = BuildEvaluator(spec);
        var ctx = new PolicyContext(RoleName: "developer");

        // Base allowed list: ListFiles only
        // Role adds ReadFile — should be allowed
        var result = ev.EvaluateTool("ReadFile", ctx);
        Assert.Equal(PolicyDecision.Allow, result.Decision);

        // Tool not in base OR role — should still be denied
        var result2 = ev.EvaluateTool("ExecuteCode", ctx);
        Assert.Equal(PolicyDecision.Deny, result2.Decision);
    }

    // ── Role inheritance ─────────────────────────────────────────────────────

    [Fact]
    public void EvaluateTool_InheritedRoleDenyApplied()
    {
        var spec = WithRoles(new Dictionary<string, RoleDefinition>
        {
            ["base"] = new() { DenyTools = ["DangerousTool"] },
            ["derived"] = new() { Inherits = ["base"] }
        });

        var ev = BuildEvaluator(spec);
        var ctx = new PolicyContext(RoleName: "derived");
        var result = ev.EvaluateTool("DangerousTool", ctx);

        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    [Fact]
    public void EvaluateTool_CircularInheritanceDoesNotInfiniteLoop()
    {
        var spec = WithRoles(new Dictionary<string, RoleDefinition>
        {
            ["a"] = new() { Inherits = ["b"] },
            ["b"] = new() { Inherits = ["a"], DenyTools = ["BannedTool"] }
        });

        var ev = BuildEvaluator(spec);
        var ctx = new PolicyContext(RoleName: "a");

        // Should not throw; cycle guard should break the loop
        var result = ev.EvaluateTool("BannedTool", ctx);
        // "b" denies BannedTool and "a" inherits from "b", so deny expected
        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    // ── EvaluateProvider ─────────────────────────────────────────────────────

    [Fact]
    public void EvaluateProvider_RoleDenyWins()
    {
        var spec = WithRoles(new Dictionary<string, RoleDefinition>
        {
            ["restricted"] = new() { DenyProviders = ["OpenAI"] }
        });

        var ev = BuildEvaluator(spec);
        var ctx = new PolicyContext(RoleName: "restricted");
        var result = ev.EvaluateProvider("OpenAI", ctx);

        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    [Fact]
    public void EvaluateProvider_RoleAllowOverridesBaseDeny()
    {
        var spec = new PolicySpec
        {
            Providers = new ProviderPolicy { Denied = ["OpenAI"] },
            Roles = new RolePolicy
            {
                Definitions = new Dictionary<string, RoleDefinition>
                {
                    ["admin"] = new() { AllowProviders = ["OpenAI"] }
                }
            }
        };

        var ev = BuildEvaluator(spec);
        var ctx = new PolicyContext(RoleName: "admin");
        var result = ev.EvaluateProvider("OpenAI", ctx);

        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    // ── EvaluateModel ────────────────────────────────────────────────────────

    [Fact]
    public void EvaluateModel_RoleDenyModelPattern()
    {
        var spec = WithRoles(new Dictionary<string, RoleDefinition>
        {
            ["limited"] = new() { DenyModels = ["gpt-4*"] }
        });

        var ev = BuildEvaluator(spec);
        var ctx = new PolicyContext(RoleName: "limited");
        var result = ev.EvaluateModel("gpt-4-turbo", null, ctx);

        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    // ── PolicyContext record ──────────────────────────────────────────────────

    [Fact]
    public void PolicyContext_SupportsRoleAndGroups()
    {
        var ctx = new PolicyContext(
            UserId: "alice",
            RoleName: "developer",
            Groups: ["engineering", "security"]);

        Assert.Equal("alice", ctx.UserId);
        Assert.Equal("developer", ctx.RoleName);
        Assert.Contains("engineering", ctx.Groups!);
    }

    // ── PolicyResolver merges RolePolicy ─────────────────────────────────────

    [Fact]
    public void PolicyResolver_MergesRolePoliciesAcrossScopes()
    {
        var global = new PolicyDocument
        {
            Metadata = new PolicyMetadata { Scope = PolicyScope.Global },
            Spec = new PolicySpec
            {
                Roles = new RolePolicy
                {
                    Definitions = new Dictionary<string, RoleDefinition>
                    {
                        ["developer"] = new() { AllowTools = ["ReadFile"] }
                    }
                }
            }
        };

        var user = new PolicyDocument
        {
            Metadata = new PolicyMetadata { Scope = PolicyScope.User },
            Spec = new PolicySpec
            {
                Roles = new RolePolicy
                {
                    Definitions = new Dictionary<string, RoleDefinition>
                    {
                        ["developer"] = new() { AllowTools = ["WriteFile"] }
                    }
                }
            }
        };

        var resolved = PolicyResolver.Resolve([global, user]);

        Assert.NotNull(resolved.Roles);
        Assert.True(resolved.Roles!.Definitions.TryGetValue("developer", out var def));
        Assert.Contains("ReadFile", def!.AllowTools);
        Assert.Contains("WriteFile", def.AllowTools);
    }
}
