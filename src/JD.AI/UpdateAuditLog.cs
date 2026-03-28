using System.Text.Json;
using JD.AI.Core.Config;
using JD.AI.Core.Infrastructure;

namespace JD.AI;

internal static class UpdateAuditLog
{
    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Options;
    private static string AuditDir => Path.Combine(DataDirectories.Root, "audit");
    private static string AuditFile => Path.Combine(AuditDir, $"update-{DateTime.UtcNow:yyyy-MM-dd}.jsonl");

    public static async Task WriteAsync(string action, object detail, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            timestamp = DateTimeOffset.UtcNow,
            action,
            detail,
        }, JsonOptions);

        Directory.CreateDirectory(AuditDir);
        await File.AppendAllTextAsync(AuditFile, payload + Environment.NewLine, ct).ConfigureAwait(false);
    }
}
