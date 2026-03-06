using JD.AI.Core.Governance;

namespace JD.AI.Commands;

/// <summary>
/// Handles <c>jdai policy compliance ...</c> CLI commands for compliance reporting.
/// </summary>
internal static class PolicyComplianceCliHandler
{
    public static async Task<int> RunAsync(string[] args)
    {
        var sub = args.Length > 0 ? args[0].ToLowerInvariant() : "list";

        try
        {
            return sub switch
            {
                "list" => ListPresets(),
                "check" => await CheckAsync(args[1..]).ConfigureAwait(false),
                "help" or "--help" or "-h" => PrintHelp(),
                _ => PrintUnknown(sub),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Policy compliance command failed: {ex.Message}");
            return 1;
        }
    }

    // ── list ──────────────────────────────────────────────────────────────

    private static int ListPresets()
    {
        Console.WriteLine("Built-in compliance presets:");
        foreach (var name in CompliancePresetLoader.AvailablePresets.OrderBy(n => n))
        {
            Console.WriteLine($"  {name}");
        }
        Console.WriteLine();
        Console.WriteLine("Use in a policy YAML with:");
        Console.WriteLine("  spec:");
        Console.WriteLine("    extends: jdai/compliance/soc2");
        return 0;
    }

    // ── check ─────────────────────────────────────────────────────────────

    private static async Task<int> CheckAsync(string[] args)
    {
        // jdai policy compliance check --profile soc2 [--policy-dir ./policies]
        var profile = GetFlag(args, "--profile");
        var policyDir = GetFlag(args, "--policy-dir") ?? Directory.GetCurrentDirectory();

        if (profile is null)
        {
            Console.Error.WriteLine("Error: --profile is required. Example: --profile jdai/compliance/soc2");
            Console.Error.WriteLine("Run 'jdai policy compliance list' to see available presets.");
            return 1;
        }

        // Normalize short names e.g. "soc2" → "jdai/compliance/soc2"
        if (!profile.StartsWith("jdai/", StringComparison.OrdinalIgnoreCase))
            profile = "jdai/compliance/" + profile;

        var policies = PolicyLoader.Load(policyDir);
        var resolvedSpec = policies.Count > 0
            ? PolicyResolver.Resolve(policies)
            : new PolicySpec();

        var controls = CompliancePresetLoader.Check(profile, resolvedSpec);

        if (controls.Count == 0)
        {
            Console.Error.WriteLine($"Unknown or unsupported profile: '{profile}'");
            Console.Error.WriteLine("Run 'jdai policy compliance list' to see available presets.");
            return 1;
        }

        var pass = controls.Count(c => c.Pass);
        var fail = controls.Count - pass;

        Console.WriteLine($"Compliance Check — {profile}");
        Console.WriteLine(new string('─', 60));
        foreach (var control in controls)
        {
            var icon = control.Pass ? "✓" : "✗";
            Console.WriteLine($"  {icon} [{control.Id}] {control.Description}");
            if (!control.Pass && control.Remediation is not null)
                Console.WriteLine($"      Remediation: {control.Remediation}");
        }
        Console.WriteLine(new string('─', 60));
        Console.WriteLine($"Result: {pass}/{controls.Count} controls passing" +
                          (fail > 0 ? $" — {fail} FAILING" : " — ALL PASS"));

        await Task.CompletedTask.ConfigureAwait(false);
        return fail > 0 ? 1 : 0;
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static string? GetFlag(string[] args, string flag)
    {
        var idx = Array.IndexOf(args, flag);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            jdai policy compliance — compliance profile reporting

            Usage:
              jdai policy compliance list
              jdai policy compliance check --profile <preset> [--policy-dir <dir>]

            Presets:
              jdai/compliance/soc2     SOC 2 Type II baseline
              jdai/compliance/gdpr     GDPR PII protection
              jdai/compliance/hipaa    HIPAA PHI protection
              jdai/compliance/pci-dss  PCI-DSS cardholder data

            Examples:
              jdai policy compliance list
              jdai policy compliance check --profile soc2
              jdai policy compliance check --profile gdpr --policy-dir ./policies
            """);
        return 0;
    }

    private static int PrintUnknown(string sub)
    {
        Console.Error.WriteLine($"Unknown compliance command: '{sub}'. Run 'jdai policy compliance help' for usage.");
        return 1;
    }
}
