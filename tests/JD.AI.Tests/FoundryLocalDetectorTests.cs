using System.Text.Json.Nodes;
using JD.AI.Core.Providers;
using Xunit;

namespace JD.AI.Tests;

public sealed class FoundryLocalDetectorTests
{
    [Fact]
    public void ProviderName_IsFoundryLocal()
    {
        var detector = new FoundryLocalDetector();
        Assert.Equal("Foundry Local", detector.ProviderName);
    }

    [Fact]
    public async Task DetectAsync_ReturnsResult_WithoutError()
    {
        // The SDK-based detector should not throw regardless of service state.
        // When the service isn't running, it returns IsAvailable=false.
        var detector = new FoundryLocalDetector();
        var result = await detector.DetectAsync();

        // We can't guarantee the service is running in CI,
        // but the call should always succeed without throwing
        Assert.NotNull(result);
        Assert.Equal("Foundry Local", result.Name);
    }

    // ── Schema fixup (Foundry Local rejects type arrays) ─────────

    [Fact]
    public void NormalizeTypeArrays_ConvertsArrayToString()
    {
        // Foundry Local rejects ["integer", "null"] — must become "integer"
        var node = JsonNode.Parse("""
            {
                "type": "object",
                "properties": {
                    "startLine": { "type": ["integer", "null"] },
                    "path": { "type": "string" }
                }
            }
            """)!;

        FoundryLocalDetector.FoundrySchemaFixupHandler.NormalizeTypeArrays(node);

        Assert.Equal("integer", node["properties"]!["startLine"]!["type"]!.GetValue<string>());
        Assert.Equal("string", node["properties"]!["path"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void NormalizeTypeArrays_HandlesNestedItems()
    {
        var node = JsonNode.Parse("""
            {
                "type": "object",
                "properties": {
                    "tags": {
                        "type": "array",
                        "items": { "type": ["string", "null"] }
                    }
                }
            }
            """)!;

        FoundryLocalDetector.FoundrySchemaFixupHandler.NormalizeTypeArrays(node);

        Assert.Equal("string", node["properties"]!["tags"]!["items"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void NormalizeTypeArrays_LeavesPlainTypesAlone()
    {
        var node = JsonNode.Parse("""{ "type": "string" }""")!;

        FoundryLocalDetector.FoundrySchemaFixupHandler.NormalizeTypeArrays(node);

        Assert.Equal("string", node["type"]!.GetValue<string>());
    }

    [Fact]
    public void NormalizeTypeArrays_NullOnlyArrayDefaultsToString()
    {
        var node = JsonNode.Parse("""{ "type": ["null"] }""")!;

        FoundryLocalDetector.FoundrySchemaFixupHandler.NormalizeTypeArrays(node);

        Assert.Equal("string", node["type"]!.GetValue<string>());
    }
}
