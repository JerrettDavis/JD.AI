namespace JD.AI.Telemetry;

/// <summary>
/// Configuration for JD.AI telemetry (tracing and metrics).
/// Bind from <c>appsettings.json</c> under the <c>Gateway:Telemetry</c> key.
/// The <c>OTEL_SERVICE_NAME</c> and <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> environment
/// variables are also honored (see individual property docs).
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
    ///   <item><c>"otlp"</c> — OTLP exporter (Grafana, Jaeger via OTLP receiver, etc.); see <see cref="OtlpProtocol"/> and <see cref="Endpoint"/></item>
    ///   <item><c>"zipkin"</c> — Zipkin HTTP exporter (traces only; metrics fall back to console)</item>
    /// </list>
    /// Setting <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> automatically activates <c>"otlp"</c>.
    /// </summary>
    public string Exporter { get; set; } = "console";

    /// <summary>
    /// OTLP transport protocol. Only used when <see cref="Exporter"/> is <c>"otlp"</c>.
    /// <list type="bullet">
    ///   <item><c>"grpc"</c> (default) — gRPC transport, default port 4317</item>
    ///   <item><c>"http"</c> — HTTP/protobuf transport, default port 4318</item>
    /// </list>
    /// </summary>
    public string OtlpProtocol { get; set; } = "grpc";

    /// <summary>
    /// Exporter endpoint URI.
    /// For OTLP/gRPC: <c>http://localhost:4317</c>.
    /// For OTLP/HTTP: <c>http://localhost:4318</c>.
    /// For Zipkin: <c>http://localhost:9411/api/v2/spans</c>.
    /// Also populated automatically from <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>.
    /// </summary>
    public string? Endpoint { get; set; }
}
