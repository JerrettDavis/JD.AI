using JD.AI.Core.Governance;
using Xunit;

namespace JD.AI.Tests.Governance;

public sealed class CompliancePresetTests
{
    // ── CompliancePresetLoader.LoadPreset ─────────────────────────────────

    [Theory]
    [InlineData("jdai/compliance/soc2")]
    [InlineData("jdai/compliance/gdpr")]
    [InlineData("jdai/compliance/hipaa")]
    [InlineData("jdai/compliance/pci-dss")]
    public void LoadPreset_BuiltInPreset_ReturnsPolicy(string presetName)
    {
        var doc = CompliancePresetLoader.LoadPreset(presetName);
        Assert.NotNull(doc);
        Assert.Equal(presetName, doc.Spec?.Data is not null || doc.Spec?.Audit is not null
            ? doc.Metadata.Name
            : presetName); // name should match
    }

    [Fact]
    public void LoadPreset_Unknown_ReturnsNull()
    {
        var doc = CompliancePresetLoader.LoadPreset("jdai/compliance/nonexistent");
        Assert.Null(doc);
    }

    [Fact]
    public void AvailablePresets_ContainsAllFour()
    {
        var presets = CompliancePresetLoader.AvailablePresets;
        Assert.Contains("jdai/compliance/soc2", presets);
        Assert.Contains("jdai/compliance/gdpr", presets);
        Assert.Contains("jdai/compliance/hipaa", presets);
        Assert.Contains("jdai/compliance/pci-dss", presets);
    }

    // ── PolicyResolver preset expansion ───────────────────────────────────

    [Fact]
    public void PolicyResolver_Extends_MergesPresetAsBase()
    {
        var userDoc = new PolicyDocument
        {
            Metadata = new PolicyMetadata { Scope = PolicyScope.User, Priority = 10 },
            Spec = new PolicySpec
            {
                Extends = "jdai/compliance/soc2",
                Audit = new AuditPolicy { Enabled = true }, // user doc enables audit
            }
        };

        var resolved = PolicyResolver.Resolve([userDoc]);

        // The SOC2 preset has data classifications — verify they come through
        Assert.NotNull(resolved.Data);
        Assert.NotEmpty(resolved.Data.Classifications);
    }

