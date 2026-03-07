using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

/// <summary>
/// Extended compliance check tests covering SOC2 full-pass,
/// unknown presets, and case-insensitivity.
/// Base SOC2/GDPR/HIPAA/PCI-DSS coverage is in <see cref="ComplianceCheckTests"/>.
/// </summary>
public sealed class ComplianceCheckExtendedTests
{
    // ── SOC2 full compliance ──────────────────────────────────────────────

    [Fact]
    public void CheckSoc2_CompliantPolicy_AllPass()
    {
        var spec = new PolicySpec
        {
            Audit = new AuditPolicy { Enabled = true },
            Sessions = new SessionPolicy { RetentionDays = 365 },
            Data = new DataPolicy
            {
                Classifications =
                [
                    new DataClassification { Name = "APIKey", Action = ClassificationAction.DenyAndAudit },
                    new DataClassification { Name = "CreditCard", Action = ClassificationAction.DenyAndAudit },
                ],
            },
        };

        var controls = CompliancePresetLoader.Check("jdai/compliance/soc2", spec);

        controls.Should().HaveCount(4);
        controls.Should().OnlyContain(c => c.Pass);
    }

    [Fact]
    public void CheckSoc2_EmptyPolicy_AllFail()
    {
        var controls = CompliancePresetLoader.Check("jdai/compliance/soc2", new PolicySpec());

        controls.Should().HaveCount(4);
        controls.Should().OnlyContain(c => !c.Pass);
        controls.Should().OnlyContain(c => c.Remediation != null);
    }

    // ── Unknown preset ────────────────────────────────────────────────────

    [Fact]
    public void Check_UnknownPreset_ReturnsEmpty()
    {
        var controls = CompliancePresetLoader.Check("unknown/preset", new PolicySpec());

        controls.Should().BeEmpty();
    }

    // ── Case-insensitivity ────────────────────────────────────────────────

    [Fact]
    public void Check_UpperCasePresetName_StillMatches()
    {
        var controls = CompliancePresetLoader.Check("JDAI/COMPLIANCE/SOC2", new PolicySpec());

        controls.Should().HaveCount(4);
    }

    [Fact]
    public void Check_MixedCasePresetName_StillMatches()
    {
        var controls = CompliancePresetLoader.Check("JdAi/Compliance/Gdpr", new PolicySpec());

        controls.Should().HaveCount(4);
    }

    // ── SOC2 individual control details ───────────────────────────────────

    [Fact]
    public void CheckSoc2_ApiKeyClassification_CaseInsensitive()
    {
        var spec = new PolicySpec
        {
            Data = new DataPolicy
            {
                Classifications =
                [
                    new DataClassification { Name = "apikey", Action = ClassificationAction.DenyAndAudit },
                ],
            },
        };

        var controls = CompliancePresetLoader.Check("jdai/compliance/soc2", spec);
        var dp1 = controls.First(c => string.Equals(c.Id, "SOC2-DP-1", StringComparison.Ordinal));
        dp1.Pass.Should().BeTrue();
    }

    [Fact]
    public void CheckSoc2_RetentionBelowThreshold_Fails()
    {
        var spec = new PolicySpec
        {
            Sessions = new SessionPolicy { RetentionDays = 364 },
        };

        var controls = CompliancePresetLoader.Check("jdai/compliance/soc2", spec);
        var au2 = controls.First(c => string.Equals(c.Id, "SOC2-AU-2", StringComparison.Ordinal));
        au2.Pass.Should().BeFalse();
        au2.Remediation.Should().Contain("365");
    }

    // ── Available presets ─────────────────────────────────────────────────

    [Fact]
    public void AvailablePresets_ContainsAllFour()
    {
        var presets = CompliancePresetLoader.AvailablePresets;

        presets.Should().HaveCount(4);
        presets.Should().Contain("jdai/compliance/soc2");
        presets.Should().Contain("jdai/compliance/gdpr");
        presets.Should().Contain("jdai/compliance/hipaa");
        presets.Should().Contain("jdai/compliance/pci-dss");
    }

    // ── LoadPreset ────────────────────────────────────────────────────────

    [Fact]
    public void LoadPreset_ValidPreset_ReturnsDocument()
    {
        var doc = CompliancePresetLoader.LoadPreset("jdai/compliance/soc2");

        doc.Should().NotBeNull();
        doc!.Spec.Should().NotBeNull();
    }

    [Fact]
    public void LoadPreset_UnknownPreset_ReturnsNull()
    {
        CompliancePresetLoader.LoadPreset("nonexistent").Should().BeNull();
    }
}
