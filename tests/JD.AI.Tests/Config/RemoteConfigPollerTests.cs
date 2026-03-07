using FluentAssertions;
using JD.AI.Core.Config;

namespace JD.AI.Tests.Config;

public sealed class RemoteConfigPollerTests
{
    [Fact]
    public async Task PollOnceAsync_WithChangedSource_InvokesCallback()
    {
        var callbackInvoked = false;
        string? receivedSource = null;
        string? receivedContent = null;

        var source = new InMemoryConfigSource("test-source", "new content", "v1");
        var poller = new RemoteConfigPoller(
            [source],
            (name, content) =>
            {
                callbackInvoked = true;
                receivedSource = name;
                receivedContent = content;
                return Task.CompletedTask;
            });

        await poller.PollOnceAsync();

        callbackInvoked.Should().BeTrue();
        receivedSource.Should().Be("test-source");
        receivedContent.Should().Be("new content");
    }

    [Fact]
    public async Task PollOnceAsync_SameVersion_DoesNotInvokeCallback()
    {
        var callCount = 0;
        var source = new InMemoryConfigSource("src", "content", "v1");
        var poller = new RemoteConfigPoller(
            [source],
            (_, _) => { callCount++; return Task.CompletedTask; });

        await poller.PollOnceAsync(); // First poll — triggers callback
        await poller.PollOnceAsync(); // Second poll — same version, no callback

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task PollOnceAsync_VersionChanged_InvokesCallbackAgain()
    {
        var callCount = 0;
        var source = new InMemoryConfigSource("src", "content-v1", "v1");
        var poller = new RemoteConfigPoller(
            [source],
            (_, _) => { callCount++; return Task.CompletedTask; });

        await poller.PollOnceAsync();

        source.SetResult("content-v2", "v2");
        await poller.PollOnceAsync();

        callCount.Should().Be(2);
    }

    [Fact]
    public async Task PollOnceAsync_NullResult_SkipsCallback()
    {
        var callCount = 0;
        var source = new InMemoryConfigSource("src", null, null);
        var poller = new RemoteConfigPoller(
            [source],
            (_, _) => { callCount++; return Task.CompletedTask; });

        await poller.PollOnceAsync();

        callCount.Should().Be(0);
    }

    [Fact]
    public void SourceCount_ReturnsCorrectCount()
    {
        var sources = new[]
        {
            new InMemoryConfigSource("a", "c", "v"),
            new InMemoryConfigSource("b", "c", "v"),
        };
        var poller = new RemoteConfigPoller(sources, (_, _) => Task.CompletedTask);

        poller.SourceCount.Should().Be(2);
    }

    [Fact]
    public void Start_SetsIsRunning()
    {
        var source = new InMemoryConfigSource("src", null, null);
        using var poller = new RemoteConfigPoller(
            [source],
            (_, _) => Task.CompletedTask,
            pollInterval: TimeSpan.FromHours(1));

        poller.Start();

        poller.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_StopsPoller()
    {
        var source = new InMemoryConfigSource("src", null, null);
        var poller = new RemoteConfigPoller(
            [source],
            (_, _) => Task.CompletedTask,
            pollInterval: TimeSpan.FromHours(1));

        poller.Start();
        poller.Dispose();

        for (var i = 0; i < 20 && poller.IsRunning; i++)
            await Task.Delay(50);
        poller.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task PollOnceAsync_MultipleSources_PollsAll()
    {
        var received = new List<string>();
        var sources = new[]
        {
            new InMemoryConfigSource("source-a", "content-a", "v1"),
            new InMemoryConfigSource("source-b", "content-b", "v1"),
        };
        var poller = new RemoteConfigPoller(
            sources,
            (name, _) => { received.Add(name); return Task.CompletedTask; });

        await poller.PollOnceAsync();

        received.Should().Contain("source-a").And.Contain("source-b");
    }

    /// <summary>Simple in-memory config source for testing.</summary>
    private sealed class InMemoryConfigSource : IRemoteConfigSource
    {
        private string? _content;
        private string? _version;

        public string Name { get; }

        public InMemoryConfigSource(string name, string? content, string? version)
        {
            Name = name;
            _content = content;
            _version = version;
        }

        public void SetResult(string? content, string? version)
        {
            _content = content;
            _version = version;
        }

        public Task<RemoteConfigResult?> FetchAsync(CancellationToken ct = default)
        {
            if (_content is null)
                return Task.FromResult<RemoteConfigResult?>(null);

            return Task.FromResult<RemoteConfigResult?>(new RemoteConfigResult
            {
                Content = _content,
                Version = _version,
            });
        }
    }
}
