using System.Text.Json.Nodes;
using FluentAssertions;
using JD.AI.Core.Skills;

namespace JD.AI.Tests.Skills;

public sealed class SkillLifecycleModelsTests
{
    // ── SkillSourceKind enum ─────────────────────────────────────────────

    [Theory]
    [InlineData(SkillSourceKind.Bundled, 0)]
    [InlineData(SkillSourceKind.Managed, 1)]
    [InlineData(SkillSourceKind.Workspace, 2)]
    public void SkillSourceKind_Values(SkillSourceKind kind, int expected) =>
        ((int)kind).Should().Be(expected);

    // ── SkillEligibilityState enum ───────────────────────────────────────

    [Theory]
    [InlineData(SkillEligibilityState.Active, 0)]
    [InlineData(SkillEligibilityState.Excluded, 1)]
    [InlineData(SkillEligibilityState.Shadowed, 2)]
    [InlineData(SkillEligibilityState.Invalid, 3)]
    public void SkillEligibilityState_Values(SkillEligibilityState state, int expected) =>
        ((int)state).Should().Be(expected);

    // ── SkillReasonCodes constants ───────────────────────────────────────

    [Fact]
    public void SkillReasonCodes_None() =>
        SkillReasonCodes.None.Should().Be("none");

    [Fact]
    public void SkillReasonCodes_InvalidFrontmatter() =>
        SkillReasonCodes.InvalidFrontmatter.Should().Be("invalid_frontmatter");

    [Fact]
    public void SkillReasonCodes_InvalidSchema() =>
        SkillReasonCodes.InvalidSchema.Should().Be("invalid_schema");

    [Fact]
    public void SkillReasonCodes_ShadowedByPrecedence() =>
        SkillReasonCodes.ShadowedByPrecedence.Should().Be("shadowed_by_precedence");

    [Fact]
    public void SkillReasonCodes_DisabledByConfig() =>
        SkillReasonCodes.DisabledByConfig.Should().Be("disabled_by_config");

    [Fact]
    public void SkillReasonCodes_BundledNotAllowlisted() =>
        SkillReasonCodes.BundledNotAllowlisted.Should().Be("bundled_not_allowlisted");

    [Fact]
    public void SkillReasonCodes_OsMismatch() =>
        SkillReasonCodes.OsMismatch.Should().Be("os_mismatch");

    [Fact]
    public void SkillReasonCodes_MissingBinaries() =>
        SkillReasonCodes.MissingBinaries.Should().Be("missing_bins");

    [Fact]
    public void SkillReasonCodes_MissingAnyBinary() =>
        SkillReasonCodes.MissingAnyBinary.Should().Be("missing_any_bins");

    [Fact]
    public void SkillReasonCodes_MissingEnvironment() =>
        SkillReasonCodes.MissingEnvironment.Should().Be("missing_env");

    [Fact]
    public void SkillReasonCodes_MissingConfig() =>
        SkillReasonCodes.MissingConfig.Should().Be("missing_config");

    // ── SkillSourceDirectory record ──────────────────────────────────────

    [Fact]
    public void SkillSourceDirectory_Construction()
    {
        var dir = new SkillSourceDirectory("user", "/home/.skills", SkillSourceKind.Workspace, 5);
        dir.Name.Should().Be("user");
        dir.RootPath.Should().Be("/home/.skills");
        dir.Kind.Should().Be(SkillSourceKind.Workspace);
        dir.Order.Should().Be(5);
    }

    [Fact]
    public void SkillSourceDirectory_OrderDefaultsToZero()
    {
        var dir = new SkillSourceDirectory("bundled", "/app/skills", SkillSourceKind.Bundled);
        dir.Order.Should().Be(0);
    }

    [Fact]
    public void SkillSourceDirectory_RecordEquality()
    {
        var a = new SkillSourceDirectory("x", "/x", SkillSourceKind.Managed, 1);
        var b = new SkillSourceDirectory("x", "/x", SkillSourceKind.Managed, 1);
        a.Should().Be(b);
    }

    // ── SkillMetadata record ─────────────────────────────────────────────

    [Fact]
    public void SkillMetadata_Construction()
    {
        var meta = new SkillMetadata(
            Name: "test-skill",
            Description: "A test skill",
            SkillKey: "test-skill",
            Always: false,
            PrimaryEnv: null,
            Os: [],
            RequiredBins: [],
            RequiredAnyBins: [],
            RequiredEnvironment: [],
            RequiredConfigPaths: []);

        meta.Name.Should().Be("test-skill");
        meta.Description.Should().Be("A test skill");
        meta.Always.Should().BeFalse();
        meta.PrimaryEnv.Should().BeNull();
        meta.Os.Should().BeEmpty();
        meta.RequiredBins.Should().BeEmpty();
    }

