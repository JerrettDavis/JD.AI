namespace JD.AI.Tests.Guardrails;

public sealed class RuntimeConstantsLiteralGuardTests
{
    [Fact]
    public void RuntimeLiterals_DoNotReappearOutsideWhitelistedFiles()
    {
        var repoRoot = GetRepoRoot();
        var sourceRoot = Path.Combine(repoRoot, "src");
        var sourceFiles = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
            .ToArray();

        var rules = new[]
        {
            new LiteralGuardRule(
                Literal: "\"localhost\"",
                AllowedFiles:
                [
                    "src/JD.AI.Core/Infrastructure/RuntimeConstants.cs",
                    "src/JD.AI.Dashboard.Wasm/Models/GatewayConfigModel.cs",
                ]),
            new LiteralGuardRule(
                Literal: "15790",
                AllowedFiles:
                [
                    "src/JD.AI.Core/Infrastructure/RuntimeConstants.cs",
                    "src/JD.AI.Dashboard.Wasm/Models/GatewayConfigModel.cs",
                ]),
            new LiteralGuardRule(
                Literal: "/health/ready",
                AllowedFiles: ["src/JD.AI.Core/Infrastructure/RuntimeConstants.cs"]),
            new LiteralGuardRule(
                Literal: "/health/live",
                AllowedFiles: ["src/JD.AI.Core/Infrastructure/RuntimeConstants.cs"]),
            new LiteralGuardRule(
                Literal: "/health/startup",
                AllowedFiles: ["src/JD.AI.Core/Infrastructure/RuntimeConstants.cs"]),
            new LiteralGuardRule(
                Literal: "\"jdai-daemon\"",
                AllowedFiles:
                [
                    "src/JD.AI.Core/Infrastructure/RuntimeConstants.cs",
                    "src/JD.AI/Commands/UpdateCliHandler.cs",
                ]),
            new LiteralGuardRule(
                Literal: "\"JDAIDaemon\"",
                AllowedFiles: ["src/JD.AI.Core/Infrastructure/RuntimeConstants.cs"]),
        };

        var violations = new List<string>();

        foreach (var rule in rules)
        {
            var offenders = sourceFiles
                .Where(file => !rule.AllowedFiles.Contains(file, StringComparer.Ordinal))
                .Where(file =>
                {
                    var content = File.ReadAllText(Path.Combine(repoRoot, file));
                    return content.Contains(rule.Literal, StringComparison.Ordinal);
                })
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToList();

            if (offenders.Count == 0)
                continue;

            violations.Add($"{rule.Literal} => {string.Join(", ", offenders)}");
        }

        Assert.True(
            violations.Count == 0,
            "Hardcoded runtime literal drift detected:\n" + string.Join('\n', violations));
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private sealed record LiteralGuardRule(string Literal, IReadOnlyList<string> AllowedFiles);
}
