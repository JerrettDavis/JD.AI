using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class ComplianceCheckTests
{
    // ── ResolveExtension ──────────────────────────────────────────────────

    [Fact]
    public void ResolveExtension_NullExtends_ReturnsNull()
    {
        var spec = new PolicySpec { Extends = null };
        CompliancePresetLoader.ResolveExtension(spec).Should().BeNull();
    }

    [Fact]
    public void ResolveExtension_EmptyExtends_ReturnsNull()
    {
        var spec = new PolicySpec { Extends = "" };
        CompliancePresetLoader.ResolveExtension(spec).Should().BeNull();
    }

    [Fact]
    public void ResolveExtension_WhitespaceExtends_ReturnsNull()
    {
        var spec = new PolicySpec { Extends = "   " };
        CompliancePresetLoader.ResolveExtension(spec).Should().BeNull();
    }

    [Fact]
    public void ResolveExtension_ValidPreset_ReturnsDocument()
    {
        var spec = new PolicySpec { Extends = "jdai/compliance/soc2" };
        var doc = CompliancePresetLoader.ResolveExtension(spec);
        doc.Should().NotBeNull();
    }

    [Fact]
    public void ResolveExtension_UnknownPreset_ReturnsNull()
    {
        var spec = new PolicySpec { Extends = "jdai/compliance/nonexistent" };
        CompliancePresetLoader.ResolveExtension(spec).Should().BeNull();
    }

    // ── Check GDPR ────────────────────────────────────────────────────────

    [Fact]
    public void CheckGdpr_EmptyPolicy_HasFailures()
    {
        var controls = CompliancePresetLoader.Check("jdai/compliance/gdpr", new PolicySpec());
        controls.Should().HaveCount(4);
        controls.Should().Contain(c => !c.Pass);
    }

    [Fact]
    public void CheckGdpr_CompliantPolicy_AllPass()
    {
        var spec = new PolicySpec
        {
            Audit = new AuditPolicy { Enabled = true },
            Sessions = new SessionPolicy { RetentionDays = 90 },
            Data = new DataPolicy
            {
                Classifications =
                [
                    new DataClassification
                    {
                        Name = "PII-Email",
                        Action = ClassificationAction.RedactAndAudit,
                    },
                ],
                NoExternalProviders = ["*"],
            },
        };

        var controls = CompliancePresetLoader.Check("jdai/compliance/gdpr", spec);
        controls.Should().OnlyContain(c => c.Pass);
    }

    [Fact]
    public void CheckGdpr_RetentionTooLong_Fails()
    {
        var spec = new PolicySpec
        {
            Sessions = new SessionPolicy { RetentionDays = 365 },
        };

        var controls = CompliancePresetLoader.Check("jdai/compliance/gdpr", spec);
        var retention = controls.First(c => string.Equals(c.Id, "GDPR-RT-1", StringComparison.Ordinal));
        retention.Pass.Should().BeFalse();
        retention.Remediation.Should().NotBeNull();
    }

    // ── Check HIPAA ───────────────────────────────────────────────────────

    [Fact]
    public void CheckHipaa_EmptyPolicy_HasFailures()
    {
        var controls = CompliancePresetLoader.Check("jdai/compliance/hipaa", new PolicySpec());
        controls.Should().HaveCount(4);
        controls.Should().Contain(c => !c.Pass);
    }

    [Fact]
    public void CheckHipaa_CompliantPolicy_AllPass()
    {
        var spec = new PolicySpec
        {
            Audit = new AuditPolicy { Enabled = true },
            Sessions = new SessionPolicy { RetentionDays = 2555 },
            Data = new DataPolicy
            {
                Classifications =
                [
                    new DataClassification
                    {
                        Name = "PHI",
                        Action = ClassificationAction.DenyAndAudit,
                    },
                ],
                NoExternalProviders = ["*"],
            },
        };

        var controls = CompliancePresetLoader.Check("jdai/compliance/hipaa", spec);
        controls.Should().OnlyContain(c => c.Pass);
    }

    [Fact]
    public void CheckHipaa_PhiWithoutDeny_Fails()
    {
        var spec = new PolicySpec
        {
            Data = new DataPolicy
            {
                Classifications =
                [
                    new DataClassification
                    {
                        Name = "PHI",
                        Action = ClassificationAction.RedactAndAudit, // Not DenyAndAudit
                    },
                ],
            },
        };

        var controls = CompliancePresetLoader.Check("jdai/compliance/hipaa", spec);
        var phiControl = controls.First(c => string.Equals(c.Id, "HIPAA-DP-1", StringComparison.Ordinal));
        phiControl.Pass.Should().BeFalse();
    }

    // ── Check PCI-DSS ─────────────────────────────────────────────────────

    [Fact]
    public void CheckPciDss_EmptyPolicy_HasFailures()
    {
        var controls = CompliancePresetLoader.Check("jdai/compliance/pci-dss", new PolicySpec());
        controls.Should().HaveCount(5);
        controls.Should().Contain(c => !c.Pass);
    }

    [Fact]
    public void CheckPciDss_CompliantPolicy_AllPass()
    {
        var spec = new PolicySpec
        {
            Audit = new AuditPolicy { Enabled = true },
            Sessions = new SessionPolicy { RetentionDays = 365 },
            Data = new DataPolicy
            {
                Classifications =
                [
                    new DataClassification { Name = "PAN", Action = ClassificationAction.DenyAndAudit },
                    new DataClassification { Name = "CVV", Action = ClassificationAction.DenyAndAudit },
                ],
                NoExternalProviders = ["*"],
            },
        };

        var controls = CompliancePresetLoader.Check("jdai/compliance/pci-dss", spec);
        controls.Should().OnlyContain(c => c.Pass);
    }

    [Fact]
    public void CheckPciDss_MissingPan_Fails()
    {
        var spec = new PolicySpec
        {
            Audit = new AuditPolicy { Enabled = true },
            Data = new DataPolicy
            {
                Classifications =
                [
                    new DataClassification { Name = "CVV", Action = ClassificationAction.DenyAndAudit },
                ],
            },
        };

        var controls = CompliancePresetLoader.Check("jdai/compliance/pci-dss", spec);
        var panControl = controls.First(c => string.Equals(c.Id, "PCI-DP-1", StringComparison.Ordinal));
        panControl.Pass.Should().BeFalse();
    }

    // ── ComplianceControl model ───────────────────────────────────────────

    [Fact]
    public void ComplianceControl_Passing_HasNoRemediation()
    {
        var controls = CompliancePresetLoader.Check("jdai/compliance/soc2", new PolicySpec
        {
            Audit = new AuditPolicy { Enabled = true },
        });

        var auditControl = controls.First(c => string.Equals(c.Id, "SOC2-AU-1", StringComparison.Ordinal));
        auditControl.Pass.Should().BeTrue();
        auditControl.Remediation.Should().BeNull();
    }

    [Fact]
    public void ComplianceControl_Failing_HasRemediation()
    {
        var controls = CompliancePresetLoader.Check("jdai/compliance/soc2", new PolicySpec());
        var auditControl = controls.First(c => string.Equals(c.Id, "SOC2-AU-1", StringComparison.Ordinal));
        auditControl.Pass.Should().BeFalse();
        auditControl.Remediation.Should().NotBeNullOrEmpty();
        auditControl.Id.Should().Be("SOC2-AU-1");
        auditControl.Description.Should().NotBeEmpty();
    }
}
