using JD.AI.Tui.Agent;

namespace JD.AI.Tui.Tests;

public sealed class EscapeCancellationTests
{
    [Fact]
    public void Token_IsNotCancelledInitially()
    {
        using var cts = new CancellationTokenSource();
        using var esc = new EscapeCancellation(cts.Token);

        Assert.False(esc.Token.IsCancellationRequested);
    }

    [Fact]
    public void Token_CancelledWhenAppTokenCancelled()
    {
        using var cts = new CancellationTokenSource();
        using var esc = new EscapeCancellation(cts.Token);

        cts.Cancel();

        // The linked token should be cancelled
        Assert.True(esc.Token.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        using var cts = new CancellationTokenSource();
        var esc = new EscapeCancellation(cts.Token);

        // Should complete without exception
        esc.Dispose();
    }

    [Fact]
    public void Dispose_CancelsToken()
    {
        using var cts = new CancellationTokenSource();
        var esc = new EscapeCancellation(cts.Token);
        var token = esc.Token;

        esc.Dispose();

        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void Token_IsLinkedToAppToken()
    {
        using var cts = new CancellationTokenSource();
        using var esc = new EscapeCancellation(cts.Token);

        // Token should not be identical to the app token (it's a linked one)
        Assert.False(esc.Token == cts.Token);
    }

    [Fact]
    public async Task MonitorLoop_ExitsGracefullyWhenDisposed()
    {
        using var cts = new CancellationTokenSource();
        var esc = new EscapeCancellation(cts.Token);

        // Give the monitor loop a moment to start
        await Task.Delay(100);

        // Dispose should complete without hanging
        esc.Dispose();
    }

    [Fact]
    public void MultipleDisposeCalls_DoNotThrow()
    {
        using var cts = new CancellationTokenSource();
        var esc = new EscapeCancellation(cts.Token);

        esc.Dispose();
        // Second dispose should be safe (CTS may throw but we don't want crashes)
        var ex = Record.Exception(() => esc.Dispose());
        // ObjectDisposedException is acceptable but no other exception
        Assert.True(ex is null or ObjectDisposedException);
    }
}
