using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class PolicyDocumentTests
{
    // ── PolicyDocument ────────────────────────────────────────────────────

    [Fact]
    public void PolicyDocument_Defaults()
    {
        var doc = new PolicyDocument();
        doc.ApiVersion.Should().Be("jdai/v1");
        doc.Kind.Should().Be("Policy");
        doc.Metadata.Should().NotBeNull();
        doc.Spec.Should().NotBeNull();
    }

    [Fact]
    public void PolicyDocument_CustomValues()
    {
        var doc = new PolicyDocument
        {
            ApiVersion = "jdai/v2",
            Kind = "CompliancePolicy",
            Metadata = new PolicyMetadata { Name = "test" },
            Spec = new PolicySpec { Extends = "jdai/compliance/soc2" },
        };
        doc.ApiVersion.Should().Be("jdai/v2");
        doc.Kind.Should().Be("CompliancePolicy");
        doc.Metadata.Name.Should().Be("test");
        doc.Spec.Extends.Should().Be("jdai/compliance/soc2");
    }

    // ── PolicySpec ────────────────────────────────────────────────────────

    [Fact]
    public void PolicySpec_AllNullByDefault()
    {
        var spec = new PolicySpec();
        spec.Extends.Should().BeNull();
        spec.Tools.Should().BeNull();
        spec.Providers.Should().BeNull();
        spec.Models.Should().BeNull();
        spec.Budget.Should().BeNull();
        spec.Data.Should().BeNull();
        spec.Sessions.Should().BeNull();
        spec.Audit.Should().BeNull();
        spec.Workflows.Should().BeNull();
        spec.CircuitBreaker.Should().BeNull();
        spec.Roles.Should().BeNull();
    }

    // ── ToolPolicy ────────────────────────────────────────────────────────

    [Fact]
    public void ToolPolicy_DefaultsEmpty()
    {
        var policy = new ToolPolicy();
        policy.Allowed.Should().BeEmpty();
        policy.Denied.Should().BeEmpty();
        policy.RequireApprovalFor.Should().BeEmpty();
    }

    // ── ProviderPolicy ────────────────────────────────────────────────────

    [Fact]
    public void ProviderPolicy_DefaultsEmpty()
    {
        var policy = new ProviderPolicy();
        policy.Allowed.Should().BeEmpty();
        policy.Denied.Should().BeEmpty();
    }

    // ── ModelPolicy ───────────────────────────────────────────────────────

    [Fact]
    public void ModelPolicy_Defaults()
    {
        var policy = new ModelPolicy();
        policy.MaxContextWindow.Should().BeNull();
        policy.Denied.Should().BeEmpty();
    }

    // ── BudgetPolicy ──────────────────────────────────────────────────────

    [Fact]
    public void BudgetPolicy_Defaults()
    {
        var policy = new BudgetPolicy();
        policy.MaxDailyUsd.Should().BeNull();
        policy.MaxMonthlyUsd.Should().BeNull();
        policy.MaxSessionUsd.Should().BeNull();
        policy.AlertThresholdPercent.Should().Be(80);
    }

    // ── DataPolicy ────────────────────────────────────────────────────────

    [Fact]
    public void DataPolicy_DefaultsEmpty()
    {
        var policy = new DataPolicy();
        policy.NoExternalProviders.Should().BeEmpty();
        policy.RedactPatterns.Should().BeEmpty();
        policy.Classifications.Should().BeEmpty();
    }

    // ── SessionPolicy ─────────────────────────────────────────────────────

    [Fact]
    public void SessionPolicy_Defaults()
    {
        var policy = new SessionPolicy();
        policy.RetentionDays.Should().BeNull();
        policy.RequireProjectTag.Should().BeFalse();
    }

    // ── AuditPolicy ───────────────────────────────────────────────────────

    [Fact]
    public void AuditPolicy_Defaults()
    {
        var policy = new AuditPolicy();
        policy.Enabled.Should().BeFalse();
        policy.Sink.Should().Be("file");
        policy.Endpoint.Should().BeNull();
        policy.Index.Should().BeNull();
        policy.Token.Should().BeNull();
        policy.Url.Should().BeNull();
        policy.ConnectionString.Should().BeNull();
        policy.Server.Should().BeNull();
    }

    // ── WorkflowPolicy ───────────────────────────────────────────────────

    [Fact]
    public void WorkflowPolicy_Defaults()
    {
        var policy = new WorkflowPolicy();
        policy.PublishAllowed.Should().BeEmpty();
        policy.PublishDenied.Should().BeEmpty();
        policy.RequireApprovalGate.Should().BeFalse();
    }

    // ── RolePolicy / RoleDefinition ───────────────────────────────────────

    [Fact]
    public void RolePolicy_DefaultsEmpty()
    {
        var policy = new RolePolicy();
        policy.Definitions.Should().BeEmpty();
    }

    [Fact]
    public void RoleDefinition_Defaults()
    {
        var def = new RoleDefinition();
        def.Inherits.Should().BeEmpty();
        def.AllowTools.Should().BeEmpty();
        def.DenyTools.Should().BeEmpty();
        def.AllowProviders.Should().BeEmpty();
        def.DenyProviders.Should().BeEmpty();
        def.AllowModels.Should().BeEmpty();
        def.DenyModels.Should().BeEmpty();
    }
}
