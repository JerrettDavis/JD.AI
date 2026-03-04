using JD.AI.Core.Config;
using JD.AI.Core.Providers;
using JD.AI.Telemetry.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace JD.AI.Telemetry.Extensions;

/// <summary>
/// Extension methods for registering JD.AI OpenTelemetry tracing, metrics,
/// and health checks with the dependency-injection container.
/// </summary>
public static class TelemetryServiceExtensions
{
    /// <summary>
    /// Registers OpenTelemetry tracing and metrics according to <paramref name="config"/>.
    /// When <see cref="TelemetryConfig.Enabled"/> is <c>false</c>, this is a no-op.
    /// </summary>
    public static IServiceCollection AddJdAiTelemetry(
        this IServiceCollection services,
        TelemetryConfig config)
    {
        if (!config.Enabled)
            return services;

        // Allow OTEL_SERVICE_NAME env var to override config
        var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
            ?? config.ServiceName;

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName);

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(ActivitySources.AllSourceNames)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                ConfigureTraceExporter(tracing, config);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(Meters.AllMeterNames)
                    .AddAspNetCoreInstrumentation();

                ConfigureMetricsExporter(metrics, config);
            });

        return services;
    }

    /// <summary>
    /// Registers the JD.AI health checks (provider connectivity, session store,
    /// disk space, and memory) with the ASP.NET Core health check infrastructure.
    /// Returns the <see cref="IHealthChecksBuilder"/> so callers can chain additional checks.
    /// </summary>
    public static IHealthChecksBuilder AddJdAiHealthChecks(
        this IServiceCollection services)
    {
        return services.AddHealthChecks()
            .AddCheck<ProviderHealthCheck>("providers", tags: ["providers"])
            .AddCheck<MemoryHealthCheck>("memory", tags: ["memory"])
            .Add(new HealthCheckRegistration(
                "session_store",
                sp => new SessionStoreHealthCheck(DataDirectories.SessionsDb),
                failureStatus: HealthStatus.Unhealthy,
                tags: ["storage"]))
            .Add(new HealthCheckRegistration(
                "disk_space",
                sp => new DiskSpaceHealthCheck(DataDirectories.Root),
                failureStatus: HealthStatus.Degraded,
                tags: ["storage"]));
    }

    private static void ConfigureTraceExporter(TracerProviderBuilder tracing, TelemetryConfig config)
    {
        var exporter = ResolveExporter(config);
        var endpoint = ResolveEndpoint(config);

        switch (exporter)
        {
            case "otlp":
                tracing.AddOtlpExporter(o =>
                {
                    if (!string.IsNullOrEmpty(endpoint))
                        o.Endpoint = new Uri(endpoint);
                });
                break;

            case "zipkin":
                tracing.AddZipkinExporter(o =>
                {
                    if (!string.IsNullOrEmpty(endpoint))
                        o.Endpoint = new Uri(endpoint);
                });
                break;

            default: // "console"
                tracing.AddConsoleExporter();
                break;
        }
    }

    private static void ConfigureMetricsExporter(MeterProviderBuilder metrics, TelemetryConfig config)
    {
        var exporter = ResolveExporter(config);
        var endpoint = ResolveEndpoint(config);

        switch (exporter)
        {
            case "otlp":
                metrics.AddOtlpExporter(o =>
                {
                    if (!string.IsNullOrEmpty(endpoint))
                        o.Endpoint = new Uri(endpoint);
                });
                break;

            default: // "console" and all others
                metrics.AddConsoleExporter();
                break;
        }
    }

    /// <summary>
    /// Resolves the exporter type, allowing the standard <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>
    /// env var to implicitly activate OTLP mode.
    /// </summary>
    private static string ResolveExporter(TelemetryConfig config)
    {
        // Standard OTel env var presence implies OTLP
        if (Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") is { Length: > 0 })
            return "otlp";

        return config.Exporter.ToLowerInvariant();
    }

    private static string? ResolveEndpoint(TelemetryConfig config) =>
        Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
        ?? config.Endpoint;
}
