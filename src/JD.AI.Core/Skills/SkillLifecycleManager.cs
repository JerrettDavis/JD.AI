using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Serialization;

namespace JD.AI.Core.Skills;

/// <summary>
/// Discovers SKILL.md files, resolves precedence, evaluates eligibility gates,
/// and provides per-run scoped environment injection.
/// </summary>
public sealed partial class SkillLifecycleManager : IDisposable
{
}
