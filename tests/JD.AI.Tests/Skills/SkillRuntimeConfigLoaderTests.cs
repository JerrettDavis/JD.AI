using JD.AI.Core.Skills;

namespace JD.AI.Tests.Skills;

public sealed class SkillRuntimeConfigLoaderTests : IDisposable
{
    private readonly string _root;

    public SkillRuntimeConfigLoaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"jdai-skill-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Load_MergesUserAndWorkspaceWithWorkspaceOverride()
    {
        var userPath = Path.Combine(_root, "user.json");
        var workspacePath = Path.Combine(_root, "workspace.json");

        File.WriteAllText(userPath, """
            {
              "skills": {
                "load": { "watch": false, "watchDebounceMs": 100 },
                "allowBundled": ["bundled-a"],
                "entries": {
                  "alpha": {
                    "enabled": true,
                    "env": { "A": "1" }
                  }
                }
              }
            }
            """);

        File.WriteAllText(workspacePath, """
            {
              "skills": {
                "load": { "watch": true, "watchDebounceMs": 400 },
                "allowBundled": ["bundled-b"],
                "entries": {
                  "alpha": {
                    "enabled": false,
                    "apiKey": "abc",
                    "env": { "B": "2" },
                    "config": { "feature": { "enabled": true } }
                  }
                }
              },
              "feature": { "enabled": true }
            }
            """);

        var config = SkillRuntimeConfigLoader.Load(userPath, workspacePath);

        Assert.True(config.Watch);
        Assert.Equal(400, config.WatchDebounceMs);
        Assert.Equal(["bundled-b"], config.AllowBundled.OrderBy(s => s, StringComparer.Ordinal).ToArray());

        var alpha = config.GetEntry("alpha");
        Assert.False(alpha.Enabled);
        Assert.Equal("abc", alpha.ApiKey);
        Assert.Equal("1", alpha.Env["A"]);
        Assert.Equal("2", alpha.Env["B"]);
        Assert.NotNull(alpha.Config);
        Assert.NotNull(config.RootConfig);
    }

    [Fact]
    public void Load_InvalidFilesFallsBackToDefaults()
    {
        var userPath = Path.Combine(_root, "bad-user.json");
        var workspacePath = Path.Combine(_root, "bad-workspace.json");

        File.WriteAllText(userPath, "{ this is invalid json }");
        File.WriteAllText(workspacePath, "[]");

        var config = SkillRuntimeConfigLoader.Load(userPath, workspacePath);

        Assert.True(config.Watch);
        Assert.Equal(250, config.WatchDebounceMs);
        Assert.Empty(config.AllowBundled);
        Assert.Empty(config.Entries);
        Assert.Null(config.RootConfig);
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
}
