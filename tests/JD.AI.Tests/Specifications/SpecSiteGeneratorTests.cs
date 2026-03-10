using FluentAssertions;
using JD.AI.SpecSite;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class SpecSiteGeneratorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    [Fact]
    public void CatalogLoader_LoadsIndexedSpecification()
    {
        WriteSampleSpecRepo(_fixture.DirectoryPath);
        var options = BuildOptions(_fixture.DirectoryPath);

        var catalogs = SpecificationCatalogLoader.Load(options);

        catalogs.Should().HaveCount(1);
        catalogs[0].TypeName.Should().Be("vision");
        catalogs[0].Documents.Should().HaveCount(1);
        catalogs[0].Documents[0].Id.Should().Be("vision.jdai.product");
    }

    [Fact]
    public void SiteWriter_WritesOverviewAndSpecPage()
    {
        WriteSampleSpecRepo(_fixture.DirectoryPath);
        var options = BuildOptions(_fixture.DirectoryPath);
        var catalogs = SpecificationCatalogLoader.Load(options);

        SpecificationSiteWriter.Write(options, catalogs);

        File.Exists(Path.Combine(options.OutputRoot, "index.html")).Should().BeTrue();
        File.Exists(Path.Combine(options.OutputRoot, "vision", "index.html")).Should().BeTrue();
        File.Exists(Path.Combine(options.OutputRoot, "vision", "vision.jdai.product.html"))
            .Should()
            .BeTrue();

        var specHtml = File.ReadAllText(
            Path.Combine(options.OutputRoot, "vision", "vision.jdai.product.html"));
        specHtml.Should().Contain("Enable safe, agent-driven software delivery");
    }

    [Fact]
    public void DocFxWriter_WritesIndexAndToc()
    {
        WriteSampleSpecRepo(_fixture.DirectoryPath);
        var options = BuildOptions(_fixture.DirectoryPath) with { EmitDocFx = true };
        var catalogs = SpecificationCatalogLoader.Load(options);

        DocFxCatalogWriter.Write(options, catalogs);

        var tocPath = Path.Combine(options.DocFxOutputRoot, "toc.yml");
        var indexPath = Path.Combine(options.DocFxOutputRoot, "index.md");
        var specPath = Path.Combine(
            options.DocFxOutputRoot,
            "vision",
            "vision.jdai.product.md");

        File.Exists(tocPath).Should().BeTrue();
        File.Exists(indexPath).Should().BeTrue();
        File.Exists(specPath).Should().BeTrue();

        var toc = File.ReadAllText(tocPath);
        toc.Should().Contain("vision/vision.jdai.product.md");
    }

    public void Dispose() => _fixture.Dispose();

    private static SpecSiteOptions BuildOptions(string repoRoot)
    {
        return new SpecSiteOptions(
            RepoRoot: repoRoot,
            SpecsRoot: Path.Combine(repoRoot, "specs"),
            OutputRoot: Path.Combine(repoRoot, "site"),
            SiteTitle: "Test Portal",
            EmitDocFx: false,
            DocFxOutputRoot: Path.Combine(repoRoot, "docfx"));
    }

    private static void WriteSampleSpecRepo(string repoRoot)
    {
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "vision", "examples"));

        var indexYaml = """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.jdai.product
                title: JD.AI Product Vision
                path: specs/vision/examples/vision.example.yaml
                status: draft
            """;
        File.WriteAllText(Path.Combine(repoRoot, "specs", "vision", "index.yaml"), indexYaml);

        var specYaml = """
            apiVersion: jdai.upss/v1
            kind: Vision
            id: vision.jdai.product
            version: 1
            status: draft
            metadata:
              owners: [JerrettDavis]
              reviewers: [upss-vision-architect]
              lastReviewed: 2026-03-09
              changeReason: Initial baseline.
            problemStatement: No canonical product vision artifact exists.
            mission: Enable safe, agent-driven software delivery.
            targetUsers:
              - id: user.product-owner
                name: Product Owner
                needs: [Traceability]
            valueProposition:
              summary: Specs and implementation stay aligned.
              differentiators: [Repo native]
            successMetrics:
              - id: metric.traceability
                name: Traceability coverage
                target: ">=95%"
                measurement: CI checks
            constraints:
              - Specs remain versioned in repository.
            nonGoals:
              - Replace human release approval.
            trace:
              upstream:
                - docs/index.md
              downstream:
                capabilities: []
            """;
        File.WriteAllText(
            Path.Combine(repoRoot, "specs", "vision", "examples", "vision.example.yaml"),
            specYaml);
    }
}
