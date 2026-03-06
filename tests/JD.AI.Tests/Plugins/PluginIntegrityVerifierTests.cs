using System.Security.Cryptography;
using FluentAssertions;
using JD.AI.Core.Plugins;
using JD.AI.Plugins.SDK;

namespace JD.AI.Tests.Plugins;

public sealed class PluginIntegrityVerifierTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), $"jdai-integrity-{Guid.NewGuid():N}");

    public PluginIntegrityVerifierTests() =>
        Directory.CreateDirectory(_tempDir);

    public void Dispose() =>
        Directory.Delete(_tempDir, recursive: true);

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static PluginManifest Manifest(string? sha256 = null) => new()
    {
        Id = "test-plugin",
        Name = "Test Plugin",
        Version = "1.0.0",
        EntryAssemblySha256 = sha256,
    };

    [Fact]
    public void NullManifest_Throws()
    {
        var act = () => PluginIntegrityVerifier.VerifyEntryAssemblyHash(null!, "file.dll");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NullPath_Throws()
    {
        var act = () => PluginIntegrityVerifier.VerifyEntryAssemblyHash(Manifest(), null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EmptyPath_Throws()
    {
        var act = () => PluginIntegrityVerifier.VerifyEntryAssemblyHash(Manifest(), "  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NullHash_Skips()
    {
        var path = WriteFile("plugin.dll", "dummy content");
        var act = () => PluginIntegrityVerifier.VerifyEntryAssemblyHash(Manifest(sha256: null), path);
        act.Should().NotThrow();
    }

    [Fact]
    public void EmptyHash_Skips()
    {
        var path = WriteFile("plugin.dll", "dummy content");
        var act = () => PluginIntegrityVerifier.VerifyEntryAssemblyHash(Manifest(sha256: "   "), path);
        act.Should().NotThrow();
    }

    [Fact]
    public void MatchingHash_Passes()
    {
        var path = WriteFile("plugin.dll", "hello world");
        var hash = ComputeSha256(path);

        var act = () => PluginIntegrityVerifier.VerifyEntryAssemblyHash(Manifest(sha256: hash), path);
        act.Should().NotThrow();
    }

    [Fact]
    public void MatchingHash_CaseInsensitive()
    {
        var path = WriteFile("plugin.dll", "test data");
        // Intentionally lowercase to test case-insensitive comparison
#pragma warning disable CA1308
        var hash = ComputeSha256(path).ToLowerInvariant();
#pragma warning restore CA1308

        var act = () => PluginIntegrityVerifier.VerifyEntryAssemblyHash(Manifest(sha256: hash), path);
        act.Should().NotThrow();
    }

    [Fact]
    public void MatchingHash_WithDashes_Normalized()
    {
        var path = WriteFile("plugin.dll", "dashed content");
        var hash = ComputeSha256(path);
        var dashed = string.Join("-", Enumerable.Range(0, hash.Length / 2)
            .Select(i => hash.Substring(i * 2, 2)));

        var act = () => PluginIntegrityVerifier.VerifyEntryAssemblyHash(Manifest(sha256: dashed), path);
        act.Should().NotThrow();
    }

    [Fact]
    public void MismatchedHash_Throws()
    {
        var path = WriteFile("plugin.dll", "real content");
        var fakeHash = "0000000000000000000000000000000000000000000000000000000000000000";

        var act = () => PluginIntegrityVerifier.VerifyEntryAssemblyHash(Manifest(sha256: fakeHash), path);
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*hash verification failed*");
    }
}
