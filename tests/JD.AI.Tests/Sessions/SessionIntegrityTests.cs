using FluentAssertions;
using JD.AI.Core.Sessions;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Sessions;

/// <summary>
/// Integration-style tests for <see cref="SessionIntegrity.CheckAsync"/>.
///
/// These tests use real <see cref="SessionStore"/> (SQLite in-memory temp file) and
/// real <see cref="SessionExporter"/> (temp directory) because both are concrete, sealed
/// static classes with no abstraction boundary to mock against.
/// </summary>
public sealed class SessionIntegrityTests : IDisposable
{
    // Each test gets its own isolated SQLite db and basePath to avoid cross-test pollution.
    private readonly TempDirectoryFixture _fixture = new();
    private readonly string _dbPath;
    private readonly SessionStore _store;

    public SessionIntegrityTests()
    {
        _dbPath = Path.Combine(_fixture.DirectoryPath, $"jdai_integrity_{Guid.NewGuid():N}.db");
        _store = new SessionStore(_dbPath);
    }

    public void Dispose()
    {
        _store.Dispose();
        _fixture.Dispose();
    }

    // ── Helper factory methods ───────────────────────────────────────────

    private static SessionInfo MakeSession(
        string id = "sess123456789012",
        string projectPath = "/test/project",
        string projectHash = "abc12345") =>
        new()
        {
            Id = id,
            Name = $"Session {id}",
            ProjectPath = projectPath,
            ProjectHash = projectHash,
        };

    private async Task<SessionStore> CreateAndInitStoreAsync()
    {
        await _store.InitializeAsync();
        return _store;
    }

    // ── Zero sessions ────────────────────────────────────────────────────

    [Fact]
    public async Task CheckAsync_EmptyStore_ReturnsZeroSessionsCheckedAndNoIssues()
    {
        await _store.InitializeAsync();

        var result = await SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);