    [Fact]
    public void PolicyResolver_UserOverridesPreset_UserWins()
    {
        var userDoc = new PolicyDocument
        {
            Metadata = new PolicyMetadata { Scope = PolicyScope.User, Priority = 10 },
            Spec = new PolicySpec
            {
                Extends = "jdai/compliance/soc2",
                Data = new DataPolicy
                {
                    Classifications =
                    [
                        new DataClassification
                        {
                            Name = "SSN",
                            Patterns = ["\\d{3}-\\d{2}-\\d{4}"],
                            Action = ClassificationAction.AuditOnly  // weaker than preset
                        }
                    ]
                }
            }
        };

        var resolved = PolicyResolver.Resolve([userDoc]);

        // The user's SSN classification should win (it comes after the preset in merge order)
        var ssnCls = resolved.Data?.Classifications?.FirstOrDefault(c =>
            c.Name.Equals("SSN", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(ssnCls);
        Assert.Equal(ClassificationAction.AuditOnly, ssnCls.Action);
    }

    // ── DataRedactor classification support ───────────────────────────────

    [Fact]
    public void DataRedactor_Redact_ClassificationRedactAndAudit_ReplacesContent()
    {
        var cls = new DataClassification
        {
            Name = "SSN",
            Patterns = [@"\b\d{3}-\d{2}-\d{4}\b"],
            Action = ClassificationAction.RedactAndAudit,
        };
        var redactor = new DataRedactor([], [cls]);
        var result = redactor.Redact("SSN: 123-45-6789");
        Assert.DoesNotContain("123-45-6789", result);
        Assert.Contains("[REDACTED:SSN]", result);
    }

    [Fact]
    public void DataRedactor_RedactWithClassifications_DenyAndAudit_SetsShouldDeny()
    {
        var cls = new DataClassification
        {
            Name = "PAN",
            Patterns = [@"\b\d{16}\b"],
            Action = ClassificationAction.DenyAndAudit,
        };
        var redactor = new DataRedactor([], [cls]);
        var result = redactor.RedactWithClassifications("Card: 4111111111111111");
        Assert.True(result.ShouldDeny);
        Assert.True(result.HasMatches);
    }

    [Fact]
    public void DataRedactor_RedactWithClassifications_AuditOnly_ContentPassesThrough()
    {
        var cls = new DataClassification
        {
            Name = "Name",
            Patterns = [@"Mr\. \w+ \w+"],
            Action = ClassificationAction.AuditOnly,
        };
        var redactor = new DataRedactor([], [cls]);
        var result = redactor.RedactWithClassifications("Hello Mr. John Smith");
        Assert.Contains("Mr. John Smith", result.Content); // content not redacted
        Assert.True(result.HasMatches);
        Assert.False(result.ShouldDeny);
    }

    [Fact]
    public void DataRedactor_RedactWithClassifications_NoMatch_EmptyMatches()
    {
        var cls = new DataClassification
        {
            Name = "SSN",
            Patterns = [@"\b\d{3}-\d{2}-\d{4}\b"],
            Action = ClassificationAction.Redact,
        };
        var redactor = new DataRedactor([], [cls]);
        var result = redactor.RedactWithClassifications("Hello world, no sensitive data here.");
        Assert.False(result.HasMatches);
        Assert.Equal("Hello world, no sensitive data here.", result.Content);
    }

    [Fact]
    public void DataRedactor_FlatAndClassification_BothApply()
    {
        var cls = new DataClassification
        {
            Name = "APIKey",
            Patterns = [@"sk-[A-Za-z0-9]{10,}"],
            Action = ClassificationAction.RedactAndAudit,
        };
        var redactor = new DataRedactor([@"\bSECRET\b"], [cls]);
        var result = redactor.Redact("SECRET and sk-abc1234567890");
        Assert.DoesNotContain("SECRET", result);
        Assert.DoesNotContain("sk-abc", result);
    }

    // ── DataPolicy merge ──────────────────────────────────────────────────

    [Fact]
    public void PolicyResolver_MergesClassifications_UnionByName()
    {
        var doc1 = new PolicyDocument
        {
            Metadata = new PolicyMetadata { Scope = PolicyScope.Global, Priority = 1 },
            Spec = new PolicySpec
            {
                Data = new DataPolicy
                {
                    Classifications = [new DataClassification { Name = "SSN", Action = ClassificationAction.Redact }]
                }
            }
        };
        var doc2 = new PolicyDocument
        {
            Metadata = new PolicyMetadata { Scope = PolicyScope.Organization, Priority = 2 },
            Spec = new PolicySpec
            {
                Data = new DataPolicy
                {
                    Classifications = [new DataClassification { Name = "PAN", Action = ClassificationAction.DenyAndAudit }]
                }
            }
        };

        var resolved = PolicyResolver.Resolve([doc1, doc2]);
        var names = resolved.Data?.Classifications.Select(c => c.Name).ToList() ?? [];
        Assert.Contains("SSN", names);
        Assert.Contains("PAN", names);
    }

    [Fact]
    public void PolicyResolver_DuplicateClassificationName_LastWins()
    {
        var doc1 = new PolicyDocument
        {
            Metadata = new PolicyMetadata { Scope = PolicyScope.Global, Priority = 1 },
            Spec = new PolicySpec
            {
                Data = new DataPolicy
                {
                    Classifications = [new DataClassification { Name = "SSN", Action = ClassificationAction.AuditOnly }]
                }
            }
        };
        var doc2 = new PolicyDocument
        {
            Metadata = new PolicyMetadata { Scope = PolicyScope.Organization, Priority = 5 },
            Spec = new PolicySpec
            {
                Data = new DataPolicy
                {
                    Classifications = [new DataClassification { Name = "SSN", Action = ClassificationAction.DenyAndAudit }]
                }
            }
        };

        var resolved = PolicyResolver.Resolve([doc1, doc2]);
        var ssn = resolved.Data?.Classifications.Single(c => c.Name == "SSN");
        Assert.Equal(ClassificationAction.DenyAndAudit, ssn?.Action);
    }

    // ── ComplianceControl check ────────────────────────────────────────────

    [Fact]
    public void Check_Soc2_FailsWithEmptyPolicy()
    {
        var controls = CompliancePresetLoader.Check("jdai/compliance/soc2", new PolicySpec());
        Assert.True(controls.Any(c => !c.Pass));
    }

    [Fact]
    public void Check_Soc2_PassesWithAuditAndRetention()
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
                    new DataClassification { Name = "CreditCard", Action = ClassificationAction.RedactAndAudit },
                ]
            }
        };

        var controls = CompliancePresetLoader.Check("jdai/compliance/soc2", spec);
        Assert.All(controls, c => Assert.True(c.Pass, $"{c.Id}: {c.Remediation}"));
    }

    [Fact]
    public void Check_UnknownProfile_ReturnsEmpty()
    {
        var controls = CompliancePresetLoader.Check("jdai/compliance/unknown", new PolicySpec());
        Assert.Empty(controls);
    }
}
