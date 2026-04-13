using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.Providers;
using JD.AI.Core.Sessions;
using JD.AI.Tests.Fixtures;
using Microsoft.SemanticKernel;
using NSubstitute;

namespace JD.AI.Tests.Agents;

public sealed class AgentSessionAdditionalTests : IDisposable
{
    private readonly TempDirectoryFixture _tempDir = new();
    private readonly IProviderRegistry _registry;
    private readonly Kernel _kernel;
    private readonly ProviderModelInfo _modelInfo;
    private readonly AgentSession _session;

    public AgentSessionAdditionalTests()
    {
        _registry = Substitute.For<IProviderRegistry>();
        _kernel = Kernel.CreateBuilder().Build();
        _modelInfo = new ProviderModelInfo(
            Id: "gpt-4",
            DisplayName: "GPT-4",
            ProviderName: "openai",
            ContextWindowTokens: 8192);

        _session = new AgentSession(_registry, _kernel, _modelInfo);
    }

    public void Dispose() => _tempDir.Dispose();

    // ── CloseSessionAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CloseSessionAsync_WithoutSessionInfo_ReturnsEarly()
    {
        var session = new AgentSession(_registry, _kernel, _modelInfo);

        var act = async () => await session.CloseSessionAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CloseSessionAsync_WithSessionInfo_MarksInactive()
    {
        await _session.InitializePersistenceAsync(_tempDir.DirectoryPath);
        _session.SessionInfo.Should().NotBeNull();
        _session.SessionInfo!.IsActive.Should().BeTrue();

        await _session.CloseSessionAsync();

        _session.SessionInfo.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CloseSessionAsync_EmitsAuditEvent()
    {
        var auditService = Substitute.For<AuditService>();
        _session.AuditService = auditService;
        await _session.InitializePersistenceAsync(_tempDir.DirectoryPath);

        await _session.CloseSessionAsync();

        await auditService.Received(1).EmitAsync(Arg.Is<AuditEvent>(e => e.Action == "session.close"));
    }

    // ── ExportSessionAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ExportSessionAsync_WithoutSessionInfo_ReturnsEarly()
    {
        var session = new AgentSession(_registry, _kernel, _modelInfo);

        var act = async () => await session.ExportSessionAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExportSessionAsync_WithSessionInfo_Completes()
    {
        await _session.InitializePersistenceAsync(_tempDir.DirectoryPath);

        var act = async () => await _session.ExportSessionAsync();

        await act.Should().NotThrowAsync();
    }

    // ── SwitchProviderAsync ────────────────────────────────────────────────

    [Fact]
    public async Task SwitchProviderAsync_WithNewModel_ChangesCurrentModel()
    {
        var newModel = new ProviderModelInfo(
            Id: "claude-3-sonnet",
            DisplayName: "Claude 3 Sonnet",
            ProviderName: "anthropic",
            ContextWindowTokens: 200000);

        var newKernel = Kernel.CreateBuilder().Build();
        _registry.BuildKernel(newModel).Returns(newKernel);

        await _session.SwitchProviderAsync(newModel, SwitchMode.Preserve);

        _session.CurrentModel.Should().Be(newModel);
    }

    [Fact]
    public async Task SwitchProviderAsync_CreatesNewForkPoint()
    {
        var newModel = new ProviderModelInfo(
            Id: "claude-3-sonnet",
            DisplayName: "Claude 3 Sonnet",
            ProviderName: "anthropic",
            ContextWindowTokens: 200000);

        var newKernel = Kernel.CreateBuilder().Build();
        _registry.BuildKernel(newModel).Returns(newKernel);

        var initialForkCount = _session.ForkPoints.Count;

        await _session.SwitchProviderAsync(newModel, SwitchMode.Preserve);

        _session.ForkPoints.Should().HaveCount(initialForkCount + 1);
    }

    [Fact]
    public async Task SwitchProviderAsync_RecordsSwitchHistory()
    {
        var newModel = new ProviderModelInfo(
            Id: "claude-3-sonnet",
            DisplayName: "Claude 3 Sonnet",
            ProviderName: "anthropic",
            ContextWindowTokens: 200000);

        var newKernel = Kernel.CreateBuilder().Build();
        _registry.BuildKernel(newModel).Returns(newKernel);

        var initialSwitchCount = _session.ModelSwitchHistory.Count;

        await _session.SwitchProviderAsync(newModel, SwitchMode.Preserve);

        _session.ModelSwitchHistory.Should().HaveCount(initialSwitchCount + 1);
    }

    // ── TrySwitchModelAsync ────────────────────────────────────────────────

    [Fact]
    public async Task TrySwitchModelAsync_WithExactIdMatch_SwitchesModel()
    {
        var newModel = new ProviderModelInfo(
            Id: "claude-3-sonnet",
            DisplayName: "Claude 3 Sonnet",
            ProviderName: "anthropic",
            ContextWindowTokens: 200000);

        var allModels = new[] { _modelInfo, newModel };
        _registry.GetModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult((IReadOnlyList<ProviderModelInfo>)allModels));

        var newKernel = Kernel.CreateBuilder().Build();
        _registry.BuildKernel(newModel).Returns(newKernel);

        var result = await _session.TrySwitchModelAsync("claude-3-sonnet");

        result.Should().BeTrue();
        _session.CurrentModel.Should().Be(newModel);
    }

    [Fact]
    public async Task TrySwitchModelAsync_WithNoMatch_ReturnsFalse()
    {
        var allModels = new[] { _modelInfo };
        _registry.GetModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult((IReadOnlyList<ProviderModelInfo>)allModels));

        var result = await _session.TrySwitchModelAsync("nonexistent-model");

        result.Should().BeFalse();
        _session.CurrentModel.Should().Be(_modelInfo);
    }

    // ── UpdateModelMetadata ────────────────────────────────────────────────

    [Fact]
    public void UpdateModelMetadata_UpdatesCurrentModel()
    {
        var enrichedModel = new ProviderModelInfo(
            Id: _modelInfo.Id,
            DisplayName: _modelInfo.DisplayName + " [Updated]",
            ProviderName: _modelInfo.ProviderName,
            ContextWindowTokens: 16384);

        _session.UpdateModelMetadata(enrichedModel);

        _session.CurrentModel.Should().Be(enrichedModel);
    }

    [Fact]
    public void UpdateModelMetadata_DoesNotCreateForkPoint()
    {
        var enrichedModel = new ProviderModelInfo(
            Id: _modelInfo.Id,
            DisplayName: "Updated",
            ProviderName: _modelInfo.ProviderName,
            ContextWindowTokens: 16384);

        var initialForkCount = _session.ForkPoints.Count;

        _session.UpdateModelMetadata(enrichedModel);

        _session.ForkPoints.Should().HaveCount(initialForkCount);
    }

    // ── RecordFileTouch ───────────────────────────────────────────────────

    [Fact]
    public async Task RecordFileTouch_WithoutCurrentTurn_DoesNotThrow()
    {
        var act = () => _session.RecordFileTouch("/test/file.txt", "read");

        act.Should().NotThrow();
    }

    [Fact]
    public async Task RecordFileTouch_WithCurrentTurn_RecordsFilePath()
    {
        await _session.InitializePersistenceAsync(_tempDir.DirectoryPath);
        _session.History.AddUserMessage("Test");

        var turn = new TurnRecord
        {
            SessionId = _session.SessionInfo!.Id,
            TurnIndex = 0,
            Role = "assistant",
            Content = "Test response",
        };
        _session.CurrentTurn = turn;

        _session.RecordFileTouch("/path/to/file.txt", "write");

        turn.FilesTouched.Should().HaveCount(1);
        turn.FilesTouched[0].FilePath.Should().Be("/path/to/file.txt");
        turn.FilesTouched[0].Operation.Should().Be("write");
    }

    [Fact]
    public async Task RecordFileTouch_WithCurrentTurn_MultipleFiles()
    {
        await _session.InitializePersistenceAsync(_tempDir.DirectoryPath);
        _session.History.AddUserMessage("Test");

        var turn = new TurnRecord
        {
            SessionId = _session.SessionInfo!.Id,
            TurnIndex = 0,
            Role = "assistant",
            Content = "Test response",
        };
        _session.CurrentTurn = turn;

        _session.RecordFileTouch("/file1.txt", "read");
        _session.RecordFileTouch("/file2.txt", "write");
        _session.RecordFileTouch("/file3.txt", "delete");

        turn.FilesTouched.Should().HaveCount(3);
    }
}
