using System.Text.Json;
using FluentAssertions;
using JD.AI.Workflows.Distributed;

namespace JD.AI.Tests.Workflows.Distributed;

/// <summary>
/// Tests for JSON serialization/deserialization of WorkflowWorkItem
/// as it would be serialized by both the Redis and Service Bus dispatchers.
/// Covers edge cases, empty context, and round-trip fidelity.
/// </summary>
public sealed class WorkflowSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptionsWeb = new(JsonSerializerDefaults.Web);

    // ── Round-trip: web defaults ──────────────────────────────────────────────

    [Fact]
    public void RoundTrip_MinimalItem_Web()
    {
        var item = new WorkflowWorkItem { WorkflowName = "minimal-wf" };

        var json = JsonSerializer.Serialize(item, JsonOptionsWeb);
        var restored = JsonSerializer.Deserialize<WorkflowWorkItem>(json, JsonOptionsWeb);

        restored.Should().NotBeNull();
        restored!.WorkflowName.Should().Be("minimal-wf");
        restored.Id.Should().Be(item.Id);
        restored.CorrelationId.Should().Be(item.CorrelationId);
        restored.MaxDeliveryCount.Should().Be(5);
        restored.Priority.Should().Be(0);
        restored.DeliveryCount.Should().Be(0);
    }

    [Fact]
    public void RoundTrip_FullyPopulatedItem_Web()
    {
        var now = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var item = new WorkflowWorkItem
        {
            Id = "fixed-id",
            WorkflowName = "full-workflow",
            WorkflowVersion = "3.2.1",
            CorrelationId = "trace-id-001",
            Priority = 9,
            DeliveryCount = 3,
            MaxDeliveryCount = 7,
            EnqueuedAt = now,
            InitialContext = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["env"] = "prod",
                ["userId"] = "u-9876",
                ["requestId"] = "req-001",
            },
        };

        var json = JsonSerializer.Serialize(item, JsonOptionsWeb);
        var restored = JsonSerializer.Deserialize<WorkflowWorkItem>(json, JsonOptionsWeb);

        restored.Should().NotBeNull();
        restored!.Id.Should().Be("fixed-id");
        restored.WorkflowName.Should().Be("full-workflow");
        restored.WorkflowVersion.Should().Be("3.2.1");
        restored.CorrelationId.Should().Be("trace-id-001");
        restored.Priority.Should().Be(9);
        restored.DeliveryCount.Should().Be(3);
        restored.MaxDeliveryCount.Should().Be(7);
        restored.EnqueuedAt.Should().Be(now);
        restored.InitialContext.Should().ContainKey("env").WhoseValue.Should().Be("prod");
        restored.InitialContext.Should().ContainKey("userId").WhoseValue.Should().Be("u-9876");
        restored.InitialContext.Should().ContainKey("requestId").WhoseValue.Should().Be("req-001");
    }

    // ── Property name casing (web serializer = camelCase) ─────────────────────

    [Fact]
    public void Serialize_Web_UsesLowercasePropertyNames()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        var json = JsonSerializer.Serialize(item, JsonOptionsWeb);

        json.Should().Contain("\"workflowName\"");
        json.Should().Contain("\"id\"");
        json.Should().Contain("\"correlationId\"");
        json.Should().Contain("\"enqueuedAt\"");
        json.Should().Contain("\"priority\"");
        json.Should().Contain("\"deliveryCount\"");
        json.Should().Contain("\"maxDeliveryCount\"");
        json.Should().Contain("\"initialContext\"");
    }

    [Fact]
    public void Serialize_Web_UsesJsonPropertyNameAttributes()
    {
        // The JsonPropertyName attribute overrides the source member name.
        // Since all attributes use the same camelCase names, the web serializer and
        // attribute-named form should be consistent.
        var item = new WorkflowWorkItem { WorkflowName = "wf" };
        var json = JsonSerializer.Serialize(item, JsonOptionsWeb);

        // 'workflowVersion' should appear even if null (serialized as null)
        json.Should().Contain("workflowVersion");
    }

    // ── DeliveryCount increment via with-expression ───────────────────────────

    [Fact]
    public void DeliveryCount_IncrementedAfterDeserialization_IsCorrect()
    {
        var original = new WorkflowWorkItem { WorkflowName = "wf", DeliveryCount = 2 };

        var json = JsonSerializer.Serialize(original, JsonOptionsWeb);
        var deserialized = JsonSerializer.Deserialize<WorkflowWorkItem>(json, JsonOptionsWeb)!;

        var incremented = deserialized with { DeliveryCount = deserialized.DeliveryCount + 1 };

        incremented.DeliveryCount.Should().Be(3);
        incremented.WorkflowName.Should().Be("wf");
    }

    // ── MaxDeliveryCount threshold ────────────────────────────────────────────

    [Theory]
    [InlineData(0, 5, false)]  // 0 deliveries, max 5 → not exceeded
    [InlineData(5, 5, false)]  // 5 deliveries, max 5 → exactly at limit (not > 5)
    [InlineData(6, 5, true)]   // 6 deliveries, max 5 → exceeded
    [InlineData(1, 1, false)]  // 1 delivery, max 1 → at limit
    [InlineData(2, 1, true)]   // 2 deliveries, max 1 → exceeded
    public void DeliveryCount_ExceedsMaxDelivery_ReturnsExpected(
        int deliveryCount, int maxDeliveryCount, bool shouldExceed)
    {
        var item = new WorkflowWorkItem
        {
            WorkflowName = "wf",
            DeliveryCount = deliveryCount,
            MaxDeliveryCount = maxDeliveryCount,
        };

        var exceeds = item.DeliveryCount > item.MaxDeliveryCount;

        exceeds.Should().Be(shouldExceed);
    }

    // ── Empty initial context ─────────────────────────────────────────────────

    [Fact]
    public void Serialize_EmptyInitialContext_RoundTrips()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };
        // InitialContext defaults to empty dictionary

        var json = JsonSerializer.Serialize(item, JsonOptionsWeb);
        var restored = JsonSerializer.Deserialize<WorkflowWorkItem>(json, JsonOptionsWeb)!;

        restored.InitialContext.Should().NotBeNull().And.BeEmpty();
    }

    // ── Null WorkflowVersion ──────────────────────────────────────────────────

    [Fact]
    public void Serialize_NullWorkflowVersion_DeserializesAsNull()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf", WorkflowVersion = null };

        var json = JsonSerializer.Serialize(item, JsonOptionsWeb);
        var restored = JsonSerializer.Deserialize<WorkflowWorkItem>(json, JsonOptionsWeb)!;

        restored.WorkflowVersion.Should().BeNull();
    }

    [Fact]
    public void Serialize_NonNullWorkflowVersion_DeserializesCorrectly()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf", WorkflowVersion = "2.0.0-rc.1" };

        var json = JsonSerializer.Serialize(item, JsonOptionsWeb);
        var restored = JsonSerializer.Deserialize<WorkflowWorkItem>(json, JsonOptionsWeb)!;

        restored.WorkflowVersion.Should().Be("2.0.0-rc.1");
    }

    // ── Large context dictionary ──────────────────────────────────────────────

    [Fact]
    public void Serialize_LargeContext_RoundTrips()
    {
        var context = Enumerable.Range(0, 50)
            .ToDictionary(i => $"key-{i}", i => $"value-{i}", StringComparer.Ordinal);

        var item = new WorkflowWorkItem
        {
            WorkflowName = "large-context-wf",
            InitialContext = context,
        };

        var json = JsonSerializer.Serialize(item, JsonOptionsWeb);
        var restored = JsonSerializer.Deserialize<WorkflowWorkItem>(json, JsonOptionsWeb)!;

        restored.InitialContext.Should().HaveCount(50);
        restored.InitialContext["key-42"].Should().Be("value-42");
    }

    // ── JSON is a valid string ────────────────────────────────────────────────

    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        var json = JsonSerializer.Serialize(item, JsonOptionsWeb);

        // Parsing the resulting JSON should not throw
        Action act = () => JsonDocument.Parse(json);

        act.Should().NotThrow();
    }

    // ── Priority zero and positive ────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(int.MaxValue)]
    public void Priority_IsSerializedAndDeserializedCorrectly(int priority)
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf", Priority = priority };

        var json = JsonSerializer.Serialize(item, JsonOptionsWeb);
        var restored = JsonSerializer.Deserialize<WorkflowWorkItem>(json, JsonOptionsWeb)!;

        restored.Priority.Should().Be(priority);
    }
}
