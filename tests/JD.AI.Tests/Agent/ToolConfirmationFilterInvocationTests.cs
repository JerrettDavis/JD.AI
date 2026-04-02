using System.Globalization;
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.Providers;
using JD.AI.Core.Safety;
using JD.AI.Core.Sessions;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;

namespace JD.AI.Tests.Agent;

public sealed class ToolConfirmationFilterInvocationTests
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyMetadata =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_ExplicitDeny_BlocksBeforeExecution()
    {
        var session = CreateSession();
        session.ToolPermissionProfile.AddDenied("run_command", projectScope: false);
        session.CurrentTurn = CreateTurn(session);
        var output = new SpyAgentOutput();
        var filter = new ToolConfirmationFilter(session, output: output);
        var context = CreateContext(session.Kernel, "run_command", new KernelArguments
        {
            ["command"] = "git status"
        });
        var nextCalled = false;

        await filter.OnAutoFunctionInvocationAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeFalse();
        context.Result.GetValue<string>().Should().Be("Tool blocked: blocked by explicit deny rule.");
        output.Warnings.Should().ContainSingle(m => m.Contains("blocked by explicit deny rule", StringComparison.Ordinal));
        session.CurrentTurn.ToolCalls.Should().ContainSingle(call =>
            call.ToolName == "run_command" &&
            call.Status == "denied");
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_WorkflowPromptAccepted_StartsRecordingAndCapturesStep()
    {
        var session = CreateSession();
        session.CurrentTurn = CreateTurn(session);
        var output = new SpyAgentOutput();
        output.WorkflowResponses.Enqueue(true);
        output.ToolResponses.Enqueue(true);
        var filter = new ToolConfirmationFilter(session, output: output);
        var context = CreateContext(session.Kernel, "run_command", new KernelArguments
        {
            ["command"] = "dotnet test"
        });

        await filter.OnAutoFunctionInvocationAsync(context, ctx => CompleteInvocationAsync(ctx, "done"));

        output.WorkflowPromptCount.Should().Be(1);
        session.ActiveWorkflowName.Should().Be("recording");
        session.CapturedWorkflowSteps.Should().ContainSingle()
            .Which.Should().Be(("run_command", "command=[REDACTED]"));
        output.InfoMessages.Should().Contain(m => m.Contains("Recording workflow", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_WorkflowCapture_DeniedToolIsNotRecorded()
    {
        var session = CreateSession();
        session.CurrentTurn = CreateTurn(session);
        session.ActiveWorkflowName = "recording";
        session.ToolPermissionProfile.AddDenied("run_command", projectScope: false);
        var output = new SpyAgentOutput();
        var filter = new ToolConfirmationFilter(session, output: output);
        var context = CreateContext(session.Kernel, "run_command", new KernelArguments
        {
            ["command"] = "dotnet test"
        });

        await filter.OnAutoFunctionInvocationAsync(context, _ => Task.CompletedTask);

        session.CapturedWorkflowSteps.Should().BeEmpty();
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_PolicyDeny_StopsAndEmitsAudit()
    {
        var session = CreateSession();
        session.CurrentTurn = CreateTurn(session);
        var output = new SpyAgentOutput();
        var policy = Substitute.For<IPolicyEvaluator>();
        policy.EvaluateTool("run_command", Arg.Any<PolicyContext>())
            .Returns(new PolicyEvaluationResult(PolicyDecision.Deny, "shell disabled"));
        var sink = new InMemoryAuditSink();
        var audit = new AuditService([sink]);
        var filter = new ToolConfirmationFilter(session, policy, audit, output: output);
        var context = CreateContext(session.Kernel, "run_command", new KernelArguments
        {
            ["command"] = "git status"
        });
        var nextCalled = false;

        await filter.OnAutoFunctionInvocationAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeFalse();
        context.Result.GetValue<string>().Should().Be("Blocked by policy: shell disabled");
        output.Warnings.Should().ContainSingle(m => m.Contains("Policy blocked", StringComparison.Ordinal));
        var auditEvent = (await sink.QueryAsync(new AuditQuery { Limit = 10 })).Events.Should().ContainSingle().Subject;
        auditEvent.Resource.Should().Be("run_command");
        auditEvent.PolicyResult.Should().Be(PolicyDecision.Deny);
        auditEvent.Detail.Should().Contain("status=denied");
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_ConfirmOnceTool_OnlyPromptsOncePerSession()
    {
        var session = CreateSession();
        session.CurrentTurn = CreateTurn(session);
        session.WorkflowDeclinedThisTurn = true;
        var output = new SpyAgentOutput();
        output.ToolResponses.Enqueue(true);
        var filter = new ToolConfirmationFilter(session, output: output);

        var first = CreateContext(session.Kernel, "write_file", new KernelArguments
        {
            ["path"] = @"C:\temp\alpha.txt",
            ["content"] = "alpha"
        });
        var second = CreateContext(session.Kernel, "write_file", new KernelArguments
        {
            ["path"] = @"C:\temp\beta.txt",
            ["content"] = "beta"
        });

        await filter.OnAutoFunctionInvocationAsync(first, ctx => CompleteInvocationAsync(ctx, "first"));
        await filter.OnAutoFunctionInvocationAsync(second, ctx => CompleteInvocationAsync(ctx, "second"));

        output.ToolPromptCount.Should().Be(1);
        output.RenderedToolCalls.Should().HaveCount(2);
        output.InfoMessages.Should().Contain(m => m.Contains(@"path=C:\temp\beta.txt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_BypassAllMode_SkipsConfirmationPrompt()
    {
        var session = CreateSession();
        session.PermissionMode = PermissionMode.BypassAll;
        session.CurrentTurn = CreateTurn(session);
        var output = new SpyAgentOutput();
        var filter = new ToolConfirmationFilter(session, output: output);
        var context = CreateContext(session.Kernel, "run_command", new KernelArguments
        {
            ["command"] = "dotnet --info"
        });

        await filter.OnAutoFunctionInvocationAsync(context, ctx => CompleteInvocationAsync(ctx, "ok"));

        output.ToolPromptCount.Should().Be(0);
        output.WorkflowPromptCount.Should().Be(0);
        output.InfoMessages.Should().ContainSingle(m => m.Contains("run_command", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_CircuitBreakerWarn_ContinuesAndRendersWarning()
    {
        var session = CreateSession();
        session.PermissionMode = PermissionMode.BypassAll;
        session.CurrentTurn = CreateTurn(session);
        var output = new SpyAgentOutput();
        var breaker = new CircuitBreaker(new ToolLoopDetector(repetitionWarningThreshold: 2, repetitionHardStopThreshold: 3));
        var filter = new ToolConfirmationFilter(session, circuitBreaker: breaker, output: output);

        await filter.OnAutoFunctionInvocationAsync(
            CreateContext(session.Kernel, "run_command", new KernelArguments { ["command"] = "build" }),
            ctx => CompleteInvocationAsync(ctx, "first"));
        await filter.OnAutoFunctionInvocationAsync(
            CreateContext(session.Kernel, "run_command", new KernelArguments { ["command"] = "build" }),
            ctx => CompleteInvocationAsync(ctx, "second"));

        output.Warnings.Should().Contain(m => m.Contains("Loop warning", StringComparison.Ordinal));
        session.CurrentTurn.ToolCalls.Should().HaveCount(2);
        session.CurrentTurn.ToolCalls
            .All(call => string.Equals(call.Status, "ok", StringComparison.Ordinal))
            .Should().BeTrue();
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_CircuitBreakerBlock_StopsExecutionAndAudits()
    {
        var session = CreateSession();
        session.PermissionMode = PermissionMode.BypassAll;
        session.CurrentTurn = CreateTurn(session);
        var output = new SpyAgentOutput();
        var sink = new InMemoryAuditSink();
        var audit = new AuditService([sink]);
        var breaker = new CircuitBreaker(new ToolLoopDetector(repetitionWarningThreshold: 2, repetitionHardStopThreshold: 3));
        var filter = new ToolConfirmationFilter(session, auditService: audit, circuitBreaker: breaker, output: output);
        var nextCalls = 0;

        for (var i = 0; i < 3; i++)
        {
            await filter.OnAutoFunctionInvocationAsync(
                CreateContext(session.Kernel, "run_command", new KernelArguments { ["command"] = "build" }),
                ctx =>
                {
                    nextCalls++;
                    return CompleteInvocationAsync(ctx, $"call-{nextCalls}");
                });
        }

        nextCalls.Should().Be(2);
        session.CurrentTurn.ToolCalls.Should().HaveCount(3);
        session.CurrentTurn.ToolCalls.Last().Status.Should().Be("denied");
        session.CurrentTurn.ToolCalls.Last().Result.Should().StartWith("Blocked by circuit breaker:");
        var auditEvent = (await sink.QueryAsync(new AuditQuery { Limit = 10 })).Events[0];
        auditEvent.Detail.Should().Contain("status=circuit_breaker_block");
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_WriteFileSummary_RendersPathAndSizeAndRedactsAuditArgs()
    {
        var session = CreateSession();
        session.PermissionMode = PermissionMode.BypassAll;
        session.CurrentTurn = CreateTurn(session);
        var output = new SpyAgentOutput();
        var sink = new InMemoryAuditSink();
        var audit = new AuditService([sink]);
        var filter = new ToolConfirmationFilter(session, auditService: audit, output: output);
        var context = CreateContext(session.Kernel, "write_file", new KernelArguments
        {
            ["path"] = @"C:\temp\note.txt",
            ["content"] = "\n\nfirst line\nsecond line"
        });

        await filter.OnAutoFunctionInvocationAsync(context, ctx => CompleteInvocationAsync(ctx, "written"));

        output.RenderedToolCalls.Should().ContainSingle();
        var rendered = output.RenderedToolCalls[0];
        rendered.ToolName.Should().Be("write_file");
        rendered.Args.Should().Contain(@"path=C:\temp\note.txt [24 chars]");
        rendered.Args.Should().NotContain("first line");

        var auditEvent = (await sink.QueryAsync(new AuditQuery { Limit = 10 })).Events.Should().ContainSingle().Subject;
        auditEvent.Detail.Should().Contain(@"path=C:\temp\note.txt");
        auditEvent.Detail.Should().Contain("content=[REDACTED]");
        auditEvent.Detail.Should().NotContain("first line\nsecond line");
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_WorkflowCapture_UsesSafeWriteFileArgs()
    {
        var session = CreateSession();
        session.CurrentTurn = CreateTurn(session);
        session.ActiveWorkflowName = "recording";
        session.PermissionMode = PermissionMode.BypassAll;
        var output = new SpyAgentOutput();
        var filter = new ToolConfirmationFilter(session, output: output);
        var context = CreateContext(session.Kernel, "write_file", new KernelArguments
        {
            ["path"] = @"C:\temp\note.txt",
            ["content"] = "hello"
        });

        await filter.OnAutoFunctionInvocationAsync(context, ctx => CompleteInvocationAsync(ctx, "written"));

        session.CapturedWorkflowSteps.Should().ContainSingle()
            .Which.Should().Be(("write_file", @"path=C:\temp\note.txt [5 chars]"));
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_ConfirmOnceReuse_PublishesAllowWithoutPromptDecision()
    {
        var session = CreateSession();
        session.CurrentTurn = CreateTurn(session);
        session.WorkflowDeclinedThisTurn = true;
        session.EventBus = Substitute.For<JD.AI.Core.Events.IEventBus>();
        session.ConfirmedOnceTools.Add("write_file");
        var output = new SpyAgentOutput();
        var filter = new ToolConfirmationFilter(session, output: output);
        var context = CreateContext(session.Kernel, "write_file", new KernelArguments
        {
            ["path"] = @"C:\temp\beta.txt",
            ["content"] = "beta"
        });

        await filter.OnAutoFunctionInvocationAsync(context, ctx => CompleteInvocationAsync(ctx, "second"));

        output.ToolPromptCount.Should().Be(0);
        await session.EventBus.Received(1).PublishAsync(
            Arg.Is<JD.AI.Core.Events.GatewayEvent>(e =>
                string.Equals(e.EventType, "tool.audit", StringComparison.Ordinal) &&
                string.Equals(e.SourceId, session.SessionInfo!.Id, StringComparison.Ordinal) &&
                e.GetType() == typeof(JD.AI.Core.Events.ToolAuditEntry) &&
                string.Equals(((JD.AI.Core.Events.ToolAuditEntry)e).Decision, "Allowed", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_ConfirmOnceReuse_DoesNotTriggerWorkflowPrompt()
    {
        var session = CreateSession();
        session.CurrentTurn = CreateTurn(session);
        session.ConfirmedOnceTools.Add("write_file");
        var output = new SpyAgentOutput();
        output.WorkflowResponses.Enqueue(true);
        var filter = new ToolConfirmationFilter(session, output: output);
        var context = CreateContext(session.Kernel, "write_file", new KernelArguments
        {
            ["path"] = @"C:\temp\beta.txt",
            ["content"] = "beta"
        });

        await filter.OnAutoFunctionInvocationAsync(context, ctx => CompleteInvocationAsync(ctx, "ok"));

        output.WorkflowPromptCount.Should().Be(0);
        session.ActiveWorkflowName.Should().BeNull();
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_SkipPermissions_BypassesPromptInNormalMode()
    {
        var session = CreateSession();
        session.CurrentTurn = CreateTurn(session);
        session.SkipPermissions = true;
        var output = new SpyAgentOutput();
        var filter = new ToolConfirmationFilter(session, output: output);
        var context = CreateContext(session.Kernel, "run_command", new KernelArguments
        {
            ["command"] = "dotnet --info"
        });

        await filter.OnAutoFunctionInvocationAsync(context, ctx => CompleteInvocationAsync(ctx, "ok"));

        output.ToolPromptCount.Should().Be(0);
        output.WorkflowPromptCount.Should().Be(0);
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_NamedWorkflow_DoesNotCaptureSteps()
    {
        var session = CreateSession();
        session.CurrentTurn = CreateTurn(session);
        session.ActiveWorkflowName = "existing-workflow";
        session.PermissionMode = PermissionMode.BypassAll;
        var output = new SpyAgentOutput();
        var filter = new ToolConfirmationFilter(session, output: output);
        var context = CreateContext(session.Kernel, "write_file", new KernelArguments
        {
            ["path"] = @"C:\temp\note.txt",
            ["content"] = "hello"
        });

        await filter.OnAutoFunctionInvocationAsync(context, ctx => CompleteInvocationAsync(ctx, "written"));

        session.CapturedWorkflowSteps.Should().BeEmpty();
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_RecordingWorkflow_DoesNotCaptureFailedStep()
    {
        var session = CreateSession();
        session.CurrentTurn = CreateTurn(session);
        session.ActiveWorkflowName = "recording";
        session.PermissionMode = PermissionMode.BypassAll;
        var output = new SpyAgentOutput();
        var filter = new ToolConfirmationFilter(session, output: output);
        var context = CreateContext(session.Kernel, "write_file", new KernelArguments
        {
            ["path"] = @"C:\temp\note.txt",
            ["content"] = "hello"
        });

        var act = async () => await filter.OnAutoFunctionInvocationAsync(
            context,
            _ => throw new InvalidOperationException("boom"));

        await act.Should().ThrowAsync<InvalidOperationException>();
        session.CapturedWorkflowSteps.Should().BeEmpty();
    }

    private static AgentSession CreateSession()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model", "Test Model", "Test Provider");
        return new AgentSession(registry, kernel, model)
        {
            SessionInfo = new SessionInfo
            {
                ProjectPath = @"C:\git\JD.AI",
            },
        };
    }

    private static TurnRecord CreateTurn(AgentSession session) =>
        new()
        {
            SessionId = session.SessionInfo!.Id,
            TurnIndex = 0,
            Role = "assistant",
        };

    private static AutoFunctionInvocationContext CreateContext(
        Kernel kernel,
        string functionName,
        KernelArguments arguments)
    {
        var function = KernelFunctionFactory.CreateFromMethod(() => string.Empty, functionName, functionName);
        var context = new AutoFunctionInvocationContext(
            kernel,
            function,
            new FunctionResult(function, string.Empty, CultureInfo.InvariantCulture, EmptyMetadata),
            new ChatHistory(),
            new ChatMessageContent(AuthorRole.Assistant, "tool call"))
        {
            Arguments = arguments,
        };

        return context;
    }

    private static Task CompleteInvocationAsync(AutoFunctionInvocationContext context, string result)
    {
        context.Result = new FunctionResult(context.Function, result, CultureInfo.InvariantCulture, EmptyMetadata);
        return Task.CompletedTask;
    }

    private sealed class SpyAgentOutput : IAgentOutput
    {
        public Queue<bool> ToolResponses { get; } = new();
        public Queue<bool> WorkflowResponses { get; } = new();
        public List<string> InfoMessages { get; } = [];
        public List<string> Warnings { get; } = [];
        public List<(string ToolName, string? Args, string Result)> RenderedToolCalls { get; } = [];
        public int ToolPromptCount { get; private set; }
        public int WorkflowPromptCount { get; private set; }

        public void RenderInfo(string message) => InfoMessages.Add(message);
        public void RenderWarning(string message) => Warnings.Add(message);
        public void RenderError(string message) { }
        public void BeginThinking() { }
        public void WriteThinkingChunk(string text) { }
        public void EndThinking() { }
        public void BeginStreaming() { }
        public void WriteStreamingChunk(string text) { }
        public void EndStreaming() { }

        public void RenderToolCall(string toolName, string? args, string result) =>
            RenderedToolCalls.Add((toolName, args, result));

        public bool ConfirmToolCall(string toolName, string? args)
        {
            ToolPromptCount++;
            return ToolResponses.TryDequeue(out var result) ? result : true;
        }

        public bool ConfirmWorkflowPrompt(string triggeringTool)
        {
            WorkflowPromptCount++;
            return WorkflowResponses.TryDequeue(out var result) ? result : false;
        }
    }
}
