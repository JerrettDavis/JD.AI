using System.Text.Json;
using FluentAssertions;
using JD.AI.Core.Governance.Audit;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Governance.Audit;

public sealed class FileAuditSinkTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly FileAuditSink _sink;

    public FileAuditSinkTests()
    {
        _sink = new FileAuditSink(_fixture.DirectoryPath);
    }

    public void Dispose()
    {
        _sink.Dispose();
        _fixture.Dispose();
    }

    private static AuditEvent MakeEvent(string action = "test") => new()
    {
        Action = action,
        Severity = AuditSeverity.Info,
    };

    [Fact]
    public void Name_ReturnsFile()
    {
        _sink.Name.Should().Be("file");
    }

    [Fact]
    public async Task WriteAsync_CreatesFileWithCorrectName()
    {
        var evt = MakeEvent();

        await _sink.WriteAsync(evt);

        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var expectedFile = Path.Combine(_fixture.DirectoryPath, $"audit-{today}.jsonl");
        File.Exists(expectedFile).Should().BeTrue();
    }

    [Fact]
    public async Task WriteAsync_WritesValidJsonLine()
    {
        var evt = new AuditEvent
        {
            Action = "test-action",
            UserId = "user-42",
            Severity = AuditSeverity.Warning,
        };

        await _sink.WriteAsync(evt);

        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var filePath = Path.Combine(_fixture.DirectoryPath, $"audit-{today}.jsonl");
        var lines = await File.ReadAllLinesAsync(filePath);

        lines.Should().HaveCount(1);

        // Verify it's valid JSON
        var parsed = JsonDocument.Parse(lines[0]);
        parsed.RootElement.GetProperty("action").GetString().Should().Be("test-action");
        parsed.RootElement.GetProperty("userId").GetString().Should().Be("user-42");
        parsed.RootElement.GetProperty("severity").GetString().Should().Be("Warning");
    }

    [Fact]
    public async Task WriteAsync_MultipleEvents_AppendsLines()
    {
        await _sink.WriteAsync(MakeEvent("event-1"));
        await _sink.WriteAsync(MakeEvent("event-2"));
        await _sink.WriteAsync(MakeEvent("event-3"));

        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var filePath = Path.Combine(_fixture.DirectoryPath, $"audit-{today}.jsonl");
        var lines = (await File.ReadAllLinesAsync(filePath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        lines.Should().HaveCount(3);
    }

    [Fact]
    public async Task WriteAsync_DailyRotation_CreatesNewFileForNewDate()
    {
        // Write to two different "days" by using different timestamps
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
        var today = DateTimeOffset.UtcNow;

        var evt1 = new AuditEvent { Action = "yesterday", Timestamp = yesterday };
        var evt2 = new AuditEvent { Action = "today", Timestamp = today };

        await _sink.WriteAsync(evt1);
        await _sink.WriteAsync(evt2);

        var yesterdayFile = Path.Combine(_fixture.DirectoryPath, $"audit-{yesterday:yyyy-MM-dd}.jsonl");
        var todayFile = Path.Combine(_fixture.DirectoryPath, $"audit-{today:yyyy-MM-dd}.jsonl");

        File.Exists(yesterdayFile).Should().BeTrue();
        File.Exists(todayFile).Should().BeTrue();
    }

    [Fact]
    public async Task FlushAsync_CompletesSuccessfully()
    {
        var act = async () => await _sink.FlushAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WriteAsync_ConcurrentWrites_AllWritten()
    {
        var tasks = Enumerable.Range(1, 20)
            .Select(i => _sink.WriteAsync(MakeEvent($"concurrent-{i}")))
            .ToList();

        await Task.WhenAll(tasks);

        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var filePath = Path.Combine(_fixture.DirectoryPath, $"audit-{today}.jsonl");
        var lines = (await File.ReadAllLinesAsync(filePath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        lines.Should().HaveCount(20);
    }

    [Fact]
    public async Task WriteAsync_CreatesDirectoryIfNotExists()
    {
        var subDir = Path.Combine(_fixture.DirectoryPath, "sub", "dir");
        using var sink = new FileAuditSink(subDir);

        await sink.WriteAsync(MakeEvent());

        Directory.Exists(subDir).Should().BeTrue();
    }
}