    // ── SkillEntryConfig.Merge ───────────────────────────────────────────

    [Fact]
    public void Merge_BothNull_ReturnsEmptyConfig()
    {
        var result = SkillEntryConfig.Merge(null, null);
        result.Enabled.Should().BeNull();
        result.ApiKey.Should().BeNull();
        result.Env.Should().BeEmpty();
        result.Config.Should().BeNull();
    }

    [Fact]
    public void Merge_LowerNull_ClonesHigher()
    {
        var higher = new SkillEntryConfig
        {
            Enabled = true,
            ApiKey = "key-h",
            Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["A"] = "1" },
        };

        var result = SkillEntryConfig.Merge(null, higher);
        result.Enabled.Should().BeTrue();
        result.ApiKey.Should().Be("key-h");
        result.Env.Should().ContainKey("A");
    }

    [Fact]
    public void Merge_HigherNull_ClonesLower()
    {
        var lower = new SkillEntryConfig
        {
            Enabled = false,
            ApiKey = "key-l",
        };

        var result = SkillEntryConfig.Merge(lower, null);
        result.Enabled.Should().BeFalse();
        result.ApiKey.Should().Be("key-l");
    }

    [Fact]
    public void Merge_HigherOverridesLower()
    {
        var lower = new SkillEntryConfig
        {
            Enabled = false,
            ApiKey = "old",
            Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["A"] = "1", ["B"] = "2" },
        };
        var higher = new SkillEntryConfig
        {
            Enabled = true,
            Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["B"] = "override", ["C"] = "3" },
        };

        var result = SkillEntryConfig.Merge(lower, higher);
        result.Enabled.Should().BeTrue();
        result.ApiKey.Should().Be("old"); // higher.ApiKey is null, falls to lower
        result.Env["A"].Should().Be("1");
        result.Env["B"].Should().Be("override");
        result.Env["C"].Should().Be("3");
    }

    [Fact]
    public void Merge_Config_HigherWins()
    {
        var lower = new SkillEntryConfig
        {
            Config = JsonNode.Parse("""{"x": 1}"""),
        };
        var higher = new SkillEntryConfig
        {
            Config = JsonNode.Parse("""{"y": 2}"""),
        };

        var result = SkillEntryConfig.Merge(lower, higher);
        result.Config!["y"]!.GetValue<int>().Should().Be(2);
    }

    [Fact]
    public void Merge_Config_FallsToLowerWhenHigherNull()
    {
        var lower = new SkillEntryConfig
        {
            Config = JsonNode.Parse("""{"x": 1}"""),
        };
        var higher = new SkillEntryConfig();

        var result = SkillEntryConfig.Merge(lower, higher);
        result.Config!["x"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public void Merge_Clone_IsIndependent()
    {
        var higher = new SkillEntryConfig
        {
            Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["K"] = "V" },
        };

        var result = SkillEntryConfig.Merge(null, higher);
        // Mutating the clone should not affect original
        ((Dictionary<string, string>)result.Env)["K"] = "changed";
        higher.Env["K"].Should().Be("V");
    }

    // ── SkillRuntimeConfig ───────────────────────────────────────────────

    [Fact]
    public void SkillRuntimeConfig_Defaults()
    {
        var config = new SkillRuntimeConfig();
        config.Watch.Should().BeTrue();
        config.WatchDebounceMs.Should().Be(250);
        config.AllowBundled.Should().BeEmpty();
        config.Entries.Should().BeEmpty();
        config.RootConfig.Should().BeNull();
    }

    [Fact]
    public void SkillRuntimeConfig_GetEntry_ReturnsEntryWhenPresent()
    {
        var entry = new SkillEntryConfig { Enabled = true };
        var config = new SkillRuntimeConfig
        {
            Entries = new Dictionary<string, SkillEntryConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["my-skill"] = entry,
            },
        };

        config.GetEntry("my-skill").Enabled.Should().BeTrue();
    }

    [Fact]
    public void SkillRuntimeConfig_GetEntry_ReturnsFreshWhenMissing()
    {
        var config = new SkillRuntimeConfig();
        var entry = config.GetEntry("nonexistent");
        entry.Enabled.Should().BeNull();
        entry.ApiKey.Should().BeNull();
    }
}
