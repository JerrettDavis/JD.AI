# JD.AI.SpecSite

`JD.AI.SpecSite` generates a static HTML portal from repository UPSS specs.
It can also emit DocFX-ready markdown and `toc.yml` output for integration into
the existing `docs/` site.

## Generate static HTML

```bash
dotnet run --project src/JD.AI.SpecSite -- \
  --repo-root . \
  --output artifacts/spec-site
```

## Generate HTML + DocFX content

```bash
dotnet run --project src/JD.AI.SpecSite -- \
  --repo-root . \
  --output artifacts/spec-site \
  --emit-docfx \
  --docfx-output docs/specs/generated
```

## Publish as a single-file executable

```bash
dotnet publish src/JD.AI.SpecSite/JD.AI.SpecSite.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true
```
