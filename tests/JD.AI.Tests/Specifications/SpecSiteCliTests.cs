using JD.AI.SpecSite;

namespace JD.AI.Tests.Specifications;

public sealed class SpecSiteCliTests
{
    [Fact]
    public void Parse_UnknownOption_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => SpecSiteCli.Parse(["--bogus"]));
        Assert.Contains("Unknown option", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_MissingOptionValue_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => SpecSiteCli.Parse(["--repo-root"]));
        Assert.Contains("requires a value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ResolvesDefaultPathsFromCurrentDirectory()
    {
        var originalCwd = Directory.GetCurrentDirectory();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"spec-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            Directory.SetCurrentDirectory(tempRoot);
            var options = SpecSiteCli.Parse([]);

            Assert.Equal(Path.GetFullPath(tempRoot), options.RepoRoot);
            Assert.Equal(Path.Combine(Path.GetFullPath(tempRoot), "specs"), options.SpecsRoot);
            Assert.Equal(Path.Combine(Path.GetFullPath(tempRoot), "artifacts", "spec-site"), options.OutputRoot);
            Assert.Equal(Path.Combine(Path.GetFullPath(tempRoot), "docs", "specs", "generated"), options.DocFxOutputRoot);
            Assert.False(options.EmitDocFx);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Parse_ResolvesRelativePathsAgainstRepoRoot()
    {
        var originalCwd = Directory.GetCurrentDirectory();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"spec-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            Directory.SetCurrentDirectory(tempRoot);
            var options = SpecSiteCli.Parse([
                "--repo-root", ".",
                "--specs-root", "my-specs",
                "--output", "site-out",
                "--docfx-output", "docfx-out",
                "--emit-docfx",
                "--title", "Portal"
            ]);

            Assert.Equal(Path.GetFullPath(tempRoot), options.RepoRoot);
            Assert.Equal(Path.Combine(Path.GetFullPath(tempRoot), "my-specs"), options.SpecsRoot);
            Assert.Equal(Path.Combine(Path.GetFullPath(tempRoot), "site-out"), options.OutputRoot);
            Assert.Equal(Path.Combine(Path.GetFullPath(tempRoot), "docfx-out"), options.DocFxOutputRoot);
            Assert.True(options.EmitDocFx);
            Assert.Equal("Portal", options.SiteTitle);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void IsHelp_DetectsFlags(string flag)
    {
        Assert.True(SpecSiteCli.IsHelp([flag]));
    }

    [Fact]
    public void BuildHelpText_ContainsUsageAndOptions()
    {
        var help = SpecSiteCli.BuildHelpText();

        Assert.Contains("Usage:", help, StringComparison.Ordinal);
        Assert.Contains("--repo-root", help, StringComparison.Ordinal);
        Assert.Contains("--emit-docfx", help, StringComparison.Ordinal);
    }
}
