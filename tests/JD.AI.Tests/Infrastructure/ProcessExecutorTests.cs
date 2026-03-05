// Licensed under the MIT License.

using FluentAssertions;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Tests.Infrastructure;

public sealed class ProcessExecutorTests
{
    [Fact]
    public async Task RunAsync_SuccessfulCommand_ReturnsZeroExitCode()
    {
        var result = await ProcessExecutor.RunAsync("dotnet", "--version");

        result.ExitCode.Should().Be(0);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RunAsync_FailingCommand_ReturnsNonZeroExitCode()
    {
        var result = await ProcessExecutor.RunAsync("dotnet", "nonexistent-command-xyz");

        result.Success.Should().BeFalse();
        result.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task RunAsync_WithTimeout_ReturnsTimeoutError()
    {
        // Use a command that will hang
        var cmd = OperatingSystem.IsWindows() ? "timeout" : "sleep";
        var args = OperatingSystem.IsWindows() ? "/t 60 /nobreak" : "60";

        var result = await ProcessExecutor.RunAsync(cmd, args, timeout: TimeSpan.FromMilliseconds(500));

        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(-1);
        result.StandardError.Should().Contain("timed out");
    }

    [Fact]
    public async Task RunAsync_WithCancellation_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => ProcessExecutor.RunAsync("dotnet", "--version", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_CapturesStderr()
    {
        // dotnet with invalid arg should produce stderr
        var result = await ProcessExecutor.RunAsync("dotnet", "nonexistent-command-xyz");

        result.StandardError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RunAsync_WithStandardInput_SendsInput()
    {
        // Use dotnet script or a simple echo-back approach
        var cmd = OperatingSystem.IsWindows() ? "findstr" : "cat";
        var args = OperatingSystem.IsWindows() ? ".*" : string.Empty;

        var result = await ProcessExecutor.RunAsync(cmd, args, standardInput: "hello world\n");

        result.StandardOutput.Should().Contain("hello world");
    }

    [Fact]
    public async Task RunForOutputAsync_Success_ReturnsStdout()
    {
        var output = await ProcessExecutor.RunForOutputAsync("dotnet", "--version");

        output.Should().NotBeNullOrWhiteSpace();
        output.Should().NotStartWith("Error");
    }

    [Fact]
    public async Task RunForOutputAsync_Failure_ReturnsErrorString()
    {
        var output = await ProcessExecutor.RunForOutputAsync("dotnet", "nonexistent-command-xyz");

        output.Should().StartWith("Error");
    }

    [Fact]
    public async Task IsAvailableAsync_DotnetExists()
    {
        var available = await ProcessExecutor.IsAvailableAsync("dotnet");

        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_FakeToolDoesNotExist()
    {
        var available = await ProcessExecutor.IsAvailableAsync("nonexistent-tool-xyz-12345");

        available.Should().BeFalse();
    }

    [Fact]
    public void ProcessResult_Success_WhenExitCodeZero()
    {
        var result = new ProcessResult(0, "output", "");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void ProcessResult_NotSuccess_WhenExitCodeNonZero()
    {
        var result = new ProcessResult(1, "", "error");
        result.Success.Should().BeFalse();
    }
}
