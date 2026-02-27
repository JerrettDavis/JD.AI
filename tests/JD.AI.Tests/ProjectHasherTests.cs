using JD.AI.Tui.Persistence;

namespace JD.AI.Tui.Tests;

public class ProjectHasherTests
{
    [Fact]
    public void Hash_ReturnsDeterministicValue()
    {
        var h1 = ProjectHasher.Hash("/tmp/test");
        var h2 = ProjectHasher.Hash("/tmp/test");
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Hash_ReturnsEightCharHex()
    {
        var hash = ProjectHasher.Hash("/some/path");
        Assert.Equal(8, hash.Length);
        Assert.Matches("^[0-9a-f]{8}$", hash);
    }

    [Fact]
    public void Hash_DifferentPaths_ProduceDifferentHashes()
    {
        var h1 = ProjectHasher.Hash("/path/a");
        var h2 = ProjectHasher.Hash("/path/b");
        Assert.NotEqual(h1, h2);
    }
}
