using JD.AI.Tui.Providers;
using Xunit;

namespace JD.AI.Tui.Tests;

public sealed class ProviderInfoTests
{
    [Fact]
    public void ProviderInfo_RecordEquality()
    {
        var a = new ProviderInfo("Test", true, "OK", []);
        var b = new ProviderInfo("Test", true, "OK", []);

        // Records use value equality for primitives but reference equality for lists
        Assert.Equal(a.Name, b.Name);
        Assert.Equal(a.IsAvailable, b.IsAvailable);
        Assert.Equal(a.StatusMessage, b.StatusMessage);
    }

    [Fact]
    public void ProviderModelInfo_RecordEquality()
    {
        var a = new ProviderModelInfo("id", "Name", "Provider");
        var b = new ProviderModelInfo("id", "Name", "Provider");

        Assert.Equal(a, b);
    }

    [Fact]
    public void ProviderModelInfo_Inequality()
    {
        var a = new ProviderModelInfo("id1", "Name", "Provider");
        var b = new ProviderModelInfo("id2", "Name", "Provider");

        Assert.NotEqual(a, b);
    }
}
