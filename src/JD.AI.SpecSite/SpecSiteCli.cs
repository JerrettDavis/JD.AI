using System.Text;

namespace JD.AI.SpecSite;

public static class SpecSiteCli
{
    public static SpecSiteOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var repoRoot = Directory.GetCurrentDirectory();
        string? specsRootArg = null;
        string? outputRootArg = null;
        string? siteTitle = null;
        string? docFxOutputArg = null;
        var emitDocFx = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--repo-root":
                    repoRoot = ReadValue(args, ref i, arg);
                    break;
                case "--specs-root":
                    specsRootArg = ReadValue(args, ref i, arg);
                    break;
                case "--output":
                    outputRootArg = ReadValue(args, ref i, arg);
                    break;
                case "--title":
                    siteTitle = ReadValue(args, ref i, arg);
                    break;
                case "--emit-docfx":
                    emitDocFx = true;
                    break;
                case "--docfx-output":
                    docFxOutputArg = ReadValue(args, ref i, arg);
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {arg}", nameof(args));
            }
        }

        var normalizedRepoRoot = ResolvePath(repoRoot, Directory.GetCurrentDirectory());
        var normalizedSpecsRoot = specsRootArg is null
            ? Path.Combine(normalizedRepoRoot, "specs")
            : ResolvePath(specsRootArg, normalizedRepoRoot);
        var normalizedOutputRoot = outputRootArg is null
            ? Path.Combine(normalizedRepoRoot, "artifacts", "spec-site")
            : ResolvePath(outputRootArg, normalizedRepoRoot);
        var normalizedDocFxRoot = docFxOutputArg is null
            ? Path.Combine(normalizedRepoRoot, "docs", "specs", "generated")
            : ResolvePath(docFxOutputArg, normalizedRepoRoot);

        return new SpecSiteOptions(
            normalizedRepoRoot,
            normalizedSpecsRoot,
            normalizedOutputRoot,
            siteTitle ?? "JD.AI UPSS Specification Portal",
            emitDocFx,
            normalizedDocFxRoot);
    }

    public static bool IsHelp(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Any(arg =>
            string.Equals(arg, "--help", StringComparison.Ordinal) ||
            string.Equals(arg, "-h", StringComparison.Ordinal));
    }

    public static string BuildHelpText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("JD.AI Spec Site Generator");
        builder.AppendLine();
        builder.AppendLine("Usage:");
        builder.AppendLine("  dotnet run --project src/JD.AI.SpecSite -- [options]");
        builder.AppendLine();
        builder.AppendLine("Options:");
        builder.AppendLine("  --repo-root <path>      Repository root (default: current directory)");
        builder.AppendLine("  --specs-root <path>     Specs root (default: <repo-root>/specs)");
        builder.AppendLine("  --output <path>         HTML output root (default: <repo-root>/artifacts/spec-site)");
        builder.AppendLine("  --title <text>          Site title");
        builder.AppendLine("  --emit-docfx            Emit DocFX-ready markdown + toc");
        builder.AppendLine("  --docfx-output <path>   DocFX output root (default: <repo-root>/docs/specs/generated)");
        builder.AppendLine("  -h, --help              Show this help");
        return builder.ToString();
    }

    private static string ResolvePath(string path, string basePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(basePath, path));
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException(
                $"Option {optionName} requires a value.",
                nameof(args));

        index++;
        return args[index];
    }
}
