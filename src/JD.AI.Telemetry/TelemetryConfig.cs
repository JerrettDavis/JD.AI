namespace JD.AI.Telemetry;

/// <summary>
/// Configuration for JD.AI telemetry (tracing and metrics).
/// Bind from <c>appsettings.json</c> under the <c>Gateway:Telemetry</c> key
/// or configure via environment variables (<c>OTEL_*</c> conventions).
/// </summary>
public sealed class TelemetryConfig
{
    /// <summary>
    /// Whether telemetry is enabled. Defaults to <c>true</c>.
    /// Set to <c>false</c> to disable all OTel instrumentation.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Logical service name reported in traces and metrics.
    /// Used when <c>OTEL_SERVICE_NAME</c> is not set; if the <c>OTEL_SERVICE_NAME</c>
    /// environment variable is set, it takes precedence over this value.
    /// Defaults to <c>"jdai"</c>.
    /// </summary>
    public string ServiceName { get; set; } = "jdai";

    /// <summary>
    /// Exporter type. Supported values:
    /// <list type="bullet">
    ///   <item><c>"console"</c> (default) — writes to stdout</item>
    ///   <item><c>"otlp"</c> — OTLP/gRPC (Jaeger, Grafana, etc.)</item>
    ///   <item><c>"zipkin"</c> — Zipkin HTTP exporter</item>
    /// </list>
    /// </summary>
    public string Exporter { get; set; } = "console";

    /// <summary>
    /// Exporter endpoint URI.
    /// For OTLP: <c>http://localhost:4317</c> (gRPC) or <c>http://localhost:4318</c> (HTTP/protobuf).
    /// For Zipkin: <c>http://localhost:9411/api/v2/spans</c>.
    /// </summary>
    public string? Endpoint { get; set; }
}
