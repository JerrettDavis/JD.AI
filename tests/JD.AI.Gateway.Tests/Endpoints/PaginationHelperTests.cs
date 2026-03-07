using FluentAssertions;
using JD.AI.Gateway.Endpoints;

namespace JD.AI.Gateway.Tests.Endpoints;

public sealed class PaginationHelperTests
{
    // ── ClampLimit ──────────────────────────────────────────────────────────

    [Fact]
    public void ClampLimit_Null_ReturnsDefault() =>
        PaginationHelper.ClampLimit(null).Should().Be(PaginationHelper.DefaultLimit);

    [Fact]
    public void ClampLimit_Zero_ClampsToOne() =>
        PaginationHelper.ClampLimit(0).Should().Be(1);

    [Fact]
    public void ClampLimit_Negative_ClampsToOne() =>
        PaginationHelper.ClampLimit(-10).Should().Be(1);

    [Fact]
    public void ClampLimit_AboveMax_ClampsToMax() =>
        PaginationHelper.ClampLimit(500).Should().Be(PaginationHelper.MaxLimit);

    [Fact]
    public void ClampLimit_WithinRange_ReturnsAsIs() =>
        PaginationHelper.ClampLimit(25).Should().Be(25);

    [Fact]
    public void ClampLimit_ExactMax_ReturnsMax() =>
        PaginationHelper.ClampLimit(PaginationHelper.MaxLimit).Should().Be(PaginationHelper.MaxLimit);

    // ── EncodeCursor / DecodeCursor ─────────────────────────────────────────

    [Fact]
    public void EncodeCursor_Positive_ReturnsBase64()
    {
        var cursor = PaginationHelper.EncodeCursor(50);
        cursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void EncodeCursor_Zero_ReturnsNull() =>
        PaginationHelper.EncodeCursor(0).Should().BeNull();

    [Fact]
    public void EncodeCursor_Negative_ReturnsNull() =>
        PaginationHelper.EncodeCursor(-5).Should().BeNull();

    [Fact]
    public void DecodeCursor_Null_ReturnsZero() =>
        PaginationHelper.DecodeCursor(null).Should().Be(0);

    [Fact]
    public void DecodeCursor_Empty_ReturnsZero() =>
        PaginationHelper.DecodeCursor("").Should().Be(0);

    [Fact]
    public void DecodeCursor_InvalidBase64_ReturnsZero() =>
        PaginationHelper.DecodeCursor("not-valid-base64!!!").Should().Be(0);

    [Fact]
    public void EncodeDecode_Roundtrip()
    {
        var encoded = PaginationHelper.EncodeCursor(42);
        var decoded = PaginationHelper.DecodeCursor(encoded);
        decoded.Should().Be(42);
    }

    [Fact]
    public void DecodeCursor_NonNumericBase64_ReturnsZero()
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("abc"));
        PaginationHelper.DecodeCursor(encoded).Should().Be(0);
    }

    // ── Paginate ────────────────────────────────────────────────────────────

    [Fact]
    public void Paginate_EmptyCollection_ReturnsEmpty()
    {
        var result = PaginationHelper.Paginate<string>([], limit: 10, cursor: null);
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.HasMore.Should().BeFalse();
        result.Cursor.Should().BeNull();
    }

    [Fact]
    public void Paginate_FirstPage_HasMore()
    {
        var items = Enumerable.Range(1, 20).ToList();
        var result = PaginationHelper.Paginate(items, limit: 5, cursor: null);

        result.Items.Should().HaveCount(5);
        result.Items[0].Should().Be(1);
        result.TotalCount.Should().Be(20);
        result.HasMore.Should().BeTrue();
        result.Cursor.Should().NotBeNull();
    }

    [Fact]
    public void Paginate_SecondPage_WithCursor()
    {
        var items = Enumerable.Range(1, 20).ToList();
        var page1 = PaginationHelper.Paginate(items, limit: 5, cursor: null);
        var page2 = PaginationHelper.Paginate(items, limit: 5, cursor: page1.Cursor);

        page2.Items.Should().HaveCount(5);
        page2.Items[0].Should().Be(6);
        page2.HasMore.Should().BeTrue();
    }

    [Fact]
    public void Paginate_LastPage_NoMore()
    {
        var items = Enumerable.Range(1, 10).ToList();
        var result = PaginationHelper.Paginate(items, limit: 10, cursor: null);

        result.Items.Should().HaveCount(10);
        result.HasMore.Should().BeFalse();
        result.Cursor.Should().BeNull();
    }

    [Fact]
    public void Paginate_NullLimit_UsesDefault()
    {
        var items = Enumerable.Range(1, 100).ToList();
        var result = PaginationHelper.Paginate(items, limit: null, cursor: null);

        result.Items.Should().HaveCount(PaginationHelper.DefaultLimit);
    }

    // ── PaginatedResult record ──────────────────────────────────────────────

    [Fact]
    public void PaginatedResult_Properties()
    {
        var result = new PaginatedResult<int>([1, 2], 10, "cursor", true);
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(10);
        result.Cursor.Should().Be("cursor");
        result.HasMore.Should().BeTrue();
    }
}
