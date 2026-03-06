# Coverage Wave 2: Orchestration, Installation, Startup & Routing

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Close the remaining test coverage gaps identified in the repo audit — orchestration strategies (9), checkpointing (3), installation strategies (3+factory+detector), OpenClaw routing handlers (4), startup modules, and supporting utilities.

**Architecture:** Unit tests with NSubstitute/manual mocks for `ISubagentExecutor`, `IOpenClawModeHandler`, and process execution. All strategies are stateless with no-arg constructors, making them ideal for isolated unit testing. Startup modules are static classes testable via parameter injection. Follow existing xUnit + FluentAssertions + `sealed class` conventions.

**Tech Stack:** xUnit, NSubstitute, FluentAssertions, Console.SetOut for output capture

**Previous coverage verified as adequate (DO NOT re-test):**
- All channel adapters (Tasks 1-3 of Wave 1)
- DiffRenderer, HistoryViewer, MarkdownRenderer (Tasks 4-5)
- PluginCliHandler, UpdateCliHandler, AgentsCliHandler, PolicyCliHandler (Task 6)
- GenAiAttributes, TelemetryConfig (Task 7)
- Dashboard settings tabs (Tasks 8-10)
- SystemPromptBuilder (3 tests, all branches covered)
- PolicyComplianceCliHandler, PolicySubcommandHandler (comprehensive)
- DefaultModelRouter (5+ tests covering all strategies)
- DataRedactor (20+ tests), PolicyParser (12+ tests)
- CompliancePresetLoader (17+ tests via CompliancePresetTests)
- ApiKeyDetectorTests (covers Anthropic, OpenRouter base pattern)

---

## Task 1: SequentialStrategy + PipelineStrategy + FanOutStrategy Tests

**Files:**
- Create: `tests/JD.AI.Tests/Agents/Orchestration/SequentialStrategyTests.cs`
- Create: `tests/JD.AI.Tests/Agents/Orchestration/PipelineStrategyTests.cs`
- Create: `tests/JD.AI.Tests/Agents/Orchestration/FanOutStrategyTests.cs`
- Reference: `src/JD.AI.Core/Agents/Orchestration/Strategies/SequentialStrategy.cs`
- Reference: `src/JD.AI.Core/Agents/Orchestration/Strategies/PipelineStrategy.cs`
- Reference: `src/JD.AI.Core/Agents/Orchestration/Strategies/FanOutStrategy.cs`
- Reference: `src/JD.AI.Core/Agents/Orchestration/ISubagentExecutor.cs`
- Reference: `src/JD.AI.Core/Agents/Orchestration/TeamContext.cs`

**Shared test helper (create once, reuse):**
```csharp
// tests/JD.AI.Tests/Agents/Orchestration/OrchestrationTestHelpers.cs
namespace JD.AI.Tests.Agents.Orchestration;

internal static class OrchestrationTestHelpers
{
    internal static SubagentConfig Agent(string name, string prompt = "do work") =>
        new(name, prompt);

    internal static AgentResult Success(string name, string output) =>
        new(name, output, true, null, 100, TimeSpan.FromMilliseconds(50), []);

    internal static AgentResult Failure(string name, string error) =>
        new(name, "", false, error, 0, TimeSpan.FromMilliseconds(10), []);

    internal static TeamContext Context(string goal = "test goal") => new(goal);

    internal static AgentSession FakeSession() =>
        new(); // minimal construction — check actual constructor
}
```

**SequentialStrategy tests:**
- `Name_IsSequential` — verifies `Name == "sequential"`
- `ExecuteAsync_SingleAgent_ReturnsItsOutput` — one agent returns "hello"
- `ExecuteAsync_TwoAgents_ChainsOutputInPrompt` — verify 2nd agent receives augmented prompt with previous output
- `ExecuteAsync_EmptyAgentList_ReturnsNoOutput` — returns "(no output)"
- `ExecuteAsync_StoresResultsInScratchpad` — verify `context.ReadScratchpad("output:{name}")` has values

