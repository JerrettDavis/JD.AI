using JD.AI.Tui.Persistence;

namespace JD.AI.Tui.Tests;

public sealed class SessionExporterTests : IDisposable
{
    private readonly string _basePath;

    public SessionExporterTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"jdai_export_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_basePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath))
            Directory.Delete(_basePath, recursive: true);
    }

    [Fact]
    public async Task ExportAndImport_RoundTrips()
    {
        var session = new SessionInfo
        {
            Id = "exp1234567890123",
            Name = "Export Test",
            ProjectPath = "/tmp/export",
            ProjectHash = "exphash1",
            ModelId = "gpt-4",
            ProviderName = "openai",
            TotalTokens = 1234,
            MessageCount = 10,
        };

        session.Turns.Add(new TurnRecord
        {
            Id = "turn123456789012",
            SessionId = "exp1234567890123",
            TurnIndex = 0,
            Role = "user",
            Content = "Hello!",
        });

        session.Turns.Add(new TurnRecord
        {
            Id = "turn234567890123",
            SessionId = "exp1234567890123",
            TurnIndex = 1,
            Role = "assistant",
            Content = "Hi there!",
            ThinkingText = "The user said hello",
            ModelId = "gpt-4",
            TokensIn = 5,
            TokensOut = 10,
        });

        await SessionExporter.ExportAsync(session, _basePath);

        // Verify file exists
        var files = SessionExporter.ListExportedFiles("exphash1", _basePath).ToList();
        Assert.Single(files);

        // Import and verify
        var imported = await SessionExporter.ImportAsync(files[0]);
        Assert.NotNull(imported);
        Assert.Equal("exp1234567890123", imported.Id);
        Assert.Equal("Export Test", imported.Name);
        Assert.Equal(2, imported.Turns.Count);
        Assert.Equal("Hello!", imported.Turns[0].Content);
        Assert.Equal("Hi there!", imported.Turns[1].Content);
        Assert.Equal("The user said hello", imported.Turns[1].ThinkingText);
    }

    [Fact]
    public async Task ImportAsync_NonexistentFile_ReturnsNull()
    {
        var result = await SessionExporter.ImportAsync("/nonexistent/path.json");
        Assert.Null(result);
    }

    [Fact]
    public void ListExportedFiles_EmptyDir_ReturnsEmpty()
    {
        var files = SessionExporter.ListExportedFiles("nohash", _basePath);
        Assert.Empty(files);
    }
}
