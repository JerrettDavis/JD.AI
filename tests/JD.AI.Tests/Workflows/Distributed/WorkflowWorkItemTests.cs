using System.Text.Json;
using FluentAssertions;
using JD.AI.Workflows.Distributed;

namespace JD.AI.Tests.Workflows.Distributed;

/// <summary>
/// Tests for the WorkflowWorkItem record: defaults, with-expression semantics,
/// JSON serialization/deserialization, and property contracts.
/// </summary>
public sealed class WorkflowWorkItemTests
{
    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void Id_Default_IsNonEmptyGuid()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        item.Id.Should().NotBeNullOrEmpty();
        item.Id.Should().HaveLength(32); // Guid.NewGuid().ToString("N") is 32 hex chars
    }

    [Fact]
    public void CorrelationId_Default_IsNonEmptyGuid()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        item.CorrelationId.Should().NotBeNullOrEmpty();
        item.CorrelationId.Should().HaveLength(32);
    }

    [Fact]
    public void EnqueuedAt_Default_IsApproximatelyNow()
    {
        var before = DateTimeOffset.UtcNow;
        var item = new WorkflowWorkItem { WorkflowName = "wf" };
        var after = DateTimeOffset.UtcNow;

        item.EnqueuedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void MaxDeliveryCount_Default_IsFive()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        item.MaxDeliveryCount.Should().Be(5);
    }

    [Fact]
    public void Priority_Default_IsZero()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        item.Priority.Should().Be(0);
    }

    [Fact]
    public void DeliveryCount_Default_IsZero()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        item.DeliveryCount.Should().Be(0);
    }

    [Fact]
    public void WorkflowVersion_Default_IsNull()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        item.WorkflowVersion.Should().BeNull();
    }

    [Fact]
    public void InitialContext_Default_IsEmpty()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        item.InitialContext.Should().NotBeNull().And.BeEmpty();
    }

    // ── Unique IDs across instances ────────────────────────────────────────────

    [Fact]
    public void TwoInstances_HaveDifferentIds()
    {
        var a = new WorkflowWorkItem { WorkflowName = "wf" };
        var b = new WorkflowWorkItem { WorkflowName = "wf" };

        a.Id.Should().NotBe(b.Id);
    }

    [Fact]
    public void TwoInstances_HaveDifferentCorrelationIds()
    {
        var a = new WorkflowWorkItem { WorkflowName = "wf" };
        var b = new WorkflowWorkItem { WorkflowName = "wf" };

        a.CorrelationId.Should().NotBe(b.CorrelationId);
    }

    // ── With-expression (record copy semantics) ───────────────────────────────

    [Fact]
    public void WithDeliveryCount_PreservesAllOtherFields()
    {
        var original = new WorkflowWorkItem
        {
            WorkflowName = "my-wf",
            WorkflowVersion = "1.0.0",
            Priority = 3,
        };

        var updated = original with { DeliveryCount = 2 };

        updated.Id.Should().Be(original.Id);
        updated.CorrelationId.Should().Be(original.CorrelationId);
        updated.WorkflowName.Should().Be("my-wf");
        updated.WorkflowVersion.Should().Be("1.0.0");
        updated.Priority.Should().Be(3);
        updated.DeliveryCount.Should().Be(2);
    }

    [Fact]
    public void WithExpression_OriginalIsUnchanged()
    {
        var original = new WorkflowWorkItem { WorkflowName = "wf", DeliveryCount = 0 };
        _ = original with { DeliveryCount = 5 };

        original.DeliveryCount.Should().Be(0);
    }

    // ── Custom initialization ─────────────────────────────────────────────────

    [Fact]
    public void CustomId_IsUsedWhenProvided()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf", Id = "custom-id-123" };

        item.Id.Should().Be("custom-id-123");
    }

    [Fact]
    public void CustomCorrelationId_IsUsedWhenProvided()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf", CorrelationId = "trace-abc" };

        item.CorrelationId.Should().Be("trace-abc");
    }

    [Fact]
    public void CustomMaxDeliveryCount_IsRespected()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf", MaxDeliveryCount = 3 };

        item.MaxDeliveryCount.Should().Be(3);
    }

    [Fact]
    public void InitialContext_CanBePopulated()
    {
        var context = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["userId"] = "u-123",
            ["env"] = "production",
        };

        var item = new WorkflowWorkItem { WorkflowName = "wf", InitialContext = context };

        item.InitialContext.Should().ContainKey("userId").WhoseValue.Should().Be("u-123");
        item.InitialContext.Should().ContainKey("env").WhoseValue.Should().Be("production");
    }

    // ── JSON serialization ────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Serialize_ProducesExpectedPropertyNames()
    {
        var item = new WorkflowWorkItem
        {
            Id = "abc",
            WorkflowName = "deploy",
            CorrelationId = "cid-1",
        };

        var json = JsonSerializer.Serialize(item, JsonOptions);

        json.Should().Contain("\"id\"");
        json.Should().Contain("\"workflowName\"");
        json.Should().Contain("\"correlationId\"");
        json.Should().Contain("\"enqueuedAt\"");
        json.Should().Contain("\"priority\"");
        json.Should().Contain("\"deliveryCount\"");
        json.Should().Contain("\"maxDeliveryCount\"");
    }

    [Fact]
    public void Deserialize_RoundTripsAllProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var original = new WorkflowWorkItem
        {
            Id = "id-xyz",
            WorkflowName = "integration-test",
            WorkflowVersion = "2.1.0",
            CorrelationId = "corr-abc",
            Priority = 7,
            DeliveryCount = 2,
            MaxDeliveryCount = 10,
            EnqueuedAt = now,
            InitialContext = new Dictionary<string, string>(StringComparer.Ordinal) { ["k"] = "v" },
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<WorkflowWorkItem>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be("id-xyz");
        deserialized.WorkflowName.Should().Be("integration-test");
        deserialized.WorkflowVersion.Should().Be("2.1.0");
        deserialized.CorrelationId.Should().Be("corr-abc");
        deserialized.Priority.Should().Be(7);
        deserialized.DeliveryCount.Should().Be(2);
        deserialized.MaxDeliveryCount.Should().Be(10);
        deserialized.InitialContext.Should().ContainKey("k").WhoseValue.Should().Be("v");
    }

    [Fact]
    public void Deserialize_NullWorkflowVersion_RemainsNull()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };
        var json = JsonSerializer.Serialize(item, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<WorkflowWorkItem>(json, JsonOptions);

        deserialized!.WorkflowVersion.Should().BeNull();
    }

    [Fact]
    public void Serialize_WorkflowVersion_IncludesNullValue()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf", WorkflowVersion = "3.0.0" };
        var json = JsonSerializer.Serialize(item, JsonOptions);

        json.Should().Contain("\"workflowVersion\"");
        json.Should().Contain("3.0.0");
    }

    // ── Record equality ───────────────────────────────────────────────────────

    [Fact]
    public void TwoItemsWithSameValues_AreEqual()
    {
        var now = DateTimeOffset.UtcNow;
        var a = new WorkflowWorkItem { WorkflowName = "wf", Id = "same", CorrelationId = "cid", EnqueuedAt = now };
        var b = new WorkflowWorkItem { WorkflowName = "wf", Id = "same", CorrelationId = "cid", EnqueuedAt = now };

        a.Should().BeEquivalentTo(b);
    }

    [Fact]
    public void TwoItemsWithDifferentIds_AreNotEqual()
    {
        var a = new WorkflowWorkItem { WorkflowName = "wf", Id = "id-a" };
        var b = new WorkflowWorkItem { WorkflowName = "wf", Id = "id-b" };

        a.Should().NotBe(b);
    }
}
