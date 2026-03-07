using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JD.AI.Core.Skills;

namespace JD.AI.Tests.Skills;

public sealed class SkillLifecycleManagerCoverageTests : IDisposable
{
    private static readonly string[] ExpectedMixedValues = ["x", "2"];
    private readonly string _root;

    public SkillLifecycleManagerCoverageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"jdai-skills-coverage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void TryRefresh_SecondCallWithoutChanges_ReturnsFalse_AndFormatsReport()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");

        WriteSkill(workspace, "active", "active", "good");
        WriteSkill(workspace, "missing-env", "missing-env", "needs env", "  jdai:\n    requires:\n      env: [MISSING_ENV]");
        WriteSkill(managed, "dupe", "dupe", "managed");
        WriteSkill(workspace, "dupe", "dupe", "workspace");
        Directory.CreateDirectory(Path.Combine(workspace, "invalid"));
        File.WriteAllText(Path.Combine(workspace, "invalid", "SKILL.md"), "no frontmatter");

        using var manager = CreateManager(bundled, managed, workspace, binaryExists: _ => true, platform: "linux");

        var changed1 = manager.TryRefresh(out _);
        var changed2 = manager.TryRefresh(out _);
        var report = manager.FormatStatusReport();

        Assert.True(changed1);
        Assert.False(changed2);
        Assert.Contains("[active]", report);
        Assert.Contains("[excluded]", report);
        Assert.Contains("[shadowed]", report);
        Assert.Contains("[invalid]", report);
    }

    [Fact]
    public void BeginRunScope_WithNoSkills_ReturnsNoopAndIsDisposable()
    {
        using var manager = CreateManager(
            Path.Combine(_root, "bundled"),
            Path.Combine(_root, "managed"),
            Path.Combine(_root, "workspace"),
            binaryExists: _ => true,
            platform: "linux");

        using var scope = manager.BeginRunScope();
        scope.Dispose();
    }

    [Fact]
    public void Parsing_RejectsInvalidMetadataShapesAndUnknownKeys()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");

        WriteRawSkill(workspace, "unknown-top", """
            ---
            name: unknown-top
            description: invalid
            unknown: value
            ---
            body
            """);

        WriteRawSkill(workspace, "no-name", """
            ---
            description: missing name
            ---
            body
            """);

        WriteRawSkill(workspace, "no-description", """
            ---
            name: no-description
            ---
            body
            """);

        WriteRawSkill(workspace, "bad-metadata", """
            ---
            name: bad-metadata
            description: metadata shape
            metadata: just-a-string
            ---
            body
            """);

        WriteRawSkill(workspace, "bad-provider-key", """
            ---
            name: bad-provider-key
            description: provider key
            metadata:
              jdai:
                badkey: true
            ---
            body
            """);

        WriteRawSkill(workspace, "bad-requires-key", """
            ---
            name: bad-requires-key
            description: requires key
            metadata:
              jdai:
                requires:
                  bad: true
            ---
            body
            """);
        WriteRawSkill(workspace, "bad-json-metadata", """
            ---
            name: bad-json-metadata
            description: bad json metadata
            metadata: '{'
            ---
            body
            """);

        using var manager = CreateManager(bundled, managed, workspace, binaryExists: _ => true, platform: "linux");
        var snapshot = manager.GetSnapshot();

        AssertStatus(snapshot, "unknown-top", SkillEligibilityState.Invalid, SkillReasonCodes.InvalidSchema);
        AssertStatus(snapshot, "no-name", SkillEligibilityState.Invalid, SkillReasonCodes.InvalidSchema);
        AssertStatus(snapshot, "no-description", SkillEligibilityState.Invalid, SkillReasonCodes.InvalidSchema);
        AssertStatus(snapshot, "bad-metadata", SkillEligibilityState.Invalid, SkillReasonCodes.InvalidSchema);
        AssertStatus(snapshot, "bad-provider-key", SkillEligibilityState.Invalid, SkillReasonCodes.InvalidSchema);
        AssertStatus(snapshot, "bad-requires-key", SkillEligibilityState.Invalid, SkillReasonCodes.InvalidSchema);
        AssertStatus(snapshot, "bad-json-metadata", SkillEligibilityState.Invalid, SkillReasonCodes.InvalidSchema);
    }

    [Fact]
    public void Parsing_AcceptsJsonMetadataString_AndEnvApiKeySatisfiesRequirement()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");
        var configPath = Path.Combine(_root, "skills.json");

        var metadataJson = "{\"jdai\":{\"primaryEnv\":\"PRIMARY\",\"requires\":{\"env\":[\"PRIMARY\"],\"bins\":[],\"anyBins\":[],\"config\":[]},\"always\":false},\"extra\":{\"n\":1,\"arr\":[1,\"x\",null],\"t\":true,\"f\":false,\"nil\":null}}";
        WriteRawSkill(workspace, "json-meta", $"---\nname: json-meta\ndescription: json metadata\nmetadata: '{metadataJson}'\n---\nbody");

        File.WriteAllText(configPath, """
            {
              "skills": {
                "entries": {
                  "json-meta": {
                    "apiKey": "secret"
                  }
                }
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
        AssertStatus(snapshot, "json-meta", SkillEligibilityState.Active, SkillReasonCodes.None);
    }

    [Fact]
    public void DefaultBinaryProbe_UsesRootedPath_AndPathEnvironmentFallback()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");

        var fakeBin = Path.Combine(_root, "fake.bin");
        File.WriteAllText(fakeBin, "x");

        WriteSkill(workspace, "root-bin", "root-bin", "rooted", $"  jdai:\n    requires:\n      bins:\n        - {fakeBin.Replace("\\", "\\\\")}");

        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var originalPathExt = Environment.GetEnvironmentVariable("PATHEXT");
        Environment.SetEnvironmentVariable("PATH", string.Empty);
        try
        {
            using var manager = CreateManager(bundled, managed, workspace, platform: "linux");
            var snapshot = manager.GetSnapshot();
            AssertStatus(snapshot, "root-bin", SkillEligibilityState.Active, SkillReasonCodes.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void ConfigTruthiness_CoversBooleansStringsNumbersArraysAndObjects()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");
        var configPath = Path.Combine(_root, "skills.json");

        WriteSkill(workspace, "cfg-bool", "cfg-bool", "cfg", "  jdai:\n    requires:\n      config: [cfg.bool]");
        WriteSkill(workspace, "cfg-str", "cfg-str", "cfg", "  jdai:\n    requires:\n      config: [cfg.str]");
        WriteSkill(workspace, "cfg-num", "cfg-num", "cfg", "  jdai:\n    requires:\n      config: [cfg.num]");
        WriteSkill(workspace, "cfg-arr", "cfg-arr", "cfg", "  jdai:\n    requires:\n      config: [cfg.arr]");
        WriteSkill(workspace, "cfg-obj", "cfg-obj", "cfg", "  jdai:\n    requires:\n      config: [cfg.obj]");
        WriteSkill(workspace, "cfg-zero", "cfg-zero", "cfg", "  jdai:\n    requires:\n      config: [cfg.zero]");

        File.WriteAllText(configPath, """
            {
              "cfg": {
                "bool": true,
                "str": "hello",
                "num": 7,
                "arr": [1],
                "obj": { "x": 1 },
                "zero": 0
              }
            }
            """);

        using var manager = CreateManager(bundled, managed, workspace, userConfigPath: configPath, binaryExists: _ => true, platform: "linux");
        var snapshot = manager.GetSnapshot();

        AssertStatus(snapshot, "cfg-bool", SkillEligibilityState.Active, SkillReasonCodes.None);
        AssertStatus(snapshot, "cfg-str", SkillEligibilityState.Active, SkillReasonCodes.None);
        AssertStatus(snapshot, "cfg-num", SkillEligibilityState.Active, SkillReasonCodes.None);
        AssertStatus(snapshot, "cfg-arr", SkillEligibilityState.Active, SkillReasonCodes.None);
        AssertStatus(snapshot, "cfg-obj", SkillEligibilityState.Active, SkillReasonCodes.None);
        AssertStatus(snapshot, "cfg-zero", SkillEligibilityState.Excluded, SkillReasonCodes.MissingConfig);
    }

    [Fact]
    public void InternalWatcherHelpers_CoverDebounceAndErrorPaths()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");
        var configPath = Path.Combine(_root, "skills.json");

        WriteSkill(workspace, "watch", "watch", "watch");
        File.WriteAllText(configPath, """
            { "skills": { "load": { "watch": true, "watchDebounceMs": 1000 } } }
            """);

        using var manager = CreateManager(bundled, managed, workspace, userConfigPath: configPath, binaryExists: _ => true, platform: "linux");
        _ = manager.GetSnapshot();

        var type = typeof(SkillLifecycleManager);
        var onWatcherChanged = type.GetMethod("OnWatcherChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var tryAddWatcher = type.GetMethod("TryAddWatcher", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var args = new object[]
        {
            this,
            new FileSystemEventArgs(WatcherChangeTypes.Changed, workspace, "SKILL.md"),
        };

        onWatcherChanged.Invoke(manager, args);
        onWatcherChanged.Invoke(manager, args); // debounced path

        tryAddWatcher.Invoke(manager, ["::invalid::path::", "*", false]); // catch path
        File.WriteAllText(configPath, """{ "skills": { "load": { "watch": false } } }""");
        _ = manager.GetSnapshot();

        // Also cover static hash fallback path.
        var hashMethod = type.GetMethod("ComputeFileContentHash", BindingFlags.NonPublic | BindingFlags.Static)!;
        var hash = (string)hashMethod.Invoke(null, [Path.Combine(_root, "missing-skill.md")])!;
        Assert.Equal("io-error", hash);
    }

    [Fact]
    public void Reflection_CoversPrivateHelperBranches()
    {
        using var manager = CreateManager(
            Path.Combine(_root, "bundled"),
            Path.Combine(_root, "managed"),
            Path.Combine(_root, "workspace"),
            platform: "linux",
            isWindows: false);

        var type = typeof(SkillLifecycleManager);

        var detectPlatform = type.GetMethod("DetectPlatform", BindingFlags.NonPublic | BindingFlags.Static)!;
        _ = (string)detectPlatform.Invoke(null, null)!;

        var binaryExists = type.GetMethod("BinaryExistsOnPath", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.False((bool)binaryExists.Invoke(manager, [""])!);
        var fakeBin = Path.Combine(_root, "ref-bin");
        File.WriteAllText(fakeBin, "x");
        Assert.True((bool)binaryExists.Invoke(manager, [fakeBin])!);
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var originalPathExt = Environment.GetEnvironmentVariable("PATHEXT");
        Environment.SetEnvironmentVariable("PATH", string.Empty);
        try
        {
            Assert.False((bool)binaryExists.Invoke(manager, ["definitely-not-installed"])!);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }

        var linuxBinDir = Path.Combine(_root, "bin-linux");
        Directory.CreateDirectory(linuxBinDir);
        var linuxBin = Path.Combine(linuxBinDir, "linux-tool");
        File.WriteAllText(linuxBin, "x");
        Environment.SetEnvironmentVariable("PATH", linuxBinDir);
        Assert.True((bool)binaryExists.Invoke(manager, ["linux-tool"])!);
        Assert.False((bool)binaryExists.Invoke(manager, ["linux-tool-missing"])!);

        using var windowsManager = CreateManager(
            Path.Combine(_root, "bundled-win"),
            Path.Combine(_root, "managed-win"),
            Path.Combine(_root, "workspace-win"),
            platform: "win32",
            isWindows: true);

        var winBinDir = Path.Combine(_root, "bin-win");
        Directory.CreateDirectory(winBinDir);
        File.WriteAllText(Path.Combine(winBinDir, "win-tool.CMD"), "x");
        Environment.SetEnvironmentVariable("PATH", winBinDir);
        Environment.SetEnvironmentVariable("PATHEXT", null);
        Assert.True((bool)binaryExists.Invoke(windowsManager, ["win-tool"])!);
        Environment.SetEnvironmentVariable("PATH", originalPath);
        Environment.SetEnvironmentVariable("PATHEXT", originalPathExt);

        var isTruthy = type.GetMethod("IsTruthy", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.False((bool)isTruthy.Invoke(null, [null])!);
        Assert.False((bool)isTruthy.Invoke(null, [JsonValue.Create(false)])!);
        Assert.True((bool)isTruthy.Invoke(null, [JsonValue.Create(true)])!);
        Assert.False((bool)isTruthy.Invoke(null, [JsonValue.Create("false")])!);
        Assert.False((bool)isTruthy.Invoke(null, [JsonValue.Create("0")])!);
        Assert.True((bool)isTruthy.Invoke(null, [JsonValue.Create("yes")])!);
        Assert.False((bool)isTruthy.Invoke(null, [JsonValue.Create(0)])!);
        Assert.True((bool)isTruthy.Invoke(null, [JsonValue.Create(2)])!);
        Assert.False((bool)isTruthy.Invoke(null, [new JsonArray()])!);
        Assert.True((bool)isTruthy.Invoke(null, [new JsonArray(1)])!);
        Assert.False((bool)isTruthy.Invoke(null, [new JsonObject()])!);
        Assert.True((bool)isTruthy.Invoke(null, [new JsonObject { ["x"] = 1 }])!);
        Assert.False((bool)isTruthy.Invoke(null, [JsonNode.Parse("null")!])!);
        Assert.False((bool)isTruthy.Invoke(null, [JsonValue.Create(default(JsonElement))!])!);

        var normalizeAsMap = type.GetMethod("NormalizeAsMap", BindingFlags.NonPublic | BindingFlags.Static)!;
        var map = (Dictionary<string, object?>)normalizeAsMap.Invoke(null, [123])!;
        Assert.Empty(map);

        var normalizeValue = type.GetMethod("NormalizeValue", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Null(normalizeValue.Invoke(null, [null]));

        var asDictionary = type.GetMethod("AsDictionary", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Null(asDictionary.Invoke(null, [null]));
        Assert.Null(asDictionary.Invoke(null, ["{"]));
        Assert.Null(asDictionary.Invoke(null, ["[1,2,3]"]));
        var fromJsonMap = (Dictionary<string, object?>)asDictionary.Invoke(null, ["{\"a\":1,\"b\":null}"])!;
        Assert.Equal("1", fromJsonMap["a"]);
        Assert.Null(fromJsonMap["b"]);

        var convertJsonNode = type.GetMethod("ConvertJsonNode", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Null(convertJsonNode.Invoke(null, [JsonNode.Parse("null")]));
        var undefinedElement = default(JsonElement);
        Assert.Null(convertJsonNode.Invoke(null, [JsonValue.Create(undefinedElement)!]));

        var normalizeStringList = type.GetMethod("NormalizeStringList", BindingFlags.NonPublic | BindingFlags.Static)!;
        var emptyList = (List<string>)normalizeStringList.Invoke(null, [123])!;
        Assert.Empty(emptyList);
        var mixedList = (List<string>)normalizeStringList.Invoke(null, [(IEnumerable)new ArrayList { "x", 2 }])!;
        Assert.Equal(ExpectedMixedValues, mixedList, StringComparer.Ordinal);
        var whitespaceOnly = (List<string>)normalizeStringList.Invoke(null, ["   "])!;
        Assert.Empty(whitespaceOnly);

        var readBool = type.GetMethod("ReadBool", BindingFlags.NonPublic | BindingFlags.Static)!;
        var boolMap = new Dictionary<string, object?>(StringComparer.Ordinal) { ["x"] = 123, ["y"] = "true" };
        Assert.Null(readBool.Invoke(null, [boolMap, "x"]));
        Assert.Equal(true, readBool.Invoke(null, [boolMap, "y"]));

        var readMap = type.GetMethod("ReadMap", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Null(readMap.Invoke(null, [boolMap, "missing"]));

        var readString = type.GetMethod("ReadString", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Null(readString.Invoke(null, [boolMap, "x"]));

        var isConfigSatisfied = type.GetMethod("IsConfigSatisfied", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(true, isConfigSatisfied.Invoke(null, [" ", null, null]));
    }

    [Fact]
    public void Models_AndLoaderApiKeyObjectBranch_AreCovered()
    {
        var source = new SkillSourceDirectory("s", "r", SkillSourceKind.Bundled, 1);
        var meta = new SkillMetadata("n", "d", "k", true, "P", ["linux"], ["b"], ["ab"], ["e"], ["c"]);
        var active = new ActiveSkill("n", "k", "dir", "file", source, meta);
        var status = new SkillStatus("n", "k", "file", source, SkillEligibilityState.Active, SkillReasonCodes.None, null);
        var now = DateTimeOffset.UtcNow;
        var snap = new SkillSnapshot(now, "f", [active], [status]);
        Assert.Equal("n", active.Name);
        Assert.Equal("dir", active.DirectoryPath);
        Assert.Equal("file", active.SkillFilePath);
        Assert.Equal("k", status.SkillKey);
        Assert.Equal("file", status.SkillFilePath);
        Assert.Equal("f", snap.Fingerprint);
        Assert.Equal(now, snap.GeneratedAtUtc);

        var mergedBothNull = SkillEntryConfig.Merge(null, null);
        Assert.NotNull(mergedBothNull);
        Assert.Empty(mergedBothNull.Env);

        var higherOnly = new SkillEntryConfig
        {
            Enabled = true,
            ApiKey = "k",
            Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["E"] = "1" },
            Config = JsonNode.Parse("""{ "x": 1 }"""),
        };
        var mergedHigher = SkillEntryConfig.Merge(null, higherOnly);
        Assert.True(mergedHigher.Enabled);
        Assert.Equal("k", mergedHigher.ApiKey);
        Assert.Equal("1", mergedHigher.Env["E"]);
        Assert.Equal("1", mergedHigher.Config!["x"]!.ToJsonString());

        var cfgPath = Path.Combine(_root, "apikey-object.json");
        File.WriteAllText(cfgPath, """
            {
              "skills": {
                "entries": {
                  "obj": {
                    "apiKey": { "value": "from-object" }
                  }
                }
              }
            }
            """);
        var cfg = SkillRuntimeConfigLoader.Load(cfgPath, null);
        Assert.Equal("from-object", cfg.GetEntry("obj").ApiKey);
    }

    [Fact]
    public void GatingEnvironmentChecks_AndEnvironmentScopeDoubleDispose_AreCovered()
    {
        using var manager = CreateManager(
            Path.Combine(_root, "bundled-env"),
            Path.Combine(_root, "managed-env"),
            Path.Combine(_root, "workspace-env"),
            platform: "linux",
            isWindows: false);

        var type = typeof(SkillLifecycleManager);
        var isEnvironmentSatisfied = type.GetMethod("IsEnvironmentSatisfied", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var metadata = new SkillMetadata("n", "d", "k", false, "PRIMARY_ENV", [], [], [], [], []);

        var originalHasEnv = Environment.GetEnvironmentVariable("HAS_ENV");
        var originalFromEntry = Environment.GetEnvironmentVariable("FROM_ENTRY");
        var originalPrimary = Environment.GetEnvironmentVariable("PRIMARY_ENV");
        try
        {
            Environment.SetEnvironmentVariable("HAS_ENV", "1");
            Assert.True((bool)isEnvironmentSatisfied.Invoke(manager, ["HAS_ENV", metadata, new SkillEntryConfig()])!);
            Environment.SetEnvironmentVariable("HAS_ENV", null);

            Environment.SetEnvironmentVariable("FROM_ENTRY", null);
            var entryWithEnv = new SkillEntryConfig
            {
                Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["FROM_ENTRY"] = "v" },
            };
            Assert.True((bool)isEnvironmentSatisfied.Invoke(manager, ["FROM_ENTRY", metadata, entryWithEnv])!);

            Environment.SetEnvironmentVariable("PRIMARY_ENV", null);
            var entryWithApiKey = new SkillEntryConfig { ApiKey = "secret" };
            Assert.True((bool)isEnvironmentSatisfied.Invoke(manager, ["PRIMARY_ENV", metadata, entryWithApiKey])!);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HAS_ENV", originalHasEnv);
            Environment.SetEnvironmentVariable("FROM_ENTRY", originalFromEntry);
            Environment.SetEnvironmentVariable("PRIMARY_ENV", originalPrimary);
        }

        var workspace = Path.Combine(_root, "workspace-scope");
        var configPath = Path.Combine(_root, "scope-double-dispose.json");
        WriteSkill(workspace, "scope-skill", "scope-skill", "scope", "  jdai:\n    primaryEnv: SCOPE_PRIMARY");
        File.WriteAllText(configPath, """
            {
              "skills": {
                "entries": {
                  "scope-skill": {
                    "apiKey": "scope-secret",
                    "env": { "SCOPE_ENV": "scope-value" }
                  }
                }
              }
            }
            """);

        var originalScopeEnv = Environment.GetEnvironmentVariable("SCOPE_ENV");
        var originalScopePrimary = Environment.GetEnvironmentVariable("SCOPE_PRIMARY");
        Environment.SetEnvironmentVariable("SCOPE_ENV", null);
        Environment.SetEnvironmentVariable("SCOPE_PRIMARY", null);
        try
        {
            using var scopeManager = CreateManager(
                Path.Combine(_root, "bundled-scope"),
                Path.Combine(_root, "managed-scope"),
                workspace,
                userConfigPath: configPath,
                binaryExists: _ => true,
                platform: "linux",
                isWindows: false);

            var scope = scopeManager.BeginRunScope();
            scope.Dispose();
            scope.Dispose();
            Assert.Null(Environment.GetEnvironmentVariable("SCOPE_ENV"));
            Assert.Null(Environment.GetEnvironmentVariable("SCOPE_PRIMARY"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SCOPE_ENV", originalScopeEnv);
            Environment.SetEnvironmentVariable("SCOPE_PRIMARY", originalScopePrimary);
        }
    }

    [Fact]
    public void Fingerprint_IncludesEntryConfig_WhenPresent()
    {
        var bundled = Path.Combine(_root, "bundled-fp");
        var managed = Path.Combine(_root, "managed-fp");
        var workspace = Path.Combine(_root, "workspace-fp");
        var configPath = Path.Combine(_root, "fingerprint-config.json");

        WriteSkill(workspace, "fp-skill", "fp-skill", "fp");
        File.WriteAllText(configPath, """
            {
              "skills": {
                "entries": {
                  "fp-skill": {
                    "config": { "feature": { "enabled": true } }
                  }
                }
              }
            }
            """);

        using var manager = CreateManager(
            bundled,
            managed,
            workspace,
            userConfigPath: configPath,
            binaryExists: _ => true,
            platform: "linux",
            isWindows: false);

        var snapshot = manager.GetSnapshot();
        Assert.Contains("fp-skill", snapshot.ActiveSkills.Select(s => s.Name), StringComparer.Ordinal);
    }

    [Fact]
    public void Coverage_MiscBranches_ForStateEvaluationAndModels()
    {
        var source = new SkillSourceDirectory("src", _root, SkillSourceKind.Managed, 0);
        var metadata = new SkillMetadata(
            "n",
            "d",
            "k",
            false,
            null,
            [],
            [],
            [],
            [],
            []);
        var active = new ActiveSkill("n", "k", "dir", "file", source, metadata);
        var status = new SkillStatus("n", "k", "file", source, (SkillEligibilityState)999, SkillReasonCodes.None, null);
        var snapshot = new SkillSnapshot(DateTimeOffset.UtcNow, "fp", [active], [status]);

        using var manager = CreateManager(
            Path.Combine(_root, "bundled"),
            Path.Combine(_root, "managed"),
            Path.Combine(_root, "workspace"),
            binaryExists: _ => true,
            platform: "linux");

        var baseline = manager.GetSnapshot();
        snapshot = snapshot with { Fingerprint = baseline.Fingerprint };

        var snapshotField = typeof(SkillLifecycleManager)
            .GetField("_snapshot", BindingFlags.NonPublic | BindingFlags.Instance)!;
        snapshotField.SetValue(manager, snapshot);

        var report = manager.FormatStatusReport();
        Assert.Contains("[unknown]", report);
    }

    [Fact]
    public void Loader_WhenConfigFileLocked_ReturnsEmptyConfig()
    {
        var lockedPath = Path.Combine(_root, "locked-config.json");
        File.WriteAllText(lockedPath, "{ \"skills\": { } }");
        using var stream = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var config = SkillRuntimeConfigLoader.Load(lockedPath, null);

        Assert.Empty(config.Entries);
        Assert.Null(config.RootConfig);
    }

    [Fact]
    public void Discovery_WhenSkillFileLocked_ReturnsInvalidFrontmatterStatus()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");
        var skillDir = WriteSkill(workspace, "locked-skill", "locked-skill", "locked");
        var skillPath = Path.Combine(skillDir, "SKILL.md");
        using var stream = new FileStream(skillPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        using var manager = CreateManager(bundled, managed, workspace, binaryExists: _ => true, platform: "linux");
        var snapshot = manager.GetSnapshot();

        AssertStatus(snapshot, "locked-skill", SkillEligibilityState.Invalid, SkillReasonCodes.InvalidFrontmatter);
    }

    [Fact]
    public void Parsing_CoversEmptyAndUnterminatedAndYamlExceptionAndProviderMissing()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");

        WriteRawSkill(workspace, "empty", "");
        WriteRawSkill(workspace, "unterminated", "---\nname: unterminated\ndescription: bad");
        WriteRawSkill(workspace, "yaml-error", "---\nname: [\ndescription: bad\n---\nbody");
        WriteRawSkill(workspace, "no-provider", "---\nname: no-provider\ndescription: ok\nmetadata:\n  other:\n    x: 1\n---\nbody");

        using var manager = CreateManager(bundled, managed, workspace, binaryExists: _ => true, platform: "linux");
        var snapshot = manager.GetSnapshot();

        AssertStatus(snapshot, "empty", SkillEligibilityState.Invalid, SkillReasonCodes.InvalidFrontmatter);
        AssertStatus(snapshot, "unterminated", SkillEligibilityState.Invalid, SkillReasonCodes.InvalidFrontmatter);
        AssertStatus(snapshot, "yaml-error", SkillEligibilityState.Invalid, SkillReasonCodes.InvalidFrontmatter);
        AssertStatus(snapshot, "no-provider", SkillEligibilityState.Active, SkillReasonCodes.None);
    }

    [Fact]
    public void Evaluation_RespectsDisabledEntryAndStringBoolAndWhitespaceLists()
    {
        var bundled = Path.Combine(_root, "bundled");
        var managed = Path.Combine(_root, "managed");
        var workspace = Path.Combine(_root, "workspace");
        var configPath = Path.Combine(_root, "skills-disable.json");

        WriteRawSkill(workspace, "disable-me", """
            ---
            name: disable-me
            description: disabled
            metadata:
              jdai:
                always: "true"
                requires:
                  env: [ " " ]
            ---
            body
            """);
        File.WriteAllText(configPath, """
            {
              "skills": {
                "entries": {
                  "disable-me": {
                    "enabled": false,
                    "apiKey": "x"
                  }
                }
              }
            }
            """);

        using var manager = CreateManager(bundled, managed, workspace, userConfigPath: configPath, binaryExists: _ => true, platform: "linux");
        var snapshot = manager.GetSnapshot();
        AssertStatus(snapshot, "disable-me", SkillEligibilityState.Excluded, SkillReasonCodes.DisabledByConfig);
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
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static SkillLifecycleManager CreateManager(
        string bundled,
        string managed,
        string workspace,
        string? userConfigPath = null,
        Func<string, bool>? binaryExists = null,
        string platform = "linux",
        bool isWindows = false)
    {
        return new SkillLifecycleManager(
            [
                new SkillSourceDirectory("bundled", bundled, SkillSourceKind.Bundled, 0),
                new SkillSourceDirectory("managed", managed, SkillSourceKind.Managed, 0),
                new SkillSourceDirectory("workspace", workspace, SkillSourceKind.Workspace, 0),
            ],
            userConfigPath: userConfigPath,
            binaryExists: binaryExists,
            isWindows: () => isWindows,
            platformProvider: () => platform);
    }

    private static string WriteSkill(string root, string folder, string name, string description, string? metadataYaml = null)
    {
        var dir = Path.Combine(root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), BuildSkillDocument(name, description, metadataYaml));
        return dir;
    }

    private static void WriteRawSkill(string root, string folder, string content)
    {
        var dir = Path.Combine(root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), content);
    }

    private static string BuildSkillDocument(string name, string description, string? metadataYaml)
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
        sb.AppendLine("body");
        return sb.ToString();
    }

    private static void AssertStatus(SkillSnapshot snapshot, string skill, SkillEligibilityState state, string reason)
    {
        var status = Assert.Single(snapshot.Statuses, s => string.Equals(s.Name, skill, StringComparison.Ordinal));
        Assert.Equal(state, status.State);
        Assert.Equal(reason, status.ReasonCode);
    }
}
