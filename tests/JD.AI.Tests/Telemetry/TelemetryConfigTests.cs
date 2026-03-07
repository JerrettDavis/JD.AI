using FluentAssertions;
using JD.AI.Telemetry;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace JD.AI.Tests.Telemetry;

public sealed class TelemetryConfigTests
{
    [Fact]
    public void Defaults_Enabled_IsTrue()
    {
        var config = new TelemetryConfig();

        config.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Defaults_ServiceName_IsJdai()
    {
        var config = new TelemetryConfig();

        config.ServiceName.Should().Be("jdai");
    }

    [Fact]
    public void Defaults_Exporter_IsConsole()
    {
        var config = new TelemetryConfig();

        config.Exporter.Should().Be("console");
    }

    [Fact]
    public void Defaults_OtlpProtocol_IsGrpc()
    {
        var config = new TelemetryConfig();

        config.OtlpProtocol.Should().Be("grpc");
    }

    [Fact]
    public void Defaults_Endpoint_IsNull()
    {
        var config = new TelemetryConfig();

        config.Endpoint.Should().BeNull();
    }

    [Fact]
    public void BindFromDictionary_OverridesDefaults()
    {
        var dict = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Enabled"] = "false",
            ["ServiceName"] = "my-service",
            ["Exporter"] = "otlp",
            ["OtlpProtocol"] = "http",
            ["Endpoint"] = "http://localhost:4318",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();

        var config = new TelemetryConfig();
        configuration.Bind(config);

        config.Enabled.Should().BeFalse();
        config.ServiceName.Should().Be("my-service");
        config.Exporter.Should().Be("otlp");
        config.OtlpProtocol.Should().Be("http");
        config.Endpoint.Should().Be("http://localhost:4318");
    }

    [Fact]
    public void BindFromDictionary_PartialOverride_RetainsDefaults()
    {
        var dict = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Exporter"] = "zipkin",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();

        var config = new TelemetryConfig();
        configuration.Bind(config);

        config.Enabled.Should().BeTrue();
        config.ServiceName.Should().Be("jdai");
        config.Exporter.Should().Be("zipkin");
        config.OtlpProtocol.Should().Be("grpc");
        config.Endpoint.Should().BeNull();
    }

    [Fact]
    public void Properties_CanBeSetDirectly()
    {
        var config = new TelemetryConfig
        {
            Enabled = false,
            ServiceName = "test-svc",
            Exporter = "otlp",
            OtlpProtocol = "http",
            Endpoint = "http://otel-collector:4317",
        };

        config.Enabled.Should().BeFalse();
        config.ServiceName.Should().Be("test-svc");
        config.Exporter.Should().Be("otlp");
        config.OtlpProtocol.Should().Be("http");
        config.Endpoint.Should().Be("http://otel-collector:4317");
    }
}
