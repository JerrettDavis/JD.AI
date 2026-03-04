using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

public sealed class AgentLoopFallbackTests
{
    private static readonly System.Reflection.MethodInfo IsRetriableErrorMethod =
        typeof(AgentLoop).GetMethod(
            "IsRetriableError",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
        ?? throw new InvalidOperationException("IsRetriableError method not found");

    private static bool InvokeIsRetriableError(Exception ex) =>
        (bool)IsRetriableErrorMethod.Invoke(null, [ex])!;

    [Fact]
    public void IsRetriableError_HttpRequestException429_ReturnsTrue()
    {
        var ex = new HttpRequestException("Too Many Requests", null, System.Net.HttpStatusCode.TooManyRequests);
        Assert.True(InvokeIsRetriableError(ex));
    }

    [Fact]
    public void IsRetriableError_HttpRequestException503_ReturnsTrue()
    {
        var ex = new HttpRequestException("Service Unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable);
        Assert.True(InvokeIsRetriableError(ex));
    }

    [Fact]
    public void IsRetriableError_TimeoutException_ReturnsTrue()
    {
        var ex = new TimeoutException("Request timed out");
        Assert.True(InvokeIsRetriableError(ex));
    }

    [Fact]
    public void IsRetriableError_HttpRequestException500_ReturnsTrue()
    {
        var ex = new HttpRequestException("Internal Server Error", null, System.Net.HttpStatusCode.InternalServerError);
        Assert.True(InvokeIsRetriableError(ex));
    }

    [Fact]
    public void IsRetriableError_InnerHttpException500_ReturnsTrue()
    {
        var inner = new HttpRequestException("500", null, System.Net.HttpStatusCode.InternalServerError);
        var ex = new InvalidOperationException("Wrapper", inner);
        Assert.True(InvokeIsRetriableError(ex));
    }

    [Fact]
    public void IsRetriableError_ModelFieldRequired_ReturnsTrue()
    {
        var ex = new InvalidOperationException(
            "{\"type\":\"error\",\"error\":{\"type\":\"invalid_request_error\",\"message\":\"model: Field required\"}}");
        Assert.True(InvokeIsRetriableError(ex));
    }

    [Fact]
    public void IsRetriableError_GenericException_ReturnsFalse()
    {
        var ex = new InvalidOperationException("Something else");
        Assert.False(InvokeIsRetriableError(ex));
    }

    [Fact]
    public void IsRetriableError_MessageContainsRateLimit_ReturnsTrue()
    {
        var ex = new InvalidOperationException("rate limit exceeded");
        Assert.True(InvokeIsRetriableError(ex));
    }

    [Fact]
    public void IsRetriableError_InnerHttpException429_ReturnsTrue()
    {
        var inner = new HttpRequestException("429", null, System.Net.HttpStatusCode.TooManyRequests);
        var ex = new InvalidOperationException("Wrapper", inner);
        Assert.True(InvokeIsRetriableError(ex));
    }
}
