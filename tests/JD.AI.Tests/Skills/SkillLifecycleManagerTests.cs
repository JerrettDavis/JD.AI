using System.Text;
using JD.AI.Core.Skills;

namespace JD.AI.Tests.Skills;

public sealed class SkillLifecycleManagerTests : IDisposable
{
    private readonly string _root;

    public SkillLifecycleManagerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"jdai-skills-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void GetSnapshot_ResolvesPrecedenceAndMarksShadowedSkills()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");

        WriteSkill(bundled, "planner-bundled", "planner", "Bundled variant");
        WriteSkill(managed, "planner-managed", "planner", "Managed variant");
        WriteSkill(workspace, "planner-workspace", "planner", "Workspace variant");

        using var manager = CreateManager(
            bundled,
            managed,
            workspace,
            binaryExists: _ => true,
            platform: "linux");

        var snapshot = manager.GetSnapshot();

        var activePlanner = Assert.Single(
            snapshot.ActiveSkills,
            s => string.Equals(s.Name, "planner", StringComparison.Ordinal));
        Assert.Equal(SkillSourceKind.Workspace, activePlanner.Source.Kind);

        var plannerStatuses = snapshot.Statuses
            .Where(s => string.Equals(s.Name, "planner", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(3, plannerStatuses.Length);
        Assert.Single(plannerStatuses, s => s.State == SkillEligibilityState.Active);
        Assert.Equal(2, plannerStatuses.Count(s => s.State == SkillEligibilityState.Shadowed));
    }

    [Fact]
    public void GetSnapshot_AppliesGatingMatrixReasons()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");

        WriteSkill(bundled, "os-skill", "os-skill", "OS gated", metadataYaml: "  jdai:\n    os: [darwin]");
        WriteSkill(managed, "bin-skill", "bin-skill", "Bins gated", metadataYaml: "  jdai:\n    requires:\n      bins: [must-exist]");
        WriteSkill(managed, "any-bin-skill", "any-bin-skill", "Any bins gated", metadataYaml: "  jdai:\n    requires:\n      anyBins: [bin-a, bin-b]");
        WriteSkill(workspace, "env-skill", "env-skill", "Env gated", metadataYaml: "  jdai:\n    requires:\n      env: [NEEDED_ENV]");
        WriteSkill(workspace, "cfg-skill", "cfg-skill", "Config gated", metadataYaml: "  jdai:\n    requires:\n      config: [feature.enabled]");

        using var manager = CreateManager(
            bundled,
            managed,
            workspace,
            binaryExists: _ => false,
            platform: "linux");

        var snapshot = manager.GetSnapshot();

        AssertStatus(snapshot, "os-skill", SkillEligibilityState.Excluded, SkillReasonCodes.OsMismatch);
        AssertStatus(snapshot, "bin-skill", SkillEligibilityState.Excluded, SkillReasonCodes.MissingBinaries);
        AssertStatus(snapshot, "any-bin-skill", SkillEligibilityState.Excluded, SkillReasonCodes.MissingAnyBinary);
        AssertStatus(snapshot, "env-skill", SkillEligibilityState.Excluded, SkillReasonCodes.MissingEnvironment);
        AssertStatus(snapshot, "cfg-skill", SkillEligibilityState.Excluded, SkillReasonCodes.MissingConfig);
    }

    [Fact]
    public void GetSnapshot_ExcludesBundledSkillsWhenAllowListPresent()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");
        var configPath = Path.Combine(_root, "skills.json");

        WriteSkill(bundled, "bundled-a", "bundled-a", "A");
        WriteSkill(bundled, "bundled-b", "bundled-b", "B");
        File.WriteAllText(configPath, """
            {
              "skills": {
                "allowBundled": ["bundled-a"]
              }
            }
            """);

        using var manager = CreateManager(
            bundled,
            managed,
            workspace,
            userConfigPath: configPath,
            binaryExists: _ => true,
            platform: "linux");

        var snapshot = manager.GetSnapshot();

        AssertStatus(snapshot, "bundled-a", SkillEligibilityState.Active, SkillReasonCodes.None);
        AssertStatus(snapshot, "bundled-b", SkillEligibilityState.Excluded, SkillReasonCodes.BundledNotAllowlisted);
    }

    [Fact]
    public void BeginRunScope_InjectsAndRestoresEnvironmentVariables()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");
        var configPath = Path.Combine(_root, "skills.json");

        WriteSkill(
            workspace,
            "injector",
            "injector",
            "Inject env",
            metadataYaml: "  jdai:\n    primaryEnv: PRIMARY_TOKEN");

        File.WriteAllText(configPath, """
            {
              "skills": {
                "entries": {
                  "injector": {
                    "apiKey": "api-key-secret",
                    "env": {
                      "INJECTED_TOKEN": "injected-value"
                    }
                  }
                }
              }
            }
            """);

        var originalInjected = Environment.GetEnvironmentVariable("INJECTED_TOKEN");
        var originalPrimary = Environment.GetEnvironmentVariable("PRIMARY_TOKEN");

        Environment.SetEnvironmentVariable("INJECTED_TOKEN", null);
        Environment.SetEnvironmentVariable("PRIMARY_TOKEN", null);

        try
        {
            using var manager = CreateManager(
                bundled,
                managed,
                workspace,
                userConfigPath: configPath,
                binaryExists: _ => true,
                platform: "linux");

            using (manager.BeginRunScope())
            {
                Assert.Equal("injected-value", Environment.GetEnvironmentVariable("INJECTED_TOKEN"));
                Assert.Equal("api-key-secret", Environment.GetEnvironmentVariable("PRIMARY_TOKEN"));
            }

            Assert.Null(Environment.GetEnvironmentVariable("INJECTED_TOKEN"));
            Assert.Null(Environment.GetEnvironmentVariable("PRIMARY_TOKEN"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("INJECTED_TOKEN", originalInjected);
            Environment.SetEnvironmentVariable("PRIMARY_TOKEN", originalPrimary);
        }
    }

    [Fact]
    public void TryRefresh_DetectsSkillFileChangesAndReloads()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");

        var skillDir = WriteSkill(workspace, "reloadable", "reloadable", "v1");

        using var manager = CreateManager(
            bundled,
            managed,
            workspace,
            binaryExists: _ => true,
            platform: "linux");

        var initial = manager.GetSnapshot();
        Assert.Equal("v1", Assert.Single(initial.ActiveSkills).Metadata.Description);

        var skillFile = Path.Combine(skillDir, "SKILL.md");
        File.WriteAllText(skillFile, SkillDocument("reloadable", "v2"));

        var changed = manager.TryRefresh(out var refreshed);

        Assert.True(changed);
        Assert.Equal("v2", Assert.Single(refreshed.ActiveSkills).Metadata.Description);
    }

    [Fact]
    public void GetSnapshot_MarksInvalidSchemaWhenUnknownFrontmatterKeyExists()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");

        var dir = Path.Combine(workspace, "badskill");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), """
            ---
            name: badskill
            description: Has unknown key
            unsupported-key: true
            ---
            body
            """);

        using var manager = CreateManager(
            bundled,
            managed,
            workspace,
            binaryExists: _ => true,
            platform: "linux");

        var snapshot = manager.GetSnapshot();
        AssertStatus(snapshot, "badskill", SkillEligibilityState.Invalid, SkillReasonCodes.InvalidSchema);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort temp cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort temp cleanup.
        }
    }

    private static SkillLifecycleManager CreateManager(
        string bundled,
        string managed,
        string workspace,
        string? userConfigPath = null,
        Func<string, bool>? binaryExists = null,
        string platform = "linux")
    {
        return new SkillLifecycleManager(
            [
                new SkillSourceDirectory("bundled", bundled, SkillSourceKind.Bundled, 0),
                new SkillSourceDirectory("managed", managed, SkillSourceKind.Managed, 0),
                new SkillSourceDirectory("workspace", workspace, SkillSourceKind.Workspace, 0),
            ],
            userConfigPath: userConfigPath,
            binaryExists: binaryExists,
            platformProvider: () => platform);
    }

    private static string WriteSkill(
        string root,
        string folder,
        string name,
        string description,
        string? metadataYaml = null)
    {
        var dir = Path.Combine(root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), SkillDocument(name, description, metadataYaml));
        return dir;
    }

    private static string SkillDocument(string name, string description, string? metadataYaml = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.Append("name: ");
        sb.Append(name);
        sb.AppendLine();
        sb.Append("description: ");
        sb.Append(description);
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(metadataYaml))
        {
            sb.AppendLine("metadata:");
            sb.AppendLine(metadataYaml);
        }

        sb.AppendLine("---");
        sb.AppendLine("Use the skill carefully.");
        return sb.ToString();
    }

    private static void AssertStatus(
        SkillSnapshot snapshot,
        string name,
        SkillEligibilityState expectedState,
        string expectedReason)
    {
        var status = Assert.Single(
            snapshot.Statuses,
            s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expectedState, status.State);
        Assert.Equal(expectedReason, status.ReasonCode);
    }
}
