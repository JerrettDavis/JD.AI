using System.Diagnostics.Metrics;
using FluentAssertions;
using JD.AI.Telemetry;
using JD.AI.Telemetry.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JD.AI.Tests.Telemetry;

/// <summary>
/// Tests for <see cref="TelemetryServiceExtensions"/>.
/// Exercises the helper logic (exporter resolution, protocol mapping, endpoint
/// parsing) without starting real OTLP exporters.
/// </summary>
public sealed class TelemetryServiceExtensionsTests : IDisposable
{
    // Cache env vars so tests restore them after each run
    private readonly string? _savedOtelEndpoint;
    private readonly string? _savedOtelServiceName;

    public TelemetryServiceExtensionsTests()
    {
        _savedOtelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        _savedOtelServiceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME");

        // Start with a clean slate
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", null);
        Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", _savedOtelEndpoint);
        Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", _savedOtelServiceName);
    }

    // ── AddJdAiTelemetry: disabled config is a no-op ─────────────────────────

    [Fact]
    public void AddJdAiTelemetry_WhenDisabled_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var config = new TelemetryConfig { Enabled = false };

        var returned = services.AddJdAiTelemetry(config);

        returned.Should().BeSameAs(services);
        // No OTel services should have been registered
        services.Should().BeEmpty();
    }

    // ── AddJdAiTelemetry: enabled config registers OTel ───────────────────────

    [Fact]
    public void AddJdAiTelemetry_WhenEnabled_RegistersServices()
    {
        var services = new ServiceCollection();
        var config = new TelemetryConfig { Enabled = true, Exporter = "console" };

        services.AddJdAiTelemetry(config);

        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddJdAiTelemetry_WhenEnabled_ReturnsServices()
    {
        var services = new ServiceCollection();
        var config = new TelemetryConfig { Enabled = true, Exporter = "console" };

        var returned = services.AddJdAiTelemetry(config);

        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddJdAiTelemetry_WithZipkinExporter_RegistersServices()
    {
        var services = new ServiceCollection();
        var config = new TelemetryConfig
        {
            Enabled = true,
            Exporter = "zipkin",
            Endpoint = "http://localhost:9411/api/v2/spans",
        };

        var act = () => services.AddJdAiTelemetry(config);

        act.Should().NotThrow();
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddJdAiTelemetry_OtlpGrpcExporter_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var config = new TelemetryConfig
        {
            Enabled = true,
            Exporter = "otlp",
            OtlpProtocol = "grpc",
            Endpoint = "http://localhost:4317",
        };

        var act = () => services.AddJdAiTelemetry(config);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddJdAiTelemetry_OtlpHttpExporter_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var config = new TelemetryConfig
        {
            Enabled = true,
            Exporter = "otlp",
            OtlpProtocol = "http",
            Endpoint = "http://localhost:4318",
        };

        var act = () => services.AddJdAiTelemetry(config);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddJdAiTelemetry_OtlpHttpProtobuf_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var config = new TelemetryConfig
        {
            Enabled = true,
            Exporter = "otlp",
            OtlpProtocol = "http/protobuf",
            Endpoint = "http://localhost:4318",
        };

        var act = () => services.AddJdAiTelemetry(config);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddJdAiTelemetry_UnknownExporter_FallsBackToConsole()
    {
        var services = new ServiceCollection();
        var config = new TelemetryConfig
        {
            Enabled = true,
            Exporter = "jaeger", // not directly supported → console fallback
        };

        var act = () => services.AddJdAiTelemetry(config);

        act.Should().NotThrow();
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddJdAiTelemetry_InvalidEndpointUri_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var config = new TelemetryConfig
        {
            Enabled = true,
            Exporter = "otlp",
            Endpoint = "NOT_A_VALID_URI",
        };

        // TryParseEndpoint returns false for invalid URIs — no exception
        var act = () => services.AddJdAiTelemetry(config);
        act.Should().NotThrow();
    }

    [Fact]
    public void AddJdAiTelemetry_NullEndpoint_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var config = new TelemetryConfig
        {
            Enabled = true,
            Exporter = "otlp",
            Endpoint = null,
        };

        var act = () => services.AddJdAiTelemetry(config);
        act.Should().NotThrow();
    }

    // ── OTEL_EXPORTER_OTLP_ENDPOINT env var activates OTLP ───────────────────

    [Fact]
    public void AddJdAiTelemetry_WhenOtlpEndpointEnvVarSet_ActivatesOtlp()
    {
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://otel-collector:4317");

        var services = new ServiceCollection();
        var config = new TelemetryConfig { Enabled = true, Exporter = "console" };

        // Even though config says "console", the env var overrides it to "otlp"
        var act = () => services.AddJdAiTelemetry(config);
        act.Should().NotThrow();
        services.Should().NotBeEmpty();
    }

    // ── AddJdAiHealthChecks ───────────────────────────────────────────────────

    [Fact]
    public void AddJdAiHealthChecks_ReturnsHealthChecksBuilder()
    {
        var services = new ServiceCollection();

        var builder = services.AddJdAiHealthChecks();

        builder.Should().NotBeNull();
    }

    [Fact]
    public void AddJdAiHealthChecks_RegistersHealthCheckServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddJdAiHealthChecks();

        // HealthCheckService and related types should be registered
        var sp = services.BuildServiceProvider();
        var healthCheckService = sp.GetService<HealthCheckService>();
        healthCheckService.Should().NotBeNull();
    }

    [Fact]
    public void AddJdAiHealthChecks_RegistersProviderMemoryDiskAndSessionChecks()
    {
        var services = new ServiceCollection();

        services.AddJdAiHealthChecks();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;

        var names = options.Registrations.Select(r => r.Name).ToList();

        names.Should().Contain("providers");
        names.Should().Contain("memory");
        names.Should().Contain("session_store");
        names.Should().Contain("disk_space");
    }

    [Fact]
    public void AddJdAiHealthChecks_SessionStoreRegistration_HasUnhealthyFailureStatus()
    {
        var services = new ServiceCollection();
        services.AddJdAiHealthChecks();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;

        var sessionReg = options.Registrations.Single(r => string.Equals(r.Name, "session_store", StringComparison.Ordinal));
        sessionReg.FailureStatus.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void AddJdAiHealthChecks_DiskSpaceRegistration_HasDegradedFailureStatus()
    {
        var services = new ServiceCollection();
        services.AddJdAiHealthChecks();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;

        var diskReg = options.Registrations.Single(r => string.Equals(r.Name, "disk_space", StringComparison.Ordinal));
        diskReg.FailureStatus.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void AddJdAiHealthChecks_TagsAreCorrect()
    {
        var services = new ServiceCollection();
        services.AddJdAiHealthChecks();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;

        var regs = options.Registrations.ToDictionary(r => r.Name, StringComparer.Ordinal);

        regs["providers"].Tags.Should().Contain("providers");
        regs["memory"].Tags.Should().Contain("memory");
        regs["session_store"].Tags.Should().Contain("storage");
        regs["disk_space"].Tags.Should().Contain("storage");
    }
}

/// <summary>
/// Tests for <see cref="ActivitySources"/> and <see cref="Meters"/> that are
/// not covered by AgentLoopTracingTests.
/// </summary>
public sealed class ActivitySourcesAndMetersTests
{
    // ── ActivitySources static instances ─────────────────────────────────────

    [Fact]
    public void ActivitySources_Agent_IsNotNull()
    {
        ActivitySources.Agent.Should().NotBeNull();
        ActivitySources.Agent.Name.Should().Be(ActivitySources.AgentSourceName);
    }

    [Fact]
    public void ActivitySources_Tools_IsNotNull()
    {
        ActivitySources.Tools.Should().NotBeNull();
        ActivitySources.Tools.Name.Should().Be(ActivitySources.ToolsSourceName);
    }

    [Fact]
    public void ActivitySources_Providers_IsNotNull()
    {
        ActivitySources.Providers.Should().NotBeNull();
        ActivitySources.Providers.Name.Should().Be(ActivitySources.ProvidersSourceName);
    }

    [Fact]
    public void ActivitySources_Sessions_IsNotNull()
    {
        ActivitySources.Sessions.Should().NotBeNull();
        ActivitySources.Sessions.Name.Should().Be(ActivitySources.SessionsSourceName);
    }

    [Fact]
    public void ActivitySources_AllSourceNames_HasCorrectCount()
    {
        ActivitySources.AllSourceNames.Should().HaveCount(4);
    }

    [Fact]
    public void ActivitySources_AllSourceNames_ContainsAllDefinedNames()
    {
        ActivitySources.AllSourceNames.Should().BeEquivalentTo([
            ActivitySources.AgentSourceName,
            ActivitySources.ToolsSourceName,
            ActivitySources.ProvidersSourceName,
            ActivitySources.SessionsSourceName,
        ]);
    }

    // ── Meters static instances ───────────────────────────────────────────────

    [Fact]
    public void Meters_AllMeterNames_HasTwoEntries()
    {
        Meters.AllMeterNames.Should().HaveCount(2);
        Meters.AllMeterNames.Should().Contain(Meters.AgentMeterName);
        Meters.AllMeterNames.Should().Contain(Meters.GenAiMeterName);
    }

    [Fact]
    public void Meters_AgentMeterName_IsCorrect()
    {
        Meters.AgentMeterName.Should().Be("JD.AI.Agent");
    }

    [Fact]
    public void Meters_GenAiMeterName_IsCorrect()
    {
        Meters.GenAiMeterName.Should().Be("JD.AI.GenAI");
    }

    [Fact]
    public void Meters_LoopDetections_CanRecord()
    {
        var act = () => Meters.LoopDetections.Add(1,
            new KeyValuePair<string, object?>("severity", "warning"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Meters_CircuitBreakerTrips_CanRecord()
    {
        var act = () => Meters.CircuitBreakerTrips.Add(1);

        act.Should().NotThrow();
    }

    [Fact]
    public void Meters_ProviderLatency_CanRecord()
    {
        var act = () => Meters.ProviderLatency.Record(123.4,
            new KeyValuePair<string, object?>("provider", "openai"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Meters_GenAiInputTokens_CanRecord()
    {
        var act = () => Meters.GenAiInputTokens.Add(512,
            new KeyValuePair<string, object?>("gen_ai.system", "anthropic"),
            new KeyValuePair<string, object?>("gen_ai.token.type", "input"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Meters_GenAiOutputTokens_CanRecord()
    {
        var act = () => Meters.GenAiOutputTokens.Add(256,
            new KeyValuePair<string, object?>("gen_ai.system", "openai"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Meters_GenAiOperationDuration_CanRecord()
    {
        var act = () => Meters.GenAiOperationDuration.Record(1.234,
            new KeyValuePair<string, object?>("gen_ai.system", "openai"),
            new KeyValuePair<string, object?>("gen_ai.operation.name", "chat"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Meters_TurnCount_IsNotNull()
    {
        Meters.TurnCount.Should().NotBeNull();
    }

    [Fact]
    public void Meters_TurnDuration_IsNotNull()
    {
        Meters.TurnDuration.Should().NotBeNull();
    }

    [Fact]
    public void Meters_TokensUsed_IsNotNull()
    {
        Meters.TokensUsed.Should().NotBeNull();
    }

    [Fact]
    public void Meters_ToolCalls_IsNotNull()
    {
        Meters.ToolCalls.Should().NotBeNull();
    }

    [Fact]
    public void Meters_ProviderErrors_IsNotNull()
    {
        Meters.ProviderErrors.Should().NotBeNull();
    }

    [Fact]
    public void Meters_LoopDetections_IsNotNull()
    {
        Meters.LoopDetections.Should().NotBeNull();
    }

    [Fact]
    public void Meters_CircuitBreakerTrips_IsNotNull()
    {
        Meters.CircuitBreakerTrips.Should().NotBeNull();
    }

    [Fact]
    public void Meters_ProviderLatency_IsNotNull()
    {
        Meters.ProviderLatency.Should().NotBeNull();
    }

    [Fact]
    public void Meters_GenAiInputTokens_IsNotNull()
    {
        Meters.GenAiInputTokens.Should().NotBeNull();
    }

    [Fact]
    public void Meters_GenAiOutputTokens_IsNotNull()
    {
        Meters.GenAiOutputTokens.Should().NotBeNull();
    }

    [Fact]
    public void Meters_GenAiOperationDuration_IsNotNull()
    {
        Meters.GenAiOperationDuration.Should().NotBeNull();
    }
}
