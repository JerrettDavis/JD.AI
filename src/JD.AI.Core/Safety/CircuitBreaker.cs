namespace JD.AI.Core.Safety;

/// <summary>
/// Adaptive circuit breaker that wraps <see cref="ToolLoopDetector"/> to manage
/// open/closed/half-open state transitions for tool invocations.
/// <para>
/// State machine:
/// <c>Closed</c> → (hard-stop detected) → <c>Open</c> → (cooldown expires) →
/// <c>HalfOpen</c> → (probe succeeds) → <c>Closed</c>.
/// </para>
/// </summary>
public sealed class CircuitBreaker
{
    private readonly Lock _lock = new();
    private readonly ToolLoopDetector _detector;

    private CircuitState _state = CircuitState.Closed;
    private DateTimeOffset _openedAt;
    private int _totalTrips;

    /// <summary>How long the circuit stays open before transitioning to half-open.</summary>
    public TimeSpan CooldownPeriod { get; }

    /// <summary>Maximum calls allowed in half-open state to probe recovery.</summary>
    public int HalfOpenProbeLimit { get; }
    private int _halfOpenProbeCount;

    /// <summary>When true, the circuit breaker cannot be disabled even by user override.</summary>
    public bool HardenedMode { get; }

    public CircuitBreaker(
        ToolLoopDetector detector,
        TimeSpan? cooldownPeriod = null,
        int halfOpenProbeLimit = 2,
        bool hardenedMode = false)
    {
        ArgumentNullException.ThrowIfNull(detector);
        _detector = detector;
        CooldownPeriod = cooldownPeriod ?? TimeSpan.FromSeconds(30);
        HalfOpenProbeLimit = halfOpenProbeLimit;
        HardenedMode = hardenedMode;
    }

    /// <summary>Current circuit state.</summary>
    public CircuitState State
    {
        get { lock (_lock) { return _state; } }
    }

    /// <summary>Total number of times the circuit has tripped since creation.</summary>
    public int TotalTrips
    {
        get { lock (_lock) { return _totalTrips; } }
    }

    /// <summary>The underlying loop detector.</summary>
    public ToolLoopDetector Detector => _detector;

    /// <summary>
    /// Evaluates whether the next tool call should proceed.
    /// Returns the decision and any loop detection diagnostics.
    /// </summary>
    public CircuitBreakerResult Evaluate(
        string toolName,
        string? argsFingerprint = null,
        string? outputFingerprint = null,
        string? agentId = null)
    {
        lock (_lock)
        {
            // Check circuit state first
            switch (_state)
            {
                case CircuitState.Open:
                    if (DateTimeOffset.UtcNow - _openedAt >= CooldownPeriod)
                    {
                        _state = CircuitState.HalfOpen;
                        _halfOpenProbeCount = 0;
                    }
                    else
                    {
                        return new CircuitBreakerResult(
                            CircuitAction.Block,
                            _state,
                            "Circuit breaker is open — tool invocations are paused. " +
                            $"Cooldown remaining: {CooldownPeriod - (DateTimeOffset.UtcNow - _openedAt):mm\\:ss}.");
                    }
                    break;

                case CircuitState.HalfOpen:
                    if (_halfOpenProbeCount >= HalfOpenProbeLimit)
                    {
                        // Probes succeeded, close circuit
                        _state = CircuitState.Closed;
                        _detector.Reset();
                    }
                    break;
            }

            // Run loop detection
            var detection = _detector.RecordAndEvaluate(toolName, argsFingerprint, outputFingerprint, agentId);

            switch (detection.Decision)
            {
                case LoopDecision.HardStop:
                    Trip();
                    return new CircuitBreakerResult(
                        CircuitAction.Block,
                        _state,
                        detection.Message,
                        detection);

                case LoopDecision.Warning:
                    if (_state == CircuitState.HalfOpen)
                    {
                        // Warning during half-open → trip again
                        Trip();
                        return new CircuitBreakerResult(
                            CircuitAction.Block,
                            _state,
                            $"Loop warning during half-open probe — circuit re-opened. {detection.Message}",
                            detection);
                    }
                    return new CircuitBreakerResult(
                        CircuitAction.Warn,
                        _state,
                        detection.Message,
                        detection);

                default:
                    if (_state == CircuitState.HalfOpen)
                    {
                        _halfOpenProbeCount++;
                    }
                    return new CircuitBreakerResult(CircuitAction.Allow, _state);
            }
        }
    }

    /// <summary>Manually resets the circuit breaker (only allowed if not in hardened mode).</summary>
    public bool TryReset()
    {
        if (HardenedMode) return false;

        lock (_lock)
        {
            _state = CircuitState.Closed;
            _detector.Reset();
            _halfOpenProbeCount = 0;
            return true;
        }
    }

    private void Trip()
    {
        _state = CircuitState.Open;
        _openedAt = DateTimeOffset.UtcNow;
        _totalTrips++;
    }
}

/// <summary>Circuit breaker state machine states.</summary>
public enum CircuitState
{
    /// <summary>Normal operation — all calls pass through detection.</summary>
    Closed,

    /// <summary>Loop detected — calls are blocked until cooldown expires.</summary>
    Open,

    /// <summary>Cooldown expired — limited probe calls allowed to test recovery.</summary>
    HalfOpen,
}

/// <summary>What the circuit breaker recommends for the current invocation.</summary>
public enum CircuitAction
{
    /// <summary>Proceed with the tool call.</summary>
    Allow,

    /// <summary>Proceed but emit a diagnostic warning.</summary>
    Warn,

    /// <summary>Block the tool call — circuit is open or hard-stop detected.</summary>
    Block,
}

/// <summary>Result of a circuit breaker evaluation.</summary>
public sealed record CircuitBreakerResult(
    CircuitAction Action,
    CircuitState CircuitState,
    string? Message = null,
    LoopDetectionResult? LoopDetection = null);