        result.SessionsChecked.Should().Be(0);
        result.MismatchesFound.Should().Be(0);
        result.Issues.Should().BeEmpty();
    }

    // ── Happy path: session exists in SQLite and matching JSON export ────

    [Fact]
    public async Task CheckAsync_SessionWithMatchingExport_ReportsNoIssues()
    {
        await _store.InitializeAsync();
        var session = MakeSession();
        await _store.CreateSessionAsync(session);
        await SessionExporter.ExportAsync(session, _fixture.DirectoryPath);

        var result = await SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);

        result.SessionsChecked.Should().Be(1);
        result.MismatchesFound.Should().Be(0);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_MultipleSessionsAllMatching_ReportsNoIssues()
    {
        await _store.InitializeAsync();
        var s1 = MakeSession("aaaa123456789012", "/proj/a", "aaaa0001");
        var s2 = MakeSession("bbbb123456789012", "/proj/b", "bbbb0002");
        var s3 = MakeSession("cccc123456789012", "/proj/c", "cccc0003");

        foreach (var s in new[] { s1, s2, s3 })
        {
            await _store.CreateSessionAsync(s);
            await SessionExporter.ExportAsync(s, _fixture.DirectoryPath);
        }

        var result = await SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);

        result.SessionsChecked.Should().Be(3);
        result.MismatchesFound.Should().Be(0);
        result.Issues.Should().BeEmpty();
    }

    // ── Missing JSON export ──────────────────────────────────────────────

    [Fact]
    public async Task CheckAsync_SessionMissingJsonExport_ReportsOneIssue()
    {
        await _store.InitializeAsync();
        var session = MakeSession();
        await _store.CreateSessionAsync(session);
        // Deliberately do NOT export to JSON.

        var result = await SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);

        result.SessionsChecked.Should().Be(1);
        result.MismatchesFound.Should().Be(1);
        result.Issues.Should().ContainSingle()
            .Which.Should().Contain("missing JSON export")
            .And.Contain(session.Id);
    }

    [Fact]
    public async Task CheckAsync_MixedExports_CountsOnlyMissingOnes()
    {
        await _store.InitializeAsync();
        var withExport = MakeSession("with123456789012", "/proj/a", "hashaaaa");
        var withoutExport = MakeSession("without123456012", "/proj/b", "hashbbbb");

        await _store.CreateSessionAsync(withExport);
        await _store.CreateSessionAsync(withoutExport);
        await SessionExporter.ExportAsync(withExport, _fixture.DirectoryPath);
        // withoutExport has no JSON

        var result = await SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);

        result.SessionsChecked.Should().Be(2);
        result.MismatchesFound.Should().Be(1);
        result.Issues.Should().ContainSingle()
            .Which.Should().Contain(withoutExport.Id);
    }

    // ── Corrupt / unreadable JSON export ────────────────────────────────
    //
    // NOTE: SessionExporter.ImportAsync does not catch JsonException - it propagates
    // through SessionIntegrity.CheckAsync when the JSON file is structurally invalid.
    // The "JSON file corrupt/unreadable" branch in CheckAsync is only reachable when
    // JsonSerializer.Deserialize returns null (valid JSON that deserialises to null).
    // For truly corrupt content, CheckAsync surfaces the underlying JsonException.

    [Fact]
    public async Task CheckAsync_CorruptJsonFile_ThrowsJsonException()
    {
        await _store.InitializeAsync();
        var session = MakeSession();
        await _store.CreateSessionAsync(session);

        // Write syntactically invalid JSON where the session file would be.
        var sessionDir = Path.Combine(
            _fixture.DirectoryPath, "projects", session.ProjectHash, "sessions");
        Directory.CreateDirectory(sessionDir);
        var jsonPath = Path.Combine(sessionDir, $"{session.Id}.json");
        await File.WriteAllTextAsync(jsonPath, "this is not valid json {{{{");

        // SessionExporter.ImportAsync lets JsonException propagate; CheckAsync does not catch it.
        var act = () => SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);
        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    [Fact]
    public async Task CheckAsync_EmptyJsonFile_ThrowsJsonException()
    {
        await _store.InitializeAsync();
        var session = MakeSession();
        await _store.CreateSessionAsync(session);

        var sessionDir = Path.Combine(
            _fixture.DirectoryPath, "projects", session.ProjectHash, "sessions");
        Directory.CreateDirectory(sessionDir);
        var jsonPath = Path.Combine(sessionDir, $"{session.Id}.json");
        await File.WriteAllTextAsync(jsonPath, string.Empty);

        // An empty file also causes JsonSerializer.Deserialize to throw.
        var act = () => SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);
        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    // ── Turn count mismatch ──────────────────────────────────────────────

    [Fact]
    public async Task CheckAsync_TurnCountMismatch_SqliteHasMoreTurns_ReportsIssue()
    {
        await _store.InitializeAsync();
        var session = MakeSession();
        await _store.CreateSessionAsync(session);

        // Export BEFORE adding turns (JSON will have 0 turns).
        await SessionExporter.ExportAsync(session, _fixture.DirectoryPath);

        // Now add a turn to SQLite only.
        await _store.SaveTurnAsync(new TurnRecord
        {
            Id = "turn123456789012",
            SessionId = session.Id,
            TurnIndex = 0,
            Role = "user",
            Content = "Hello from SQLite only",
        });

        var result = await SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);

        result.SessionsChecked.Should().Be(1);
        result.MismatchesFound.Should().Be(1);
        result.Issues.Should().ContainSingle()
            .Which.Should().Contain("turn count mismatch")
            .And.Contain(session.Id)
            .And.Contain("SQLite=1")
            .And.Contain("JSON=0");
    }

    [Fact]
    public async Task CheckAsync_TurnCountMismatch_JsonHasMoreTurns_ReportsIssue()
    {
        await _store.InitializeAsync();
        var session = MakeSession();
        await _store.CreateSessionAsync(session);

        // Build a session with turns, export it, but only store an empty session in SQLite.
        var enriched = MakeSession(session.Id, session.ProjectPath, session.ProjectHash);
        enriched.Turns.Add(new TurnRecord
        {
            Id = "turn123456789012",
            SessionId = session.Id,
            TurnIndex = 0,
            Role = "user",
            Content = "Turn in JSON only",
        });
        // Export the enriched copy (has 1 turn) but SQLite has 0 turns.
        await SessionExporter.ExportAsync(enriched, _fixture.DirectoryPath);

        var result = await SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);

        result.SessionsChecked.Should().Be(1);
        result.MismatchesFound.Should().Be(1);
        result.Issues.Should().ContainSingle()
            .Which.Should().Contain("turn count mismatch")
            .And.Contain("SQLite=0")
            .And.Contain("JSON=1");
    }

    [Fact]
    public async Task CheckAsync_TurnCountMatches_ReportsNoIssues()
    {
        await _store.InitializeAsync();
        var session = MakeSession();
        await _store.CreateSessionAsync(session);

        // Add two turns to SQLite.
        for (var i = 0; i < 2; i++)
        {
            await _store.SaveTurnAsync(new TurnRecord
            {
                Id = $"turn{i}23456789012345"[..16],
                SessionId = session.Id,
                TurnIndex = i,
                Role = i % 2 == 0 ? "user" : "assistant",
                Content = $"Turn {i}",
            });
        }

        // Retrieve the fully-loaded session from SQLite and export it (2 turns).
        var full = await _store.GetSessionAsync(session.Id);
        full.Should().NotBeNull();
        await SessionExporter.ExportAsync(full!, _fixture.DirectoryPath);

        var result = await SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);

        result.SessionsChecked.Should().Be(1);
        result.MismatchesFound.Should().Be(0);
        result.Issues.Should().BeEmpty();
    }

    // ── Auto-repair from SQLite ──────────────────────────────────────────

    [Fact]
    public async Task CheckAsync_AutoRepair_MissingExport_CreatesJsonFile()
    {
        await _store.InitializeAsync();
        var session = MakeSession();
        await _store.CreateSessionAsync(session);
        // No JSON export yet.

        var result = await SessionIntegrity.CheckAsync(
            _store,
            _fixture.DirectoryPath,
            autoRepairFromSqlite: true);

        // Issue is still recorded (repair does not retroactively suppress the report).
        result.MismatchesFound.Should().Be(1);
        result.Issues.Should().ContainSingle()
            .Which.Should().Contain("missing JSON export");

        // But the file should now exist after repair.
        var files = SessionExporter.ListExportedFiles(session.ProjectHash, _fixture.DirectoryPath).ToList();
        files.Should().ContainSingle()
            .Which.Should().EndWith($"{session.Id}.json");
    }

    [Fact]
    public async Task CheckAsync_AutoRepair_CorruptExport_StillThrowsJsonException()
    {
        // The auto-repair path for corrupt JSON is not reachable in the current implementation:
        // SessionExporter.ImportAsync throws JsonException before CheckAsync can inspect the
        // result, so the repair branch inside CheckAsync cannot execute. The exception propagates
        // regardless of autoRepairFromSqlite.
        await _store.InitializeAsync();
        var session = MakeSession();
        await _store.CreateSessionAsync(session);

        var sessionDir = Path.Combine(
            _fixture.DirectoryPath, "projects", session.ProjectHash, "sessions");
        Directory.CreateDirectory(sessionDir);
        var jsonPath = Path.Combine(sessionDir, $"{session.Id}.json");
        await File.WriteAllTextAsync(jsonPath, "{bad json");

        var act = () => SessionIntegrity.CheckAsync(
            _store,
            _fixture.DirectoryPath,
            autoRepairFromSqlite: true);

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    [Fact]
    public async Task CheckAsync_AutoRepair_TurnMismatch_ReExportsFromSqlite()
    {
        await _store.InitializeAsync();
        var session = MakeSession();
        await _store.CreateSessionAsync(session);

        // Export with 0 turns.
        await SessionExporter.ExportAsync(session, _fixture.DirectoryPath);

        // Add a turn to SQLite.
        await _store.SaveTurnAsync(new TurnRecord
        {
            Id = "turn123456789012",
            SessionId = session.Id,
            TurnIndex = 0,
            Role = "user",
            Content = "Repaired turn",
        });

        await SessionIntegrity.CheckAsync(
            _store,
            _fixture.DirectoryPath,
            autoRepairFromSqlite: true);

        // After repair, JSON should have 1 turn.
        var sessionDir = Path.Combine(
            _fixture.DirectoryPath, "projects", session.ProjectHash, "sessions");
        var jsonPath = Path.Combine(sessionDir, $"{session.Id}.json");
        var imported = await SessionExporter.ImportAsync(jsonPath);
        imported.Should().NotBeNull();
        imported!.Turns.Should().HaveCount(1);
        imported.Turns[0].Content.Should().Be("Repaired turn");
    }

    [Fact]
    public async Task CheckAsync_AutoRepairFalse_MissingExport_DoesNotCreateFile()
    {
        await _store.InitializeAsync();
        var session = MakeSession();
        await _store.CreateSessionAsync(session);

        await SessionIntegrity.CheckAsync(
            _store,
            _fixture.DirectoryPath,
            autoRepairFromSqlite: false);

        var files = SessionExporter.ListExportedFiles(session.ProjectHash, _fixture.DirectoryPath);
        files.Should().BeEmpty();
    }

    // ── Return value shape ───────────────────────────────────────────────

    [Fact]
    public async Task CheckAsync_ReturnsSessionsChecked_EqualToListCount()
    {
        await _store.InitializeAsync();

        for (var i = 0; i < 5; i++)
        {
            var s = MakeSession($"sess{i:D12}", "/proj", $"hash{i:D4}");
            await _store.CreateSessionAsync(s);
            await SessionExporter.ExportAsync(s, _fixture.DirectoryPath);
        }

        var result = await SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);

        result.SessionsChecked.Should().Be(5);
    }

    [Fact]
    public async Task CheckAsync_MismatchesFound_EqualsIssueCount()
    {
        await _store.InitializeAsync();

        // Two sessions, both missing JSON.
        await _store.CreateSessionAsync(MakeSession("aaaa123456789012", "/p/a", "hasha001"));
        await _store.CreateSessionAsync(MakeSession("bbbb123456789012", "/p/b", "hashb002"));

        var result = await SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);

        result.MismatchesFound.Should().Be(result.Issues.Count);
        result.MismatchesFound.Should().Be(2);
    }

    [Fact]
    public async Task CheckAsync_Issues_IsReadOnlyCollection()
    {
        await _store.InitializeAsync();

        var result = await SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);

        result.Issues.Should().BeAssignableTo<IReadOnlyList<string>>();
    }

    // ── Null / default basePath falls through (no crash) ─────────────────

    [Fact]
    public async Task CheckAsync_NullBasePath_DoesNotThrow()
    {
        // With null basePath SessionExporter uses Config.DataDirectories.Root.
        // We don't care about results, only that it doesn't blow up on an empty store.
        await _store.InitializeAsync();

        var act = () => SessionIntegrity.CheckAsync(_store, basePath: null);

        await act.Should().NotThrowAsync();
    }

    // ── Multiple issues accumulate correctly ─────────────────────────────

    [Fact]
    public async Task CheckAsync_TwoDistinctProblems_BothReported()
    {
        await _store.InitializeAsync();

        // Session 1: missing export (no JSON at all)
        var s1 = MakeSession("miss123456789012", "/p/a", "hashmiss");
        await _store.CreateSessionAsync(s1);

        // Session 2: turn count mismatch
        var s2 = MakeSession("mism123456789012", "/p/c", "hashmism");
        await _store.CreateSessionAsync(s2);
        await SessionExporter.ExportAsync(s2, _fixture.DirectoryPath); // JSON: 0 turns
        await _store.SaveTurnAsync(new TurnRecord
        {
            Id = "turn123456789012",
            SessionId = s2.Id,
            TurnIndex = 0,
            Role = "user",
            Content = "Extra turn",
        });

        // Session 3: clean (no issues)
        var s3 = MakeSession("good123456789012", "/p/d", "hashgood");
        await _store.CreateSessionAsync(s3);
        await SessionExporter.ExportAsync(s3, _fixture.DirectoryPath);

        var result = await SessionIntegrity.CheckAsync(_store, _fixture.DirectoryPath);

        result.SessionsChecked.Should().Be(3);
        result.MismatchesFound.Should().Be(2);
        result.Issues.Should().HaveCount(2);
        result.Issues.Should().Contain(i => i.Contains(s1.Id) && i.Contains("missing JSON export"));
        result.Issues.Should().Contain(i => i.Contains(s2.Id) && i.Contains("turn count mismatch"));
    }
}
