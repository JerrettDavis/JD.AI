using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace JD.AI.Core.Skills;

public sealed partial class SkillLifecycleManager
{
    private string ComputeFingerprint(SkillRuntimeConfig runtimeConfig)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AddHash(hash, _platformProvider());
        AddHash(hash, runtimeConfig.Watch ? "watch:1" : "watch:0");
        AddHash(hash, $"debounce:{runtimeConfig.WatchDebounceMs}");

        foreach (var value in runtimeConfig.AllowBundled.OrderBy(v => v, KeyComparer))
            AddHash(hash, $"allow:{value}");

        foreach (var entry in runtimeConfig.Entries.OrderBy(e => e.Key, KeyComparer))
        {
            AddHash(hash, $"entry:{entry.Key}:enabled:{entry.Value.Enabled?.ToString() ?? "null"}");
            AddHash(hash, $"entry:{entry.Key}:apikey:{entry.Value.ApiKey ?? string.Empty}");

            foreach (var env in entry.Value.Env.OrderBy(e => e.Key, StringComparer.Ordinal))
                AddHash(hash, $"entry:{entry.Key}:env:{env.Key}={env.Value}");

            if (entry.Value.Config is not null)
                AddHash(hash, $"entry:{entry.Key}:config:{entry.Value.Config.ToJsonString()}");
        }

        if (runtimeConfig.RootConfig is not null)
            AddHash(hash, $"root:{runtimeConfig.RootConfig.ToJsonString()}");

        foreach (var source in _sources.OrderBy(s => GetTier(s.Kind)).ThenBy(s => s.Order))
        {
            AddHash(hash, $"source:{source.Kind}:{source.Order}:{source.RootPath}");
            if (!Directory.Exists(source.RootPath))
                continue;

            foreach (var file in Directory.EnumerateFiles(source.RootPath, "SKILL.md", SearchOption.AllDirectories)
                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                AddHash(hash, $"file:{Path.GetFullPath(file)}|{ComputeFileContentHash(file)}");
            }
        }

        foreach (DictionaryEntry env in _getEnvironmentVariables())
        {
            var name = env.Key?.ToString();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            AddHash(hash, $"env:{name}={env.Value}");
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AddHash(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(bytes);
    }

    private static string ComputeFileContentHash(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "io-error";
        }
    }

    private void EnsureWatchers(SkillRuntimeConfig runtimeConfig)
    {
        if (!runtimeConfig.Watch)
        {
            if (_watchersEnabled)
                DisposeWatchers();
            return;
        }

        if (_watchersEnabled && runtimeConfig.WatchDebounceMs == _watchDebounceMs)
            return;

        DisposeWatchers();

        _watchDebounceMs = runtimeConfig.WatchDebounceMs;

        foreach (var source in _sources)
        {
            if (Directory.Exists(source.RootPath))
            {
                TryAddWatcher(source.RootPath, "SKILL.md", includeSubdirectories: true);
            }
            else
            {
                var parent = Path.GetDirectoryName(source.RootPath);
                var leaf = Path.GetFileName(source.RootPath);
                if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent) && !string.IsNullOrWhiteSpace(leaf))
                    TryAddWatcher(parent, leaf, includeSubdirectories: false);
            }
        }

        foreach (var path in new[] { _userConfigPath, _workspaceConfigPath }.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var parent = Path.GetDirectoryName(path!);
            var file = Path.GetFileName(path!);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent) && !string.IsNullOrWhiteSpace(file))
                TryAddWatcher(parent, file, includeSubdirectories: false);
        }

        _watchersEnabled = true;
    }

    private void TryAddWatcher(string path, string filter, bool includeSubdirectories)
    {
        try
        {
            var watcher = new FileSystemWatcher(path, filter)
            {
                IncludeSubdirectories = includeSubdirectories,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };

            watcher.Changed += OnWatcherChanged;
            watcher.Created += OnWatcherChanged;
            watcher.Deleted += OnWatcherChanged;
            watcher.Renamed += OnWatcherChanged;
            _watchers.Add(watcher);
        }
#pragma warning disable CA1031
        catch
        {
            // Non-fatal: deterministic fingerprint checks still provide refresh safety.
        }
#pragma warning restore CA1031
    }

    private void OnWatcherChanged(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastWatchEventUtc < TimeSpan.FromMilliseconds(_watchDebounceMs))
                return;

            _lastWatchEventUtc = now;
        }
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();
        _watchersEnabled = false;
    }
}
