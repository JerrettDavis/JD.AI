using FluentAssertions;
using JD.AI.Core.Safety;

namespace JD.AI.Tests.Safety;

public sealed class CircuitBreakerTests
{
    // ── Basic state transitions ──────────────────────────

    [Fact]
    public void InitialState_IsClosed()
    {
        var cb = CreateBreaker();

        cb.State.Should().Be(CircuitState.Closed);
        cb.TotalTrips.Should().Be(0);
    }

    [Fact]
    public void NormalCall_ReturnsAllow()
    {
        var cb = CreateBreaker();

        var result = cb.Evaluate("read_file", "path=foo.txt");

        result.Action.Should().Be(CircuitAction.Allow);
        result.CircuitState.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void WarningThreshold_ReturnsWarn()
    {
        var cb = CreateBreaker(warningThreshold: 2);

        cb.Evaluate("read_file", "x");
        var result = cb.Evaluate("read_file", "x");

        result.Action.Should().Be(CircuitAction.Warn);
    }

    [Fact]
    public void HardStopThreshold_TripsCircuit()
    {
        var cb = CreateBreaker(warningThreshold: 2, hardStopThreshold: 3);

        cb.Evaluate("read_file", "x");
        cb.Evaluate("read_file", "x");
        var result = cb.Evaluate("read_file", "x");

        result.Action.Should().Be(CircuitAction.Block);
        cb.State.Should().Be(CircuitState.Open);
        cb.TotalTrips.Should().Be(1);
    }

    [Fact]
    public void OpenCircuit_BlocksSubsequentCalls()
    {
        var cb = CreateBreaker(hardStopThreshold: 2, cooldownSeconds: 300);

        cb.Evaluate("read_file", "x");
        cb.Evaluate("read_file", "x"); // Trips

        var result = cb.Evaluate("write_file", "y"); // Different tool, still blocked

        result.Action.Should().Be(CircuitAction.Block);
        result.Message.Should().Contain("Circuit breaker is open");
    }

    // ── Half-open recovery ───────────────────────────────

    [Fact]
    public void AfterCooldown_TransitionsToHalfOpen()
    {
        var cb = CreateBreaker(
            hardStopThreshold: 2,
            cooldownSeconds: 0); // Instant cooldown for test

        cb.Evaluate("read_file", "x");
        cb.Evaluate("read_file", "x"); // Trips

        // Cooldown is 0 seconds, so next call should transition to half-open
        var result = cb.Evaluate("read_file", "y"); // Different args, should pass

        // Should be allowed (half-open probe)
        result.Action.Should().Be(CircuitAction.Allow);
    }

    [Fact]
    public void HalfOpenProbe_SucceedsAndCloses()
    {
        var cb = CreateBreaker(
            hardStopThreshold: 2,
            cooldownSeconds: 0,
            halfOpenProbeLimit: 2);

        cb.Evaluate("read_file", "x");
        cb.Evaluate("read_file", "x"); // Trips

        // Probes (cooldown = 0)
        cb.Evaluate("write_file", "a"); // Probe 1
        cb.Evaluate("grep", "b");       // Probe 2

        // Next call: probes succeeded, circuit should close
        var result = cb.Evaluate("read_file", "c");
        result.Action.Should().Be(CircuitAction.Allow);
        cb.State.Should().Be(CircuitState.Closed);
    }

    // ── Hardened mode ────────────────────────────────────

    [Fact]
    public void HardenedMode_CannotReset()
    {
        var cb = CreateBreaker(hardStopThreshold: 2, hardenedMode: true);

        cb.Evaluate("read_file", "x");
        cb.Evaluate("read_file", "x"); // Trips

        var resetResult = cb.TryReset();

        resetResult.Should().BeFalse();
        cb.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public void NonHardenedMode_CanReset()
    {
        var cb = CreateBreaker(hardStopThreshold: 2);

        cb.Evaluate("read_file", "x");
        cb.Evaluate("read_file", "x"); // Trips

        var resetResult = cb.TryReset();

        resetResult.Should().BeTrue();
        cb.State.Should().Be(CircuitState.Closed);
    }

    // ── Multiple trips ───────────────────────────────────

    [Fact]
    public void MultipleTrips_CountedCorrectly()
    {
        var cb = CreateBreaker(
            hardStopThreshold: 2,
            cooldownSeconds: 0,
            halfOpenProbeLimit: 1);

        // First trip
        cb.Evaluate("read_file", "x");
        cb.Evaluate("read_file", "x");

        // Recover
        cb.Evaluate("write_file", "a"); // Probe in half-open
        cb.Evaluate("grep", "b");       // Closes circuit

        // Second trip
        cb.Evaluate("write_file", "y");
        cb.Evaluate("write_file", "y");

        cb.TotalTrips.Should().Be(2);
    }

    // ── Ping-pong trips circuit ──────────────────────────

    [Fact]
    public void PingPongLoop_TripsCircuit()
    {
        var cb = CreateBreaker(pingPongThreshold: 4);

        cb.Evaluate("read_file", "a");
        cb.Evaluate("write_file", "b");
        cb.Evaluate("read_file", "c");
        cb.Evaluate("write_file", "d");
        cb.Evaluate("read_file", "e");
        cb.Evaluate("write_file", "f");
        cb.Evaluate("read_file", "g");
        var result = cb.Evaluate("write_file", "h");

        result.Action.Should().Be(CircuitAction.Block);
        cb.State.Should().Be(CircuitState.Open);
    }

    // ── Helper ───────────────────────────────────────────

    private static CircuitBreaker CreateBreaker(
        int warningThreshold = 3,
        int hardStopThreshold = 5,
        int pingPongThreshold = 4,
        int windowSize = 50,
        int cooldownSeconds = 30,
        int halfOpenProbeLimit = 2,
        bool hardenedMode = false)
    {
        var detector = new ToolLoopDetector(
            windowSize: windowSize,
            repetitionWarningThreshold: warningThreshold,
            repetitionHardStopThreshold: hardStopThreshold,
            pingPongThreshold: pingPongThreshold);

        return new CircuitBreaker(
            detector,
            cooldownPeriod: TimeSpan.FromSeconds(cooldownSeconds),
            halfOpenProbeLimit: halfOpenProbeLimit,
            hardenedMode: hardenedMode);
    }
}
