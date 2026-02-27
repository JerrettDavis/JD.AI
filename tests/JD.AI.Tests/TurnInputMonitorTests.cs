using JD.AI.Tui.Agent;

namespace JD.AI.Tui.Tests;

public sealed class TurnInputMonitorTests
{
    [Fact]
    public void Token_IsNotCancelledInitially()
    {
        using var cts = new CancellationTokenSource();
        using var monitor = new TurnInputMonitor(cts.Token);

        Assert.False(monitor.Token.IsCancellationRequested);
    }

    [Fact]
    public void Token_CancelledWhenAppTokenCancelled()
    {
        using var cts = new CancellationTokenSource();
        using var monitor = new TurnInputMonitor(cts.Token);

        cts.Cancel();

        Assert.True(monitor.Token.IsCancellationRequested);
    }

    [Fact]
    public void Token_IsLinkedToAppToken()
    {
        using var cts = new CancellationTokenSource();
        using var monitor = new TurnInputMonitor(cts.Token);

        // Linked token should be distinct from the source
        Assert.False(monitor.Token == cts.Token);
    }

    [Fact]
    public void Dispose_CancelsToken()
    {
        using var cts = new CancellationTokenSource();
        var monitor = new TurnInputMonitor(cts.Token);
        var token = monitor.Token;

        monitor.Dispose();

        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        using var cts = new CancellationTokenSource();
        var monitor = new TurnInputMonitor(cts.Token);

        var ex = Record.Exception(() => monitor.Dispose());

        Assert.Null(ex);
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

        Assert.Null(monitor.SteeringMessage);
    }

    [Fact]
    public void MultipleDisposeCalls_DoNotThrow()
    {
        using var cts = new CancellationTokenSource();
        var monitor = new TurnInputMonitor(cts.Token);

        monitor.Dispose();
        var ex = Record.Exception(() => monitor.Dispose());
        Assert.True(ex is null or ObjectDisposedException);
    }

    [Fact]
    public void CustomDoubleTapWindow_IsAccepted()
    {
        using var cts = new CancellationTokenSource();
        using var monitor = new TurnInputMonitor(
            cts.Token,
            doubleTapWindow: TimeSpan.FromMilliseconds(500));

        Assert.False(monitor.Token.IsCancellationRequested);
    }
}
