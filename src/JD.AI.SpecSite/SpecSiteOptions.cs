namespace JD.AI.SpecSite;

public sealed record SpecSiteOptions(
    string RepoRoot,
    string SpecsRoot,
    string OutputRoot,
    string SiteTitle,
    bool EmitDocFx,
    string DocFxOutputRoot);
