using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JD.AI.Commands;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using NSubstitute;

namespace JD.AI.Tests.Guardrails;

[Collection("DataDirectories")]
public sealed class CommandAndConfigDriftGuardTests
{
    private static readonly Regex HelpCommandRegex = new(
        @"^\s*(?<cmd>/[a-z0-9-]+)",
        RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(500));

    private static readonly Regex ConfigKeyRegex = new(
        @"^\s{2}(?<key>[a-z_]+):",
        RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(500));

    [Fact]
    public void CompletionCatalog_SnapshotHash_IsStable()
    {
        var payload = string.Join(
            '\n',
            SlashCommandCatalog.CompletionEntries
                .OrderBy(static e => e.Command, StringComparer.OrdinalIgnoreCase)
                .Select(static e => $"{e.Command}|{e.Description}"));

        var actualHash = ComputeSha256(payload);
        const string expectedHash = "B181B3033EBC1BF4B97CAFD2EDBAE796C0776180AD95239716162F36410641C5";

        Assert.True(
            string.Equals(actualHash, expectedHash, StringComparison.Ordinal),
            $"Slash command snapshot drifted. Expected={expectedHash}, Actual={actualHash}.");
    }

    [Fact]
    public async Task CompletionCatalog_Entries_AreDispatchable()
    {
        var router = CreateRouter();
        var commands = SlashCommandCatalog.CompletionEntries
            .Select(static e => e.Command)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var command in commands)
        {
            var result = await router.ExecuteAsync(command);
            if (string.Equals(command, "/quit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(command, "/exit", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Null(result);
                continue;
            }

            Assert.NotNull(result);
            Assert.DoesNotContain("Unknown command", result, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task HelpCommands_AreDispatchable_AndMappedInRouterSwitch()
    {
        var router = CreateRouter();
        var helpText = await router.ExecuteAsync("/help");
        Assert.NotNull(helpText);

        var dispatchRoots = ExtractDispatchRootsFromRouterSource();
        var helpCommands = HelpCommandRegex
            .Matches(helpText)
            .Select(static m => m.Groups["cmd"].Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.NotEmpty(helpCommands);

        foreach (var command in helpCommands)
        {
            Assert.Contains(command, dispatchRoots);

            var result = await router.ExecuteAsync(command);
            if (string.Equals(command, "/quit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(command, "/exit", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Null(result);
                continue;
            }

            Assert.NotNull(result);
            Assert.DoesNotContain("Unknown command", result, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ConfigKeys_SnapshotHash_IsStable_AndRoundTripWorks()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-guardrail-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        DataDirectories.SetRoot(tempDirectory);

        try
        {
            var router = CreateRouter();

            var list = await router.ExecuteAsync("/config");
            Assert.NotNull(list);

            var keys = ConfigKeyRegex
                .Matches(list)
                .Select(static m => m.Groups["key"].Value.ToUpperInvariant())
                .Where(static key => !string.Equals(key, "USAGE", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.NotEmpty(keys);

            var keyPayload = string.Join('\n', keys);
            var actualHash = ComputeSha256(keyPayload);
            const string expectedHash = "B8C443F292273C333367143F1277BEC1AF74BC85EF8B83E067AFC3B40734378D";

            Assert.True(
                string.Equals(actualHash, expectedHash, StringComparison.Ordinal),
                $"Config key snapshot drifted. Expected={expectedHash}, Actual={actualHash}.");

            foreach (var key in keys)
            {
                var getResult = await router.ExecuteAsync($"/config get {key}");
                Assert.NotNull(getResult);
                Assert.DoesNotContain("Unknown config key", getResult, StringComparison.OrdinalIgnoreCase);
            }

            var setSamples = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["theme"] = "monokai",
                ["vim_mode"] = "on",
                ["output_style"] = "rich",
                ["spinner_style"] = "normal",
                ["prompt_cache"] = "on",
                ["prompt_cache_ttl"] = "1h",
                ["autorun"] = "off",
                ["permissions"] = "on",
                ["plan_mode"] = "off",
            };

            foreach (var key in keys.Where(setSamples.ContainsKey))
            {
                var value = setSamples[key];
                var setResult = await router.ExecuteAsync($"/config set {key} {value}");
                Assert.NotNull(setResult);
                Assert.DoesNotContain("Unknown config key", setResult, StringComparison.OrdinalIgnoreCase);

                var getResult = await router.ExecuteAsync($"/config get {key}");
                Assert.NotNull(getResult);
                Assert.DoesNotContain("Unknown config key", getResult, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            DataDirectories.Reset();
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static SlashCommandRouter CreateRouter()
    {
        var registry = Substitute.For<IProviderRegistry>();
        registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderInfo>>([]));
        registry.GetModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderModelInfo>>([]));

        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        var session = new AgentSession(registry, kernel, model);
        return new SlashCommandRouter(session, registry);
    }

    private static HashSet<string> ExtractDispatchRootsFromRouterSource()
    {
        var routerPath = Path.Combine(GetRepoRoot(), "src", "JD.AI", "Commands", "SlashCommandRouter.cs");
        var source = File.ReadAllText(routerPath);

        var switchStart = source.IndexOf("return cmd switch", StringComparison.Ordinal);
        Assert.True(switchStart >= 0, "Could not locate command switch in SlashCommandRouter.");

        var switchEnd = source.IndexOf("};", switchStart, StringComparison.Ordinal);
        Assert.True(switchEnd > switchStart, "Could not locate end of command switch in SlashCommandRouter.");

        var switchBody = source[switchStart..switchEnd];
        var matches = Regex.Matches(
            switchBody,
            "\"(?<cmd>/[A-Z0-9-]+)\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(500));

        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in matches)
        {
            var cmd = match.Groups["cmd"].Value.ToUpperInvariant();
            if (cmd.StartsWith("/JDAI-", StringComparison.Ordinal))
                cmd = "/" + cmd[6..];

            roots.Add(cmd);
        }

        return roots;
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ComputeSha256(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
