namespace JD.AI.Core.Skills;

public sealed partial class SkillLifecycleManager
{
    private SkillSnapshot BuildSnapshot(SkillRuntimeConfig runtimeConfig, string fingerprint)
    {
        var discovered = DiscoverCandidates();
        var statuses = new List<SkillStatus>();
        var active = new List<ActiveSkill>();

        foreach (var group in discovered.GroupBy(GetGroupingName, KeyComparer))
        {
            var validCandidates = group.Where(c => c.Metadata is not null).ToList();

            foreach (var invalid in group.Where(c => c.Metadata is null))
            {
                statuses.Add(new SkillStatus(
                    Name: invalid.InferredName,
                    SkillKey: invalid.InferredName,
                    SkillFilePath: invalid.SkillFilePath,
                    Source: invalid.Source,
                    State: SkillEligibilityState.Invalid,
                    ReasonCode: invalid.ReasonCode,
                    ReasonDetail: invalid.ReasonDetail));
            }

            if (validCandidates.Count == 0)
                continue;

            var ordered = validCandidates
                .OrderByDescending(c => GetTier(c.Source.Kind))
                .ThenByDescending(c => c.Source.Order)
                .ThenBy(c => c.SkillFilePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var winner = ordered[0];
            foreach (var shadowed in ordered.Skip(1))
            {
                statuses.Add(new SkillStatus(
                    Name: shadowed.Metadata!.Name,
                    SkillKey: shadowed.Metadata.SkillKey,
                    SkillFilePath: shadowed.SkillFilePath,
                    Source: shadowed.Source,
                    State: SkillEligibilityState.Shadowed,
                    ReasonCode: SkillReasonCodes.ShadowedByPrecedence,
                    ReasonDetail: $"Overridden by {winner.Metadata!.Name} from {winner.Source.Name}"));
            }

            var eligibility = EvaluateEligibility(winner, runtimeConfig);
            statuses.Add(eligibility.Status);
            if (eligibility.Active is not null)
                active.Add(eligibility.Active);
        }

        return new SkillSnapshot(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Fingerprint: fingerprint,
            ActiveSkills: active
                .OrderBy(s => s.Name, KeyComparer)
                .ThenByDescending(s => GetTier(s.Source.Kind))
                .ThenByDescending(s => s.Source.Order)
                .ToArray(),
            Statuses: statuses
                .OrderBy(s => s.Name, KeyComparer)
                .ThenByDescending(s => GetTier(s.Source.Kind))
                .ThenByDescending(s => s.Source.Order)
                .ToArray());
    }

    private EligibilityResult EvaluateEligibility(DiscoveredSkill candidate, SkillRuntimeConfig runtimeConfig)
    {
        var metadata = candidate.Metadata!;
        var entry = runtimeConfig.GetEntry(metadata.SkillKey);

        if (entry.Enabled == false)
            return Excluded(metadata, candidate, SkillReasonCodes.DisabledByConfig, "skills.entries.<key>.enabled=false");

        if (candidate.Source.Kind == SkillSourceKind.Bundled &&
            runtimeConfig.AllowBundled.Count > 0 &&
            !runtimeConfig.AllowBundled.Contains(metadata.Name) &&
            !runtimeConfig.AllowBundled.Contains(metadata.SkillKey))
        {
            return Excluded(metadata, candidate, SkillReasonCodes.BundledNotAllowlisted, "Not present in skills.allowBundled");
        }

        if (!metadata.Always)
        {
            if (metadata.Os.Count > 0)
            {
                var platform = _platformProvider();
                if (!metadata.Os.Contains(platform, StringComparer.OrdinalIgnoreCase))
                    return Excluded(metadata, candidate, SkillReasonCodes.OsMismatch, $"Requires [{string.Join(", ", metadata.Os)}], current={platform}");
            }

            var missingBins = metadata.RequiredBins.Where(bin => !_binaryExists(bin)).ToArray();
            if (missingBins.Length > 0)
                return Excluded(metadata, candidate, SkillReasonCodes.MissingBinaries, string.Join(", ", missingBins));

            if (metadata.RequiredAnyBins.Count > 0 && metadata.RequiredAnyBins.All(bin => !_binaryExists(bin)))
            {
                return Excluded(metadata, candidate, SkillReasonCodes.MissingAnyBinary,
                    string.Join(" OR ", metadata.RequiredAnyBins));
            }

            var missingEnv = metadata.RequiredEnvironment
                .Where(name => !IsEnvironmentSatisfied(name, metadata, entry))
                .ToArray();
            if (missingEnv.Length > 0)
                return Excluded(metadata, candidate, SkillReasonCodes.MissingEnvironment, string.Join(", ", missingEnv));

            var missingConfig = metadata.RequiredConfigPaths
                .Where(path => !IsConfigSatisfied(path, runtimeConfig.RootConfig, entry.Config))
                .ToArray();
            if (missingConfig.Length > 0)
                return Excluded(metadata, candidate, SkillReasonCodes.MissingConfig, string.Join(", ", missingConfig));
        }

        var active = new ActiveSkill(
            Name: metadata.Name,
            SkillKey: metadata.SkillKey,
            DirectoryPath: candidate.DirectoryPath,
            SkillFilePath: candidate.SkillFilePath,
            Source: candidate.Source,
            Metadata: metadata);

        return new EligibilityResult(
            Active: active,
            Status: new SkillStatus(
                Name: metadata.Name,
                SkillKey: metadata.SkillKey,
                SkillFilePath: candidate.SkillFilePath,
                Source: candidate.Source,
                State: SkillEligibilityState.Active,
                ReasonCode: SkillReasonCodes.None,
                ReasonDetail: null));
    }

    private static EligibilityResult Excluded(
        SkillMetadata metadata,
        DiscoveredSkill candidate,
        string reasonCode,
        string detail)
    {
        return new EligibilityResult(
            Active: null,
            Status: new SkillStatus(
                Name: metadata.Name,
                SkillKey: metadata.SkillKey,
                SkillFilePath: candidate.SkillFilePath,
                Source: candidate.Source,
                State: SkillEligibilityState.Excluded,
                ReasonCode: reasonCode,
                ReasonDetail: detail));
    }

    private IEnumerable<DiscoveredSkill> DiscoverCandidates()
    {
        foreach (var source in _sources)
        {
            if (!Directory.Exists(source.RootPath))
                continue;

            foreach (var skillFile in Directory.EnumerateFiles(source.RootPath, "SKILL.md", SearchOption.AllDirectories))
            {
                var directory = Path.GetDirectoryName(skillFile)!;
                var inferredName = Path.GetFileName(directory);
                string? markdown = null;
                string? readError = null;
                try
                {
                    markdown = File.ReadAllText(skillFile);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    readError = "Unable to read SKILL.md";
                }

                if (readError is not null)
                {
                    yield return new DiscoveredSkill(
                        inferredName,
                        skillFile,
                        directory,
                        source,
                        null,
                        SkillReasonCodes.InvalidFrontmatter,
                        readError);
                    continue;
                }

                if (!TryParseMetadata(markdown!, out var metadata, out var reasonCode, out var reasonDetail))
                {
                    yield return new DiscoveredSkill(inferredName, skillFile, directory, source, null, reasonCode, reasonDetail);
                    continue;
                }

                yield return new DiscoveredSkill(inferredName, skillFile, directory, source, metadata, SkillReasonCodes.None, null);
            }
        }
    }

    private static string GetGroupingName(DiscoveredSkill candidate) => candidate.Metadata?.Name ?? candidate.InferredName;

    private sealed record DiscoveredSkill(
        string InferredName,
        string SkillFilePath,
        string DirectoryPath,
        SkillSourceDirectory Source,
        SkillMetadata? Metadata,
        string ReasonCode,
        string? ReasonDetail);

    private sealed record EligibilityResult(ActiveSkill? Active, SkillStatus Status);
}
