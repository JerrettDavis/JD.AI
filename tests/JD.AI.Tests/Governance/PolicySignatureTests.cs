using System.Text;
using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class PolicySignatureTests
{
    private static readonly byte[] TestKey = Encoding.UTF8.GetBytes("test-signing-key-32bytes-long!!");

    private const string SampleYaml = """
        apiVersion: jdai/v1
        kind: Policy
        metadata:
          name: test-policy
          scope: User
        spec: {}
        """;

    [Fact]
    public void Sign_AppendsSignatureLine()
    {
        var signed = PolicySignature.Sign(SampleYaml, TestKey);

        signed.Should().Contain("# jdai-signature: ");
        signed.Should().EndWith(Environment.NewLine);
    }

    [Fact]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        var signed = PolicySignature.Sign(SampleYaml, TestKey);

        PolicySignature.Verify(signed, TestKey).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongKey_ReturnsFalse()
    {
        var signed = PolicySignature.Sign(SampleYaml, TestKey);
        var wrongKey = Encoding.UTF8.GetBytes("wrong-key-also-needs-some-bytes!");

        PolicySignature.Verify(signed, wrongKey).Should().BeFalse();
    }

    [Fact]
    public void Verify_TamperedContent_ReturnsFalse()
    {
        var signed = PolicySignature.Sign(SampleYaml, TestKey);
        var tampered = signed.Replace("test-policy", "evil-policy");

        PolicySignature.Verify(tampered, TestKey).Should().BeFalse();
    }

    [Fact]
    public void Verify_UnsignedYaml_ReturnsFalse()
    {
        PolicySignature.Verify(SampleYaml, TestKey).Should().BeFalse();
    }

    [Fact]
    public void ExtractSignature_SignedYaml_ReturnsHex()
    {
        var signed = PolicySignature.Sign(SampleYaml, TestKey);

        var sig = PolicySignature.ExtractSignature(signed);

        sig.Should().NotBeNullOrEmpty();
        sig.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void ExtractSignature_UnsignedYaml_ReturnsNull()
    {
        PolicySignature.ExtractSignature(SampleYaml).Should().BeNull();
    }

    [Fact]
    public void StripSignature_RemovesSignatureLine()
    {
        var signed = PolicySignature.Sign(SampleYaml, TestKey);

        var stripped = PolicySignature.StripSignature(signed);

        stripped.Should().NotContain("# jdai-signature:");
    }

    [Fact]
    public void Sign_Idempotent_DoesNotStackSignatures()
    {
        var signed1 = PolicySignature.Sign(SampleYaml, TestKey);
        var signed2 = PolicySignature.Sign(signed1, TestKey);

        // Should have exactly one signature line
        var sigLines = signed2.Split('\n')
            .Count(l => l.StartsWith("# jdai-signature:", StringComparison.Ordinal));
        sigLines.Should().Be(1);

        PolicySignature.Verify(signed2, TestKey).Should().BeTrue();
    }

    [Fact]
    public void Sign_NullYaml_ThrowsArgumentNullException()
    {
        var act = () => PolicySignature.Sign(null!, TestKey);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Sign_NullKey_ThrowsArgumentNullException()
    {
        var act = () => PolicySignature.Sign(SampleYaml, null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
