using System.Net;
using System.Text;
using FluentAssertions;
using JD.AI.Core.Config;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

public sealed class WelcomeMotdProviderTests
{
    [Fact]
    public void NormalizeMotd_Null_ReturnsNull() =>
        WelcomeMotdProvider.NormalizeMotd(null, 100).Should().BeNull();

    [Fact]
    public void NormalizeMotd_ReturnsNull_WhenEmpty() =>
        WelcomeMotdProvider.NormalizeMotd("   ", 80).Should().BeNull();

    [Fact]
    public void NormalizeMotd_UsesFirstNonEmptyLine() =>
        WelcomeMotdProvider.NormalizeMotd(
            "\n\nHello operators\nSecond line", 80)
            .Should().Be("Hello operators");

    [Fact]
    public void NormalizeMotd_Truncates_WhenLongerThanMax()
    {
        var raw = "This is a very long message that should be truncated for the welcome panel.";
        var result = WelcomeMotdProvider.NormalizeMotd(raw, 32);

        result.Should().NotBeNull();
        result!.Length.Should().BeLessThanOrEqualTo(32);
        result.Should().EndWith("...");
    }

    [Fact]
    public void NormalizeMotd_TabsReplacedWithSpaces() =>
        WelcomeMotdProvider.NormalizeMotd("hello\tworld", 100)
            .Should().Be("hello world");

    [Fact]
    public void NormalizeMotd_ExactlyMaxLength_NoTruncation() =>
        WelcomeMotdProvider.NormalizeMotd("abcde", 5)
            .Should().Be("abcde");

    [Fact]
    public void NormalizeMotd_WindowsLineEndings_Handled() =>
        WelcomeMotdProvider.NormalizeMotd("line1\r\nline2\r\n", 100)
            .Should().Be("line1");

    [Fact]
    public void NormalizeMotd_OnlyWhitespaceLines_ReturnsNull() =>
        WelcomeMotdProvider.NormalizeMotd("\n  \n\t\n  ", 100)
            .Should().BeNull();

    [Fact]
    public async Task TryGetMotdAsync_WhenMotdDisabled_ReturnsNull()
    {
        var settings = CreateSettings(showMotd: false, motdUrl: "http://localhost");

        var result = await WelcomeMotdProvider.TryGetMotdAsync(settings);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryGetMotdAsync_WhenUriIsInvalid_ReturnsNull()
    {
        var settings = CreateSettings(motdUrl: "::not-a-uri::");

        var result = await WelcomeMotdProvider.TryGetMotdAsync(settings);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryGetMotdAsync_WhenUriSchemeIsUnsupported_ReturnsNull()
    {
        var settings = CreateSettings(motdUrl: "file:///tmp/motd.txt");

        var result = await WelcomeMotdProvider.TryGetMotdAsync(settings);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(300001)]
    public async Task TryGetMotdAsync_WhenTimeoutIsInvalid_ReturnsNull(int timeoutMs)
    {
        var settings = CreateSettings(motdUrl: "https://motd.test/", motdTimeoutMs: timeoutMs);

        var result = await WelcomeMotdProvider.TryGetMotdAsync(settings);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryGetMotdAsync_WhenFetchSucceeds_ReturnsNormalizedMotd()
    {
        using var http = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("\n  Welcome back operator  \nSecond line")
        });
        var settings = CreateSettings(motdUrl: "https://motd.test/");

        var result = await WelcomeMotdProvider.TryGetMotdAsync(settings, http);

        result.Should().Be("Welcome back operator");
    }

    [Fact]
    public async Task TryGetMotdAsync_WhenRequestTimesOut_ReturnsNull()
    {
        using var http = CreateHttpClient(_ => throw new TaskCanceledException("timeout"));
        var settings = CreateSettings(motdUrl: "https://motd.test/", motdTimeoutMs: 100);

        var result = await WelcomeMotdProvider.TryGetMotdAsync(settings, http);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryGetMotdAsync_WhenServerReturnsError_ReturnsNull()
    {
        using var http = CreateHttpClient(_ => throw new HttpRequestException("boom"));
        var settings = CreateSettings(motdUrl: "https://motd.test/");

        var result = await WelcomeMotdProvider.TryGetMotdAsync(settings, http);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryGetMotdAsync_WhenResponseIsOversized_TruncatesNormalizedMotd()
    {
        var oversized = "  " + new string('x', 400);
        using var http = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(oversized)
        });
        var settings = CreateSettings(motdUrl: "https://motd.test/", motdMaxLength: 60);

        var result = await WelcomeMotdProvider.TryGetMotdAsync(settings, http);

        result.Should().NotBeNull();
        result!.Length.Should().BeLessThanOrEqualTo(60);
        result.Should().EndWith("...");
    }

    [Fact]
    public async Task TryGetMotdAsync_WhenMaxLengthIsTooSmall_ReturnsClampedResult()
    {
        using var http = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("  hello world  ")
        });
        var settings = CreateSettings(motdUrl: "https://motd.test/", motdMaxLength: 1);

        var result = await WelcomeMotdProvider.TryGetMotdAsync(settings, http);

        result.Should().Be("h...");
    }

    [Fact]
    public async Task TryGetMotdAsync_WhenMaxLengthExceedsFiveHundredTwelve_DoesNotClampToLegacyLimit()
    {
        var longLine = new string('x', 700);
        using var http = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(longLine)
        });
        var settings = CreateSettings(motdUrl: "https://motd.test/", motdMaxLength: 800);

        var result = await WelcomeMotdProvider.TryGetMotdAsync(settings, http);

        result.Should().Be(longLine);
    }

    [Fact]
    public async Task TryGetMotdAsync_WhenUtf8CharactersSpanChunks_PreservesText()
    {
        using var http = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new ChunkedReadStream(Encoding.UTF8.GetBytes("  héllo operators  "), 1))
        });
        var settings = CreateSettings(motdUrl: "https://motd.test/");

        var result = await WelcomeMotdProvider.TryGetMotdAsync(settings, http);

        result.Should().Be("héllo operators");
    }

    [Fact]
    public async Task TryGetMotdAsync_WhenCancellationIsRequested_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var http = CreateHttpClient(_ => throw new OperationCanceledException(cts.Token));
        var settings = CreateSettings(motdUrl: "https://motd.test/");

        Func<Task> act = () => WelcomeMotdProvider.TryGetMotdAsync(settings, http, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static WelcomePanelSettings CreateSettings(
        bool showMotd = true,
        string? motdUrl = null,
        int motdTimeoutMs = 700,
        int motdMaxLength = 160) =>
        new()
        {
            ShowMotd = showMotd,
            MotdUrl = motdUrl,
            MotdTimeoutMs = motdTimeoutMs,
            MotdMaxLength = motdMaxLength,
        };

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new StubHttpMessageHandler(responder));

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class ChunkedReadStream(byte[] bytes, int chunkSize) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.Length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= bytes.Length)
                return 0;

            var read = Math.Min(Math.Min(count, chunkSize), bytes.Length - _position);
            Array.Copy(bytes, _position, buffer, offset, read);
            _position += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_position >= bytes.Length)
                return ValueTask.FromResult(0);

            var read = Math.Min(Math.Min(buffer.Length, chunkSize), bytes.Length - _position);
            bytes.AsMemory(_position, read).CopyTo(buffer);
            _position += read;
            return ValueTask.FromResult(read);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