**PipelineStrategy tests:**
- `Name_IsPipeline` — verifies `Name == "pipeline"`
- `ExecuteAsync_ChainsInputThroughStages` — verify each stage output feeds next stage input
- `ExecuteAsync_FailFast_StopsOnFirstFailure` — middle stage fails → pipeline stops, partial output
- `ExecuteAsync_SingleStage_ReturnsDirectly` — one agent → no chaining

**FanOutStrategy tests:**
- `Name_IsFanOut` — verifies `Name == "fan-out"`
- `ExecuteAsync_AllAgentsRunInParallel` — verify all agents executed
- `ExecuteAsync_CreatesSynthesizerAgent` — verify synthesizer is invoked after all agents
- `ExecuteAsync_SynthesizerReceivesAllOutputs` — verify synthesizer prompt contains all agent outputs

**Testing approach:**
- Create a `FakeSubagentExecutor` that records calls and returns pre-configured results
- Use `TeamContext` directly (it's a concrete class with thread-safe scratchpad)
- Verify executor was called with expected configs by inspecting recorded calls

**Step 1: Create the test helper and FakeSubagentExecutor**
```csharp
internal sealed class FakeSubagentExecutor : ISubagentExecutor
{
    private readonly Dictionary<string, AgentResult> _results = new();
    private readonly List<SubagentConfig> _calls = [];

    public IReadOnlyList<SubagentConfig> Calls => _calls;

    public FakeSubagentExecutor WithResult(string agentName, AgentResult result)
    {
        _results[agentName] = result;
        return this;
    }

    public Task<AgentResult> ExecuteAsync(
        SubagentConfig config,
        AgentSession parentSession,
        TeamContext? teamContext = null,
        Action<SubagentProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        _calls.Add(config);
        var result = _results.GetValueOrDefault(config.Name,
            OrchestrationTestHelpers.Success(config.Name, $"output from {config.Name}"));
        return Task.FromResult(result);
    }
}
```

**Step 2: Write SequentialStrategy tests**

**Step 3: Write PipelineStrategy tests**

**Step 4: Write FanOutStrategy tests**

**Step 5: Run tests**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~SequentialStrategyTests|FullyQualifiedName~PipelineStrategyTests|FullyQualifiedName~FanOutStrategyTests"
```

**Step 6: Commit**
```bash
git add tests/JD.AI.Tests/Agents/Orchestration/
git commit -m "test: add SequentialStrategy, PipelineStrategy, FanOutStrategy unit tests"
```

---

## Task 2: SupervisorStrategy + VotingStrategy + BlackboardStrategy Tests

**Files:**
- Create: `tests/JD.AI.Tests/Agents/Orchestration/SupervisorStrategyTests.cs`
- Create: `tests/JD.AI.Tests/Agents/Orchestration/VotingStrategyTests.cs`
- Create: `tests/JD.AI.Tests/Agents/Orchestration/BlackboardStrategyTests.cs`
- Reference: `src/JD.AI.Core/Agents/Orchestration/Strategies/SupervisorStrategy.cs`
- Reference: `src/JD.AI.Core/Agents/Orchestration/Strategies/VotingStrategy.cs`
- Reference: `src/JD.AI.Core/Agents/Orchestration/Strategies/BlackboardStrategy.cs`

**SupervisorStrategy tests:**
- `Name_IsSupervisor`
- `ExecuteAsync_WorkersRunInParallel_ThenSupervisorReviews` — verify 2-phase execution
- `ExecuteAsync_SupervisorApproves_ReturnsFinalOutput` — supervisor returns "APPROVED: final answer"
- `ExecuteAsync_SupervisorRedirects_ReRunsWorkers` — supervisor returns "REDIRECT: fix this" → workers re-run
- `ExecuteAsync_MaxIterations_StopsAfterThreeLoops` — verify max 3 supervisor iterations

**VotingStrategy tests:**
- `Name_IsVoting`
- `ExecuteAsync_AllAgentsVoteInParallel` — verify parallel execution
- `ExecuteAsync_CreatesAggregatorAgent` — verify aggregator receives all votes
- `ExecuteAsync_WeightsIncludedInPrompt` — set custom weights, verify they appear in aggregator prompt

**BlackboardStrategy tests:**
- `Name_IsBlackboard`
- `ExecuteAsync_ConvergesWhenAllAgentsSignal` — agents return "[CONVERGED]" → stops early
- `ExecuteAsync_StopsAtMaxIterations` — 3 iterations without convergence → stops
- `ExecuteAsync_BlackboardStateAccumulatesAcrossRounds` — verify scratchpad state grows

**Testing approach:** Same `FakeSubagentExecutor` pattern. For SupervisorStrategy, configure the executor to return different results based on agent name pattern (e.g., "supervisor-review-1" → "APPROVED: done").

**Step 1: Write SupervisorStrategy tests**

**Step 2: Write VotingStrategy tests**

**Step 3: Write BlackboardStrategy tests**

**Step 4: Run and verify**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~SupervisorStrategyTests|FullyQualifiedName~VotingStrategyTests|FullyQualifiedName~BlackboardStrategyTests"
```

**Step 5: Commit**
```bash
git add tests/JD.AI.Tests/Agents/Orchestration/
git commit -m "test: add SupervisorStrategy, VotingStrategy, BlackboardStrategy unit tests"
```

---

## Task 3: DebateStrategy + RelayStrategy + MapReduceStrategy Tests

**Files:**
- Create: `tests/JD.AI.Tests/Agents/Orchestration/DebateStrategyTests.cs`
- Create: `tests/JD.AI.Tests/Agents/Orchestration/RelayStrategyTests.cs`
- Create: `tests/JD.AI.Tests/Agents/Orchestration/MapReduceStrategyTests.cs`
- Reference: `src/JD.AI.Core/Agents/Orchestration/Strategies/DebateStrategy.cs`
- Reference: `src/JD.AI.Core/Agents/Orchestration/Strategies/RelayStrategy.cs`
- Reference: `src/JD.AI.Core/Agents/Orchestration/Strategies/MapReduceStrategy.cs`

**DebateStrategy tests:**
- `Name_IsDebate`
- `ExecuteAsync_DebatersRunInParallel_ThenJudgeSynthesizes` — verify 2-phase execution
- `ExecuteAsync_JudgeReceivesAllDebaterArguments` — verify judge prompt contains all arguments
- `ExecuteAsync_UsesAgentPerspective` — agents with `Perspective` property → used in system prompt

**RelayStrategy tests:**
- `Name_IsRelay`
- `ExecuteAsync_AgentsRunSequentially_EachRefiningPreviousOutput`
- `ExecuteAsync_StopEarlyOnNoChangesToken` — agent returns "[NO_CHANGES]" → stops
- `ExecuteAsync_ContinuesWhenStopEarlyFalse` — StopEarly=false ignores "[NO_CHANGES]"

**MapReduceStrategy tests:**
- `Name_IsMapReduce`
- `ExecuteAsync_MappersRunInParallel_ThenReducerSynthesizes`
- `ExecuteAsync_SingleAgent_ActsAsMapperWithNoReduce`
- `ExecuteAsync_EmptyAgentList_ReturnsError`
- `ExecuteAsync_RespectsMaxParallelism` — verify bounded concurrency

**Step 1: Write DebateStrategy tests**

**Step 2: Write RelayStrategy tests**

**Step 3: Write MapReduceStrategy tests**

**Step 4: Run and verify**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~DebateStrategyTests|FullyQualifiedName~RelayStrategyTests|FullyQualifiedName~MapReduceStrategyTests"
```

**Step 5: Commit**
```bash
git add tests/JD.AI.Tests/Agents/Orchestration/
git commit -m "test: add DebateStrategy, RelayStrategy, MapReduceStrategy unit tests"
```

---

## Task 4: TeamOrchestrator + TeamContext Unit Tests

**Files:**
- Create: `tests/JD.AI.Tests/Agents/Orchestration/TeamOrchestratorTests.cs`
- Create: `tests/JD.AI.Tests/Agents/Orchestration/TeamContextTests.cs`
- Reference: `src/JD.AI.Core/Agents/Orchestration/TeamOrchestrator.cs`
- Reference: `src/JD.AI.Core/Agents/Orchestration/TeamContext.cs`

**TeamContext tests:**
- `WriteScratchpad_And_ReadScratchpad_RoundTrips`
- `RemoveScratchpad_RemovesEntry`
- `GetScratchpadSnapshot_ReturnsImmutableCopy`
- `RecordEvent_And_GetEventsSnapshot_PreservesOrder`
- `GetEventsFor_FiltersToSpecificAgent`
- `SetResult_And_GetResult_Stores`
- `AllCompleted_ReturnsTrueWhenAllPresent`
- `AllCompleted_ReturnsFalseWhenMissing`
- `CanNest_TrueWhenUnderMaxDepth`
- `CanNest_FalseWhenAtMaxDepth`
- `CreateChildContext_IncrementsDepth`

**TeamOrchestrator tests:**
- `RunTeamAsync_ResolvesSequentialStrategy`
- `RunTeamAsync_ResolvesFanOutByAlias` — "parallel" resolves to FanOutStrategy
- `RunTeamAsync_UnknownStrategy_ThrowsOrReturnsError`
- `RunTeamAsync_SelectsMultiTurnExecutor_WhenFlagSet`
- `RunAgentAsync_RespectsMaxNestingDepth`

**Testing approach:**
- TeamContext is fully testable without mocks (concrete, thread-safe)
- TeamOrchestrator needs mocked `AgentSession` — check constructor requirements

**Step 1: Write TeamContext tests**

**Step 2: Write TeamOrchestrator tests**

**Step 3: Run and verify**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~TeamOrchestratorTests|FullyQualifiedName~TeamContextTests"
```

**Step 4: Commit**
```bash
git add tests/JD.AI.Tests/Agents/Orchestration/
git commit -m "test: add TeamOrchestrator and TeamContext unit tests"
```

---

## Task 5: Checkpointing Strategy Tests

**Files:**
- Create: `tests/JD.AI.Tests/Agents/Checkpointing/CommitCheckpointStrategyTests.cs`
- Create: `tests/JD.AI.Tests/Agents/Checkpointing/DirectoryCheckpointStrategyTests.cs`
- Create: `tests/JD.AI.Tests/Agents/Checkpointing/StashCheckpointStrategyTests.cs`
- Reference: `src/JD.AI.Core/Agents/Checkpointing/ICheckpointStrategy.cs`
- Reference: `src/JD.AI.Core/Agents/Checkpointing/CommitCheckpointStrategy.cs`
- Reference: `src/JD.AI.Core/Agents/Checkpointing/DirectoryCheckpointStrategy.cs`
- Reference: `src/JD.AI.Core/Agents/Checkpointing/StashCheckpointStrategy.cs`

**What to test:**
- Read the `ICheckpointStrategy` interface first to understand the contract
- For each strategy: verify `Name` property, test `CreateAsync`/`RestoreAsync`/`ListAsync` with temp directories
- `CommitCheckpointStrategy` — uses git commits (needs a temp git repo)
- `DirectoryCheckpointStrategy` — copies directory snapshots (filesystem operations)
- `StashCheckpointStrategy` — uses git stash (needs a temp git repo)

**Testing approach:**
- Use `TempDirectoryFixture` pattern (create temp dir, init git repo, add files, test checkpoint create/restore)
- For git-based strategies: `git init`, add a file, commit, then test create/restore

**Step 1: Read interface to understand contract**

**Step 2: Write CommitCheckpointStrategyTests**

**Step 3: Write DirectoryCheckpointStrategyTests**

**Step 4: Write StashCheckpointStrategyTests**

**Step 5: Run and verify**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~CheckpointStrategy"
```

**Step 6: Commit**
```bash
git add tests/JD.AI.Tests/Agents/Checkpointing/
git commit -m "test: add checkpointing strategy unit tests"
```

---

## Task 6: InstallerFactory + PackageManagerStrategy Command Generation Tests

**Files:**
- Create: `tests/JD.AI.Tests/Installation/InstallerFactoryTests.cs`
- Create: `tests/JD.AI.Tests/Installation/PackageManagerStrategyTests.cs`
- Create: `tests/JD.AI.Tests/Installation/InstallationDetectorTests.cs`
- Reference: `src/JD.AI.Core/Installation/InstallerFactory.cs`
- Reference: `src/JD.AI.Core/Installation/PackageManagerStrategy.cs`
- Reference: `src/JD.AI.Core/Installation/InstallationDetector.cs`
- Reference: `src/JD.AI.Core/Installation/InstallKind.cs`

**InstallerFactory tests:**
- `Create_DotnetTool_ReturnsDotnetToolStrategy`
- `Create_Winget_ReturnsPackageManagerStrategy`
- `Create_Chocolatey_ReturnsPackageManagerStrategy`
- `Create_Scoop_ReturnsPackageManagerStrategy`
- `Create_Brew_ReturnsPackageManagerStrategy`
- `Create_Apt_ReturnsPackageManagerStrategy`
- `Create_NativeBinary_ReturnsGitHubReleaseStrategy`
- `Create_Unknown_ReturnsGitHubReleaseStrategy`

**PackageManagerStrategy tests (command generation only, don't execute):**
- `Name_MatchesInstallKind` — verify `.Name` for each manager
- `Constructor_ThrowsForNonPackageManager` — e.g., `InstallKind.DotnetTool` → throws

**InstallationDetector tests:**
- `GetCurrentVersion_ReturnsNonNullNonEmpty`
- `GetCurrentRid_ReturnsValidFormat` — matches pattern `(win|linux|osx)-(x64|arm64|x86)`

**Testing notes:**
- InstallerFactory tests are pure factory mapping — no I/O needed
- PackageManagerStrategy constructor validation is testable
- InstallationDetector version/RID methods are safe to call in test env

**Step 1-3: Write tests for each file**

**Step 4: Run and verify**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~InstallerFactory|FullyQualifiedName~PackageManagerStrategy|FullyQualifiedName~InstallationDetector"
```

**Step 5: Commit**
```bash
git add tests/JD.AI.Tests/Installation/
git commit -m "test: add InstallerFactory, PackageManagerStrategy, InstallationDetector unit tests"
```

---

## Task 7: ModelCapabilityHeuristics + QuestionValidation Tests

**Files:**
- Create: `tests/JD.AI.Tests/Providers/ModelCapabilityHeuristicsTests.cs`
- Create: `tests/JD.AI.Tests/Questions/QuestionValidationTests.cs`
- Reference: `src/JD.AI.Core/Providers/ModelCapabilityHeuristics.cs`
- Reference: `src/JD.AI.Core/Questions/QuestionValidation.cs`

**ModelCapabilityHeuristics tests (static `InferFromName`):**
- `InferFromName_UnknownModel_ReturnsChatOnly`
- `InferFromName_Llama31_ReturnsToolCalling` — "llama-3.1-70b"
- `InferFromName_Qwen25_ReturnsToolCalling` — "qwen2.5-coder"
- `InferFromName_LLaVA_ReturnsVision` — "llava-1.6"
- `InferFromName_Gemma3_ReturnsToolCallingAndVision` — "gemma-3-27b"
- `InferFromName_IsCaseInsensitive` — "LLAMA-3.1" same as "llama-3.1"

**QuestionValidation tests:**
- Read `QuestionValidation.cs` first to understand what methods it has
- Test each validation method with valid and invalid inputs
- Expected: null/empty validation, format validation, range validation

**Step 1: Read QuestionValidation.cs**

**Step 2: Write ModelCapabilityHeuristicsTests**

**Step 3: Write QuestionValidationTests**

**Step 4: Run and verify**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~ModelCapabilityHeuristicsTests|FullyQualifiedName~QuestionValidationTests"
```

**Step 5: Commit**
```bash
git add tests/JD.AI.Tests/Providers/ModelCapabilityHeuristicsTests.cs tests/JD.AI.Tests/Questions/
git commit -m "test: add ModelCapabilityHeuristics and QuestionValidation unit tests"
```

---

## Task 8: OpenClaw Routing Mode Handler Tests

**Files:**
- Create: `tests/JD.AI.Tests/Channels/OpenClaw/PassthroughModeHandlerTests.cs`
- Create: `tests/JD.AI.Tests/Channels/OpenClaw/InterceptModeHandlerTests.cs`
- Create: `tests/JD.AI.Tests/Channels/OpenClaw/ProxyModeHandlerTests.cs`
- Create: `tests/JD.AI.Tests/Channels/OpenClaw/SidecarModeHandlerTests.cs`
- Reference: `src/JD.AI.Channels.OpenClaw/Routing/PassthroughModeHandler.cs`
- Reference: `src/JD.AI.Channels.OpenClaw/Routing/InterceptModeHandler.cs`
- Reference: `src/JD.AI.Channels.OpenClaw/Routing/ProxyModeHandler.cs`
- Reference: `src/JD.AI.Channels.OpenClaw/Routing/SidecarModeHandler.cs`

**PassthroughModeHandler tests (simplest):**
- `Mode_IsPassthrough`
- `HandleAsync_AlwaysReturnsFalse` — never handles events

**InterceptModeHandler tests:**
- `Mode_IsIntercept`
- `HandleAsync_ExtractsUserMessage_Processes_Injects` — verify abort → process → inject sequence

**ProxyModeHandler tests:**
- `Mode_IsProxy`
- `HandleAsync_Processes_Injects_WithoutAbort` — no abort call, just process → inject

**SidecarModeHandler tests:**
- `Mode_IsSidecar`
- `HandleAsync_TriggeredByCommandPrefix_ProcessesMessage` — "/jdai hello" triggers JD.AI
- `HandleAsync_NotTriggered_ReturnsFalse` — regular message → false (OpenClaw handles)
- `HandleAsync_StripsDiscordMention` — `<@123456>` stripped from input

**Testing approach:**
- Read each handler to understand constructor dependencies
- Mock `OpenClawBridgeChannel` (or use NSubstitute) for `AbortSessionAsync`/`InjectMessageAsync`
- Mock `Func<string, string, Task<string?>>` message processor
- Test that the correct sequence of calls happens

**Step 1-4: Write tests for each handler**

**Step 5: Run and verify**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~ModeHandlerTests"
```

**Step 6: Commit**
```bash
git add tests/JD.AI.Tests/Channels/OpenClaw/
git commit -m "test: add OpenClaw routing mode handler unit tests"
```

---

## Task 9: OnboardingCliHandler Tests

**Files:**
- Create: `tests/JD.AI.Tests/Startup/OnboardingCliHandlerTests.cs`
- Reference: `src/JD.AI/Startup/OnboardingCliHandler.cs`

**What to test:**
- `RunAsync(["--help"])` or `RunAsync([])` — returns 0 with help output
- Flag parsing: `--global`, `--skip-mcp`, `--provider <name>`, `--model <id>`
- `GetFlagValue` helper (private but exercised through public API)

**Testing approach:**
- OnboardingCliHandler is a static class — test via `RunAsync` with controlled args
- Redirect Console.Out to capture output
- Use `[Collection("Console")]` since it captures stdout
- For provider/model resolution tests, need to mock or control what providers are detected
- Focus on argument parsing paths that don't require real provider connectivity

**Step 1: Read the file to understand interactive vs non-interactive paths**

**Step 2: Write tests for non-interactive flag parsing paths**

**Step 3: Run and verify**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~OnboardingCliHandlerTests"
```

**Step 4: Commit**
```bash
git add tests/JD.AI.Tests/Startup/OnboardingCliHandlerTests.cs
git commit -m "test: add OnboardingCliHandler unit tests"
```

---

## Task 10: SessionConfigurator + GovernanceInitializer Expanded Tests

**Files:**
- Modify: `tests/JD.AI.Tests/Startup/SessionConfiguratorTests.cs`
- Modify: `tests/JD.AI.Tests/Startup/GovernanceInitializerTests.cs`
- Reference: `src/JD.AI/Startup/SessionConfigurator.cs`
- Reference: `src/JD.AI/Startup/GovernanceInitializer.cs`

**New SessionConfigurator tests:**
- `ConfigureAsync_PlanPermissionMode_SetsPlanMode`
- `ConfigureAsync_AcceptEditsPermissionMode_SetsAcceptEdits`
- `ConfigureAsync_DontAskPermissionMode_SetsDontAsk`
- `ConfigureAsync_WithBudgetUsd_SetsBudgetLimit`
- `ConfigureAsync_WithDebugCategories_ParsesCorrectly`

**New GovernanceInitializer tests:**
- `Initialize_WithMaxBudgetUsd_CreatesBudgetTracker`
- `Initialize_WithAllowedTools_FiltersKernelPlugins`
- `Initialize_WithDisallowedTools_RemovesFromKernel`
- `Initialize_WithNoGitDir_UsesDirectoryCheckpointStrategy`

**Testing approach:**
- Read existing tests to understand the fixture/mock setup
- Add new test methods following the same patterns
- Focus on untested branches identified in the analysis

**Step 1: Read existing test files to understand setup**

**Step 2: Add new SessionConfigurator tests**

**Step 3: Add new GovernanceInitializer tests**

**Step 4: Run and verify**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~SessionConfiguratorTests|FullyQualifiedName~GovernanceInitializerTests"
```

**Step 5: Commit**
```bash
git add tests/JD.AI.Tests/Startup/SessionConfiguratorTests.cs tests/JD.AI.Tests/Startup/GovernanceInitializerTests.cs
git commit -m "test: expand SessionConfigurator and GovernanceInitializer coverage"
```

---

## Task 11: FileRoleResolver + PolicyLoader Tests

**Files:**
- Create: `tests/JD.AI.Tests/Governance/FileRoleResolverTests.cs`
- Create: `tests/JD.AI.Tests/Governance/PolicyLoaderTests.cs`
- Reference: `src/JD.AI.Core/Governance/FileRoleResolver.cs`
- Reference: `src/JD.AI.Core/Governance/PolicyLoader.cs`

**FileRoleResolver tests:**
- `ResolveRole_KnownUser_ReturnsRole`
- `ResolveRole_UnknownUser_ReturnsNull`
- `ResolveRole_NullUserId_ReturnsNull`
- `ResolveGroups_KnownUser_ReturnsGroups`
- `ResolveGroups_UnknownUser_ReturnsEmptyList`
- `Constructor_MissingFile_CreatesEmptyResolver`

**PolicyLoader tests:**
- `Load_NoPoliciesDir_ReturnsEmptyList`
- `Load_WithGlobalPolicies_ReturnsSortedByPriority`
- `Load_WithProjectPath_IncludesProjectPolicies`
- `Load_MultipleScopes_OrdersGlobalBeforeProject`

**Testing approach:**
- Use temp directories with YAML policy files
- FileRoleResolver takes a file path → create temp YAML file
- PolicyLoader reads from DataDirectories → may need env var setup (`[Collection("DataDirectories")]`)

**Step 1-2: Write FileRoleResolver tests with YAML fixtures**

**Step 3-4: Write PolicyLoader tests with temp directories**

**Step 5: Run and verify**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~FileRoleResolverTests|FullyQualifiedName~PolicyLoaderTests"
```

**Step 6: Commit**
```bash
git add tests/JD.AI.Tests/Governance/FileRoleResolverTests.cs tests/JD.AI.Tests/Governance/PolicyLoaderTests.cs
git commit -m "test: add FileRoleResolver and PolicyLoader unit tests"
```

---

## Task 12: InProcessEventBus + PluginIntegrityVerifier Tests

**Files:**
- Create: `tests/JD.AI.Tests/Events/InProcessEventBusTests.cs`
- Create: `tests/JD.AI.Tests/Plugins/PluginIntegrityVerifierTests.cs`
- Reference: `src/JD.AI.Core/Events/InProcessEventBus.cs`
- Reference: `src/JD.AI.Core/Plugins/PluginIntegrityVerifier.cs`

**InProcessEventBus tests:**
- `PublishAsync_NotifiesSubscribers`
- `Subscribe_ReturnsDisposableHandle`
- `Unsubscribe_StopsReceivingEvents`
- `PublishAsync_MultipleSubscribers_AllNotified`
- `PublishAsync_NoSubscribers_DoesNotThrow`

**PluginIntegrityVerifier tests:**
- Read the class first to understand verification logic
- Test hash verification, signature checking, tamper detection
- Test with valid and corrupted plugin manifests

**Step 1: Read both files**

**Step 2: Write InProcessEventBus tests**

**Step 3: Write PluginIntegrityVerifier tests**

**Step 4: Run and verify**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj --filter "FullyQualifiedName~InProcessEventBusTests|FullyQualifiedName~PluginIntegrityVerifierTests"
```

**Step 5: Commit**
```bash
git add tests/JD.AI.Tests/Events/ tests/JD.AI.Tests/Plugins/PluginIntegrityVerifierTests.cs
git commit -m "test: add InProcessEventBus and PluginIntegrityVerifier unit tests"
```

---

## Task 13: Build Verification + Push + CI Watch

**Step 1: Full solution build**
```bash
dotnet build JD.AI.slnx --configuration Release
```
Expected: 0 errors

**Step 2: Run all unit tests**
```bash
dotnet test tests/JD.AI.Tests/JD.AI.Tests.csproj
```
Expected: All pass (previous 2761+ existing tests plus ~85 new)

**Step 3: Run all BDD specs**
```bash
dotnet test tests/JD.AI.Specs/JD.AI.Specs.csproj
```
Expected: All pass

**Step 4: Run Gateway tests**
```bash
dotnet test tests/JD.AI.Gateway.Tests/JD.AI.Gateway.Tests.csproj
```
Expected: All pass

**Step 5: Push and watch CI**
```bash
git push
gh run watch <run-id>
```
Expected: All green

---

## Summary

| Task | Area | New Tests | Test Type |
|------|------|-----------|-----------|
| 1 | Sequential + Pipeline + FanOut strategies | ~12 | Unit |
| 2 | Supervisor + Voting + Blackboard strategies | ~12 | Unit |
| 3 | Debate + Relay + MapReduce strategies | ~12 | Unit |
| 4 | TeamOrchestrator + TeamContext | ~16 | Unit |
| 5 | Checkpointing strategies (3) | ~12 | Unit |
| 6 | InstallerFactory + PackageManager + Detector | ~12 | Unit |
| 7 | ModelCapabilityHeuristics + QuestionValidation | ~10 | Unit |
| 8 | OpenClaw routing mode handlers (4) | ~10 | Unit |
| 9 | OnboardingCliHandler | ~5 | Unit |
| 10 | SessionConfigurator + GovernanceInitializer expand | ~9 | Unit |
| 11 | FileRoleResolver + PolicyLoader | ~10 | Unit |
| 12 | InProcessEventBus + PluginIntegrityVerifier | ~10 | Unit |
| 13 | Build + Push + CI | — | Verification |

**Total new coverage: ~130 tests closing gaps in orchestration, installation, startup, routing, and supporting utilities**
