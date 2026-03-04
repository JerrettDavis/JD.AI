using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

public sealed class AgentLoopFallbackTests
{
    [Fact]
    public void IsRetriableError_HttpRequestException429_ReturnsTrue()
    {
        // Use reflection to test the private static method
        var method = typeof(AgentLoop).GetMethod("IsRetriableError",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var ex = new HttpRequestException("Too Many Requests", null, System.Net.HttpStatusCode.TooManyRequests);
        var result = (bool)method.Invoke(null, [ex])!;
        Assert.True(result);
    }

    [Fact]
    public void IsRetriableError_HttpRequestException503_ReturnsTrue()
    {
        var method = typeof(AgentLoop).GetMethod("IsRetriableError",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var ex = new HttpRequestException("Service Unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable);
        var result = (bool)method.Invoke(null, [ex])!;
        Assert.True(result);
    }

    [Fact]
    public void IsRetriableError_TimeoutException_ReturnsTrue()
    {
        var method = typeof(AgentLoop).GetMethod("IsRetriableError",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var ex = new TimeoutException("Request timed out");
        var result = (bool)method.Invoke(null, [ex])!;
        Assert.True(result);
    }

    [Fact]
    public void IsRetriableError_GenericException_ReturnsFalse()
    {
        var method = typeof(AgentLoop).GetMethod("IsRetriableError",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var ex = new InvalidOperationException("Something else");
        var result = (bool)method.Invoke(null, [ex])!;
        Assert.False(result);
    }

    [Fact]
    public void IsRetriableError_MessageContainsRateLimit_ReturnsTrue()
    {
        var method = typeof(AgentLoop).GetMethod("IsRetriableError",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var ex = new InvalidOperationException("rate limit exceeded");
        var result = (bool)method.Invoke(null, [ex])!;
        Assert.True(result);
    }

    [Fact]
    public void IsRetriableError_InnerHttpException429_ReturnsTrue()
    {
        var method = typeof(AgentLoop).GetMethod("IsRetriableError",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var inner = new HttpRequestException("429", null, System.Net.HttpStatusCode.TooManyRequests);
        var ex = new InvalidOperationException("Wrapper", inner);
        var result = (bool)method.Invoke(null, [ex])!;
        Assert.True(result);
    }
}
