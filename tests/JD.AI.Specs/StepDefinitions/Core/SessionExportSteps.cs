using FluentAssertions;
using JD.AI.Core.Sessions;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class SessionExportSteps
{
    private readonly ScenarioContext _context;

    public SessionExportSteps(ScenarioContext context) => _context = context;

    [Given(@"a temporary export directory")]
    public void GivenATemporaryExportDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-export-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        _context.Set(tempDir, "exportBasePath");
    }

    [Given(@"a session with ID ""(.*)"" for project ""(.*)"" with hash ""(.*)""")]
    public void GivenASessionWithIdForProject(string id, string projectPath, string hash)
    {
        var session = new SessionInfo
        {
            Id = id,
            ProjectPath = projectPath,
            ProjectHash = hash,
        };
        _context.Set(session, "currentExportSession");
    }

    [Given(@"the session has a user turn ""(.*)"" and assistant turn ""(.*)""")]
    public void GivenTheSessionHasTurns(string userContent, string assistantContent)
    {
        var session = _context.Get<SessionInfo>("currentExportSession");
        session.Turns.Add(new TurnRecord
        {
            SessionId = session.Id,
            TurnIndex = 0,
            Role = "user",
            Content = userContent,
        });
        session.Turns.Add(new TurnRecord
        {
            SessionId = session.Id,
            TurnIndex = 1,
            Role = "assistant",
            Content = assistantContent,
        });
        session.MessageCount = 2;
    }

    [When(@"the session is exported to JSON")]
    [Given(@"the session is exported to JSON")]
    public async Task WhenTheSessionIsExportedToJson()
    {
        var session = _context.Get<SessionInfo>("currentExportSession");
        var basePath = _context.Get<string>("exportBasePath");
        await SessionExporter.ExportAsync(session, basePath);
    }

    [When(@"exported files for project hash ""(.*)"" are listed")]
    public void WhenExportedFilesForProjectHashAreListed(string projectHash)
    {
        var basePath = _context.Get<string>("exportBasePath");
        var files = SessionExporter.ListExportedFiles(projectHash, basePath).ToList();
        _context.Set(files, "exportFileList");
    }

    [Then(@"a JSON file should exist at the expected export path")]
    public void ThenAJsonFileShouldExistAtTheExpectedExportPath()
    {
        var session = _context.Get<SessionInfo>("currentExportSession");
        var basePath = _context.Get<string>("exportBasePath");
        var expectedPath = Path.Combine(basePath, "projects", session.ProjectHash, "sessions", $"{session.Id}.json");
        File.Exists(expectedPath).Should().BeTrue($"Expected file at {expectedPath}");
    }

    [Then(@"the exported JSON should contain the session ID ""(.*)""")]
    public void ThenTheExportedJsonShouldContainTheSessionId(string sessionId)
    {
        var session = _context.Get<SessionInfo>("currentExportSession");
        var basePath = _context.Get<string>("exportBasePath");
        var path = Path.Combine(basePath, "projects", session.ProjectHash, "sessions", $"{session.Id}.json");
        var json = File.ReadAllText(path);
        json.Should().Contain(sessionId);
    }

    [Then(@"the exported JSON should contain ""(.*)""")]
    public void ThenTheExportedJsonShouldContain(string expected)
    {
        var session = _context.Get<SessionInfo>("currentExportSession");
        var basePath = _context.Get<string>("exportBasePath");
        var path = Path.Combine(basePath, "projects", session.ProjectHash, "sessions", $"{session.Id}.json");
        var json = File.ReadAllText(path);
        json.Should().Contain(expected);
    }

    [Then(@"the exported JSON should contain role ""(.*)""")]
    public void ThenTheExportedJsonShouldContainRole(string role)
    {
        var session = _context.Get<SessionInfo>("currentExportSession");
        var basePath = _context.Get<string>("exportBasePath");
        var path = Path.Combine(basePath, "projects", session.ProjectHash, "sessions", $"{session.Id}.json");
        var json = File.ReadAllText(path);
        json.Should().Contain(role);
    }

    [Then(@"the export file list should contain (\d+) entries")]
    public void ThenTheExportFileListShouldContainEntries(int count)
    {
        var files = _context.Get<List<string>>("exportFileList");
        files.Should().HaveCount(count);
    }

    [AfterScenario("@export")]
    public void Cleanup()
    {
        if (_context.TryGetValue<string>("exportBasePath", out var dir) && Directory.Exists(dir))
        {
            try { Directory.Delete(dir, true); } catch { /* best-effort */ }
        }
    }
}
