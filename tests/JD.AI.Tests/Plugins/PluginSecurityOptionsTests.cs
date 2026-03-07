using FluentAssertions;
using JD.AI.Core.Plugins;

namespace JD.AI.Tests.Plugins;

public sealed class PluginSecurityOptionsTests
{
    [Fact]
    public void Default_NoTrustedPublishers()
    {
        var opts = new PluginSecurityOptions();
        opts.TrustedPublishers.Should().BeEmpty();
        opts.EnforceTrustedPublishers.Should().BeFalse();
    }

    [Fact]
    public void EnforceTrustedPublishers_WhenPublishersPresent_ReturnsTrue()
    {
        var opts = new PluginSecurityOptions
        {
            TrustedPublishers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Acme" },
        };
        opts.EnforceTrustedPublishers.Should().BeTrue();
    }

    [Fact]
    public void TrustedPublishers_CaseInsensitive()
    {
        var opts = new PluginSecurityOptions
        {
            TrustedPublishers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "JD" },
        };
        opts.TrustedPublishers.Contains("jd").Should().BeTrue();
        opts.TrustedPublishers.Contains("JD").Should().BeTrue();
    }

    [Fact]
    public void ConstantName_IsCorrect()
    {
        PluginSecurityOptions.TrustedPublishersEnvironmentVariable
            .Should().Be("JDAI_PLUGIN_TRUSTED_PUBLISHERS");
    }

    // ── FromEnvironment ──────────────────────────────────────────────

    [Fact]
    public void FromEnvironment_NoVariable_ReturnsEmpty()
    {
        var original = Environment.GetEnvironmentVariable(
            PluginSecurityOptions.TrustedPublishersEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, null);
            var opts = PluginSecurityOptions.FromEnvironment();
            opts.TrustedPublishers.Should().BeEmpty();
            opts.EnforceTrustedPublishers.Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, original);
        }
    }

    [Fact]
    public void FromEnvironment_EmptyVariable_ReturnsEmpty()
    {
        var original = Environment.GetEnvironmentVariable(
            PluginSecurityOptions.TrustedPublishersEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, "");
            var opts = PluginSecurityOptions.FromEnvironment();
            opts.TrustedPublishers.Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, original);
        }
    }

    [Fact]
    public void FromEnvironment_CommaSeparated_ParsesAll()
    {
        var original = Environment.GetEnvironmentVariable(
            PluginSecurityOptions.TrustedPublishersEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, "acme,contoso,fabrikam");
            var opts = PluginSecurityOptions.FromEnvironment();
            opts.TrustedPublishers.Should().HaveCount(3);
            opts.TrustedPublishers.Should().Contain("acme");
            opts.TrustedPublishers.Should().Contain("contoso");
            opts.TrustedPublishers.Should().Contain("fabrikam");
            opts.EnforceTrustedPublishers.Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, original);
        }
    }

    [Fact]
    public void FromEnvironment_SemicolonSeparated_ParsesAll()
    {
        var original = Environment.GetEnvironmentVariable(
            PluginSecurityOptions.TrustedPublishersEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, "acme;contoso");
            var opts = PluginSecurityOptions.FromEnvironment();
            opts.TrustedPublishers.Should().HaveCount(2);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, original);
        }
    }

    [Fact]
    public void FromEnvironment_TrimsWhitespace()
    {
        var original = Environment.GetEnvironmentVariable(
            PluginSecurityOptions.TrustedPublishersEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, " acme , contoso ");
            var opts = PluginSecurityOptions.FromEnvironment();
            opts.TrustedPublishers.Should().Contain("acme");
            opts.TrustedPublishers.Should().Contain("contoso");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, original);
        }
    }

    [Fact]
    public void FromEnvironment_SkipsEmptyEntries()
    {
        var original = Environment.GetEnvironmentVariable(
            PluginSecurityOptions.TrustedPublishersEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, "acme,,,,contoso");
            var opts = PluginSecurityOptions.FromEnvironment();
            opts.TrustedPublishers.Should().HaveCount(2);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, original);
        }
    }

    [Fact]
    public void FromEnvironment_CaseInsensitiveLookup()
    {
        var original = Environment.GetEnvironmentVariable(
            PluginSecurityOptions.TrustedPublishersEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, "ACME,Contoso");
            var opts = PluginSecurityOptions.FromEnvironment();
            opts.TrustedPublishers.Should().Contain("acme");
            opts.TrustedPublishers.Should().Contain("contoso");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                PluginSecurityOptions.TrustedPublishersEnvironmentVariable, original);
        }
    }
}
