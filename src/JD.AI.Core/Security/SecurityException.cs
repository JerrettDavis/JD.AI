namespace JD.AI.Core.Security;

/// <summary>
/// Thrown when a security policy violation is detected — for example, when an outbound
/// request is found to contain secrets or when a prompt injection is identified.
/// </summary>
public sealed class SecurityException : Exception
{
    /// <inheritdoc/>
    public SecurityException()
    {
    }

    /// <inheritdoc/>
    public SecurityException(string message) : base(message)
    {
    }

    /// <inheritdoc/>
    public SecurityException(string message, Exception inner) : base(message, inner)
    {
    }
}
