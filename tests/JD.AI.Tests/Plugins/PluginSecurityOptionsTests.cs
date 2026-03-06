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
}
