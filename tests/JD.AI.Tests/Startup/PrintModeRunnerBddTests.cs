using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using JD.AI.Core.Skills;
using JD.AI.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Startup;

[Feature("Print Mode Runner")]
public sealed class PrintModeRunnerBddTests : TinyBddXunitBase
{
    private static readonly Regex AnsiEscapeRegex = new(
        @"\x1B\[[0-9;?]*[ -/]*[@-~]",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public PrintModeRunnerBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Print mode emits structured JSON output for a single turn"), Fact]
    public async Task PrintMode_EmitsStructuredJson()
    {
        int exitCode = -1;
        string stdout = "";

        await Given("a print request with JSON output format", () =>
            {
                var ctx = CreateRunnerContext("assistant smoke response");
                var opts = new CliOptions
                {
                    PrintMode = true,
                    PrintQuery = "say hello",
                    OutputFormat = "json",
                };
                return (ctx.session, ctx.model, ctx.skills, ctx.governance, opts);
            })
            .When("running print mode", async Task (ctx) =>
            {
                (exitCode, stdout, _) = await CaptureConsoleAsync(() =>
                    PrintModeRunner.RunAsync(
                        ctx.opts,
                        ctx.session,
                        ctx.model,
                        ctx.skills,
                        ctx.governance));
            })
            .Then("exit code is zero and payload contains model/provider metadata", _ =>
            {
                exitCode.Should().Be(0);

                var payload = NormalizeJsonPayload(stdout);
                using var json = JsonDocument.Parse(payload);
                json.RootElement.GetProperty("result").GetString().Should().Be("assistant smoke response");
                json.RootElement.GetProperty("model").GetString().Should().Be("smoke-model");
                json.RootElement.GetProperty("provider").GetString().Should().Be("SmokeProvider");
                json.RootElement.GetProperty("turns").GetInt32().Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Print mode fails fast when no query or piped input is provided"), Fact]
    public async Task PrintMode_FailsWhenNoQueryIsProvided()
    {
        int exitCode = -1;
        string stderr = "";

        await Given("print mode options without query text", () =>
            {
                var ctx = CreateRunnerContext("unused");
                var opts = new CliOptions { PrintMode = true };
                return (ctx.session, ctx.model, ctx.skills, ctx.governance, opts);
            })
            .When("running print mode", async Task (ctx) =>
            {
                (exitCode, _, stderr) = await CaptureConsoleAsync(() =>
                    PrintModeRunner.RunAsync(
                        ctx.opts,
                        ctx.session,
                        ctx.model,
                        ctx.skills,
                        ctx.governance));
            })
            .Then("it returns exit code 1 and explains the missing query", _ =>
            {
                exitCode.Should().Be(1);
                stderr.Should().Contain("--print requires a query argument or piped input");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Print mode enforces max-turn guardrail"), Fact]
    public async Task PrintMode_EnforcesMaxTurns()
    {
        int exitCode = -1;
        string stderr = "";

        await Given("print mode options with max-turns set to zero", () =>
            {
                var ctx = CreateRunnerContext("unused");
                var opts = new CliOptions
                {
                    PrintMode = true,
                    PrintQuery = "trigger guardrail",
                    MaxTurns = 0,
                };
                return (ctx.session, ctx.model, ctx.skills, ctx.governance, opts);
            })
            .When("running print mode", async Task (ctx) =>
            {
                (exitCode, _, stderr) = await CaptureConsoleAsync(() =>
                    PrintModeRunner.RunAsync(
                        ctx.opts,
                        ctx.session,
                        ctx.model,
                        ctx.skills,
                        ctx.governance));
            })
            .Then("it aborts before running turns and returns an error", _ =>
            {
                exitCode.Should().Be(1);
                stderr.Should().Contain("max turns (0) exceeded");
                return true;
            })
            .AssertPassed();
    }

    private static (AgentSession session, ProviderModelInfo model, SkillLifecycleManager skills, GovernanceSetup governance)
        CreateRunnerContext(string assistantResponse)
    {
        var chat = Substitute.For<IChatCompletionService>();
        chat.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, assistantResponse),
            });

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chat);
        var kernel = builder.Build();

        var registry = Substitute.For<IProviderRegistry>();
        var model = new ProviderModelInfo("smoke-model", "Smoke Model", "SmokeProvider");
        var session = new AgentSession(registry, kernel, model)
        {
            NoSessionPersistence = true,
        };

        var skills = new SkillLifecycleManager([]);
        var governance = GovernanceInitializer.Initialize(
            Directory.GetCurrentDirectory(),
            session,
            kernel,
            new CliOptions { PrintMode = true },
            maxBudgetUsd: null);
        return (session, model, skills, governance);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> CaptureConsoleAsync(
        Func<Task<int>> action)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        try
        {
            var code = await action().ConfigureAwait(false);
            return (code, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static string NormalizeJsonPayload(string output)
    {
        var cleaned = AnsiEscapeRegex.Replace(output, string.Empty);

        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return cleaned[start..(end + 1)];
        }

        return cleaned;
    }
}
