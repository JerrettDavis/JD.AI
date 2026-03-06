namespace JD.AI.Core.Security;

/// <summary>
/// A curated library of regex patterns for detecting common secret types.
/// These patterns complement custom patterns loaded from policy YAML files.
/// </summary>
public static class SecretPatternLibrary
{
    /// <summary>AWS access key IDs (AKIA…).</summary>
    public const string AwsAccessKeyId = @"AKIA[0-9A-Z]{16}";

    /// <summary>AWS secret access key (40 alphanumeric chars after typical env-var assignments).</summary>
    public const string AwsSecretAccessKey = @"(?i)aws_secret_access_key\s*=\s*[0-9a-zA-Z/+]{40}";

    /// <summary>GitHub fine-grained personal access tokens (github_pat_…).</summary>
    public const string GitHubFineGrainedPat = @"github_pat_[a-zA-Z0-9]{22}_[a-zA-Z0-9]{59}";

    /// <summary>GitHub classic personal access tokens (ghp_…).</summary>
    public const string GitHubClassicPat = @"ghp_[a-zA-Z0-9]{36}";

    /// <summary>GitHub OAuth tokens (gho_…).</summary>
    public const string GitHubOAuthToken = @"gho_[a-zA-Z0-9]{36}";

    /// <summary>GitHub Actions runner tokens (ghs_…).</summary>
    public const string GitHubActionsToken = @"ghs_[a-zA-Z0-9]{36}";

    /// <summary>OpenAI API keys (sk-…).</summary>
    public const string OpenAiKey = @"sk-[a-zA-Z0-9]{40,}";

    /// <summary>Anthropic API keys (sk-ant-…).</summary>
    public const string AnthropicKey = @"sk-ant-[a-zA-Z0-9\-_]{93}";

    /// <summary>HuggingFace access tokens (hf_…).</summary>
    public const string HuggingFaceToken = @"hf_[a-zA-Z0-9]{36,}";

    /// <summary>Stripe secret keys (sk_live_…, sk_test_…).</summary>
    public const string StripeSecretKey = @"sk_(?:live|test)_[a-zA-Z0-9]{24,}";

    /// <summary>JSON Web Tokens (three Base64url segments separated by dots).</summary>
    public const string Jwt = @"eyJ[a-zA-Z0-9\-_]+\.eyJ[a-zA-Z0-9\-_]+\.[a-zA-Z0-9\-_]+";

    /// <summary>PEM private key headers (RSA, EC, PKCS8, etc.).</summary>
    public const string PemPrivateKey = @"-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----";

    /// <summary>Generic high-entropy base64 strings often used as API secrets (≥32 chars).</summary>
    public const string GenericBase64Secret = @"(?i)(?:api[_\-]?key|secret|token|password|passwd|pwd)\s*[=:]\s*[a-zA-Z0-9+/]{32,}={0,2}";

    /// <summary>Database connection strings containing passwords.</summary>
    public const string DatabaseConnectionString =
        @"(?i)(?:password|pwd)\s*=\s*[^;""'\s]{8,}";

    /// <summary>
    /// Returns the full set of built-in secret detection patterns.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        AwsAccessKeyId,
        AwsSecretAccessKey,
        GitHubFineGrainedPat,
        GitHubClassicPat,
        GitHubOAuthToken,
        GitHubActionsToken,
        OpenAiKey,
        AnthropicKey,
        HuggingFaceToken,
        StripeSecretKey,
        Jwt,
        PemPrivateKey,
        GenericBase64Secret,
        DatabaseConnectionString,
    ];

    /// <summary>
    /// Returns a subset of high-confidence patterns with minimal false positives,
    /// suitable for blocking actions (vs. audit-only scanning).
    /// </summary>
    public static IReadOnlyList<string> HighConfidence { get; } =
    [
        AwsAccessKeyId,
        GitHubFineGrainedPat,
        GitHubClassicPat,
        GitHubOAuthToken,
        GitHubActionsToken,
        OpenAiKey,
        AnthropicKey,
        HuggingFaceToken,
        StripeSecretKey,
        PemPrivateKey,
    ];
}
