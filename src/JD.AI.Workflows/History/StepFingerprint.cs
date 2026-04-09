using System.Security.Cryptography;
using System.Text;

namespace JD.AI.Workflows.History;

public static class StepFingerprint
{
    public static string Compute(AgentStepKind kind, string? target, string name)
    {
        var canonical = $"{kind}|{target ?? ""}|{name}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(hash)[..16];
    }

    public static string Compute(AgentStepDefinition step) =>
        Compute(step.Kind, step.Target, step.Name);
}
