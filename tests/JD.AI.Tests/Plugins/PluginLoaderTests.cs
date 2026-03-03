using FluentAssertions;
using JD.AI.Core.Plugins;
using JD.AI.Plugins.SDK;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace JD.AI.Tests.Plugins;

public sealed class PluginLoaderTests
{
    private readonly PluginLoader _loader = new(NullLogger<PluginLoader>.Instance);

    [Fact]
    public async Task LoadFromDirectoryAsync_NonExistentDirectory_ReturnsEmpty()
    {
        var context = Substitute.For<IPluginContext>();
        var result = await _loader.LoadFromDirectoryAsync(
            @"C:\nonexistent\plugin\dir", context);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAll_Initially_ReturnsEmpty()
    {
        _loader.GetAll().Should().BeEmpty();
    }

    [Fact]
    public async Task UnloadAsync_UnknownName_DoesNotThrow()
    {
        var act = () => _loader.UnloadAsync("nonexistent-plugin");

        await act.Should().NotThrowAsync();
    }
}
