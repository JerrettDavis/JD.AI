using System.Security.Cryptography;
using JD.AI.Core.Plugins;
using JD.AI.Plugins.SDK;

namespace JD.AI.Tests.Plugins;

public sealed class PluginIntegrityVerifierTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public PluginIntegrityVerifierTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private string WriteFile(byte[] content, string? name = null)
    {
        var path = Path.Combine(_tempDir, name ?? $"{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static string ComputeSha256Hex(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash);  // uppercase
    }

    private static PluginManifest MakeManifest(string? sha256 = null) => new()
    {
        Id = "test-plugin",
        Name = "Test Plugin",
        Version = "1.0.0",
        EntryAssemblySha256 = sha256,
    };

    // ── No hash configured (skip verification) ──────────────────────────────────

    [Fact]
    public void VerifyEntryAssemblyHash_NoHash_DoesNotThrow()
    {
        var manifest = MakeManifest(sha256: null);
        var path = WriteFile([0x00, 0x01, 0x02]);

        PluginIntegrityVerifier.VerifyEntryAssemblyHash(manifest, path);
    }

    [Fact]
    public void VerifyEntryAssemblyHash_EmptyHash_DoesNotThrow()
    {
        var manifest = MakeManifest(sha256: "");
        var path = WriteFile([0x00, 0x01, 0x02]);

        PluginIntegrityVerifier.VerifyEntryAssemblyHash(manifest, path);
    }

    [Fact]
    public void VerifyEntryAssemblyHash_WhitespaceHash_DoesNotThrow()
    {
        var manifest = MakeManifest(sha256: "   ");
        var path = WriteFile([0x00, 0x01, 0x02]);

        PluginIntegrityVerifier.VerifyEntryAssemblyHash(manifest, path);
    }

    // ── Correct hash ─────────────────────────────────────────────────────────────

    [Fact]
    public void VerifyEntryAssemblyHash_CorrectHash_DoesNotThrow()
    {
        var content = new byte[] { 0xAB, 0xCD, 0xEF, 0x01 };
        var sha256 = ComputeSha256Hex(content);
        var path = WriteFile(content);
        var manifest = MakeManifest(sha256);

        PluginIntegrityVerifier.VerifyEntryAssemblyHash(manifest, path);
    }

    [Fact]
    public void VerifyEntryAssemblyHash_CorrectHashLowercase_DoesNotThrow()
    {
        var content = new byte[] { 0x01, 0x02, 0x03 };
#pragma warning disable CA1308 // intentionally testing lowercase input (NormalizeHex calls ToUpperInvariant)
        var sha256 = ComputeSha256Hex(content).ToLowerInvariant();
#pragma warning restore CA1308
        var path = WriteFile(content);
        var manifest = MakeManifest(sha256);

        PluginIntegrityVerifier.VerifyEntryAssemblyHash(manifest, path);
    }

    [Fact]
    public void VerifyEntryAssemblyHash_HashWithDashes_NormalizedAndAccepted()
    {
        var content = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var hash = SHA256.HashData(content);
        var dashHex = BitConverter.ToString(hash);  // e.g. "AB-CD-EF..."
        var path = WriteFile(content);
        var manifest = MakeManifest(dashHex);

        PluginIntegrityVerifier.VerifyEntryAssemblyHash(manifest, path);
    }

    [Fact]
    public void VerifyEntryAssemblyHash_HashWithLeadingWhitespace_NormalizedAndAccepted()
    {
        var content = new byte[] { 0x10, 0x20, 0x30 };
        var sha256 = "  " + ComputeSha256Hex(content) + "  ";
        var path = WriteFile(content);
        var manifest = MakeManifest(sha256);

        PluginIntegrityVerifier.VerifyEntryAssemblyHash(manifest, path);
    }

    // ── Wrong hash ───────────────────────────────────────────────────────────────

    [Fact]
    public void VerifyEntryAssemblyHash_WrongHash_ThrowsInvalidDataException()
    {
        var content = new byte[] { 0x01, 0x02, 0x03 };
        var wrongHash = new string('A', 64);  // 64 hex chars, but wrong
        var path = WriteFile(content);
        var manifest = MakeManifest(wrongHash);

        var ex = Assert.Throws<InvalidDataException>(() =>
            PluginIntegrityVerifier.VerifyEntryAssemblyHash(manifest, path));

        Assert.Contains("test-plugin", ex.Message);
        Assert.Contains("hash verification failed", ex.Message);
    }

    [Fact]
    public void VerifyEntryAssemblyHash_ModifiedFile_ThrowsInvalidDataException()
    {
        var original = new byte[] { 0x01, 0x02, 0x03 };
        var sha256 = ComputeSha256Hex(original);

        // Write different bytes to the file
        var path = WriteFile([0x04, 0x05, 0x06]);
        var manifest = MakeManifest(sha256);

        Assert.Throws<InvalidDataException>(() =>
            PluginIntegrityVerifier.VerifyEntryAssemblyHash(manifest, path));
    }

    // ── Guard clauses ────────────────────────────────────────────────────────────

    [Fact]
    public void VerifyEntryAssemblyHash_NullManifest_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PluginIntegrityVerifier.VerifyEntryAssemblyHash(null!, "some.dll"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void VerifyEntryAssemblyHash_NullOrWhitespaceAssemblyPath_Throws(string path)
    {
        var manifest = MakeManifest(sha256: null);
        Assert.Throws<ArgumentException>(() =>
            PluginIntegrityVerifier.VerifyEntryAssemblyHash(manifest, path));
    }
}
