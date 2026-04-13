using FluentAssertions;
using JD.AI.Agent;
using JD.AI.Core.Agents;

namespace JD.AI.Tests;

public sealed class TurnInputMonitorTests
{
    [Fact]
    public void Token_IsNotCancelledInitially()
    {
        using var cts = new CancellationTokenSource();
        using var monitor = new TurnInputMonitor(cts.Token);

        monitor.Token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Token_CancelledWhenAppTokenCancelled()
    {
        using var cts = new CancellationTokenSource();
        using var monitor = new TurnInputMonitor(cts.Token);

        cts.Cancel();

        monitor.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Token_IsLinkedToAppToken()
    {
        using var cts = new CancellationTokenSource();
        using var monitor = new TurnInputMonitor(cts.Token);

        // Linked token should be distinct from the source
        (monitor.Token == cts.Token).Should().BeFalse();
    }

    [Fact]
    public void Dispose_CancelsToken()
    {
        using var cts = new CancellationTokenSource();
        var monitor = new TurnInputMonitor(cts.Token);
        var token = monitor.Token;

        monitor.Dispose();

        token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        using var cts = new CancellationTokenSource();
        var monitor = new TurnInputMonitor(cts.Token);

        var ex = Record.Exception(() => monitor.Dispose());

        ex.Should().BeNull();
    }

    [Fact]
    public async Task MonitorLoop_ExitsGracefullyWhenDisposed()
    {
        using var cts = new CancellationTokenSource();
        var monitor = new TurnInputMonitor(cts.Token);

        await Task.Delay(100);

        // Should not hang
        monitor.Dispose();
    }

    [Fact]
    public void SteeringMessage_IsNullInitially()
    {
        using var cts = new CancellationTokenSource();
        using var monitor = new TurnInputMonitor(cts.Token);

        monitor.SteeringMessage.Should().BeNull();
    }

    [Fact]
    public void MultipleDisposeCalls_DoNotThrow()
    {
        using var cts = new CancellationTokenSource();
        var monitor = new TurnInputMonitor(cts.Token);

        monitor.Dispose();
        var ex = Record.Exception(() => monitor.Dispose());
        (ex is null or ObjectDisposedException).Should().BeTrue();
    }

    [Fact]
    public void CustomDoubleTapWindow_IsAccepted()
    {
        using var cts = new CancellationTokenSource();
        using var monitor = new TurnInputMonitor(
            cts.Token,
            doubleTapWindow: TimeSpan.FromMilliseconds(500));

        monitor.Token.IsCancellationRequested.Should().BeFalse();
    }

    // ── Extended Tests ─────────────────────────────────────────────────

    [Fact]
    public void CancelTurn_GivenActiveTurn_WhenCalled_ThenTokenBecomesCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var monitor = new TurnInputMonitor(cts.Token);

        monitor.Token.IsCancellationRequested.Should().BeFalse();

        // Act
        monitor.CancelTurn();

        // Assert
        monitor.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void CancelTurn_GivenAlreadyCancelledToken_WhenCalledAgain_ThenDoesNotThrow()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var monitor = new TurnInputMonitor(cts.Token);

        monitor.CancelTurn();
        monitor.Token.IsCancellationRequested.Should().BeTrue();

        // Act & Assert
        var ex = Record.Exception(() => monitor.CancelTurn());
        ex.Should().BeNull("CancelTurn should be idempotent");
    }

    [Fact]
    public void CancelTurn_GivenDisposedMonitor_WhenCalled_ThenDoesNotThrow()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var monitor = new TurnInputMonitor(cts.Token);
        monitor.Dispose();

        // Act & Assert
        var ex = Record.Exception(() => monitor.CancelTurn());
        ex.Should().BeNull("CancelTurn should handle disposed state gracefully");
    }

    [Fact]
    public void Token_GivenAppTokenAlreadyCancelled_WhenConstructed_ThenTokenIsCancelledImmediately()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        using var monitor = new TurnInputMonitor(cts.Token);

        // Assert
        monitor.Token.IsCancellationRequested.Should().BeTrue(
            "linked token should be cancelled immediately if app token is already cancelled");
    }

    [Fact]
    public void Dispose_GivenMonitorLoopRunning_WhenDisposed_ThenCompletesWithinTimeout()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var monitor = new TurnInputMonitor(cts.Token);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        monitor.Dispose();
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500,
            "Dispose should complete within timeout (monitor uses 300ms wait)");
    }

    [Fact]
    public void SteeringMessage_GivenNoInput_WhenDisposed_ThenRemainsNull()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var monitor = new TurnInputMonitor(cts.Token);

        monitor.SteeringMessage.Should().BeNull();

        // Act
        monitor.Dispose();

        // Assert
        monitor.SteeringMessage.Should().BeNull(
            "steering message should remain null if no input was received");
    }
}
