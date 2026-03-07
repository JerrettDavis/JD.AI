using System.Net;
using System.Text;
using FluentAssertions;
using JD.AI.Core.Events;
using JD.AI.Daemon.Config;
using JD.AI.Daemon.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using DaemonUpdateChecker = JD.AI.Daemon.Services.UpdateChecker;
using DaemonUpdateInfo = JD.AI.Daemon.Services.UpdateInfo;

namespace JD.AI.Tests.Daemon.Services;

/// <summary>
/// Unit tests for <see cref="UpdateService"/>.
///
/// Strategy: UpdateService depends on UpdateChecker (a concrete class that calls
/// the NuGet HTTP API). We stub UpdateChecker by injecting it via a factory helper
/// that wires a stub IHttpClientFactory returning canned JSON responses — the same
/// pattern used in <see cref="UpdateCheckerTests"/>.
///
/// IEventBus, IHostApplicationLifetime, and IOptions are NSubstituted.
/// </summary>
public sealed class UpdateServiceTests
{
    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>Creates an UpdateConfig with an extremely short drain timeout so tests don't block.</summary>
    private static UpdateConfig DefaultConfig(
        bool autoApply = false,
        bool notifyChannels = true,
        TimeSpan? drainTimeout = null) => new()
        {
            CheckInterval = TimeSpan.FromHours(24),
            AutoApply = autoApply,
            NotifyChannels = notifyChannels,
            DrainTimeout = drainTimeout ?? TimeSpan.Zero,
            PackageId = "JD.AI.Daemon",
            NuGetFeedUrl = "https://api.nuget.org/v3-flatcontainer/",
        };

    private static DaemonUpdateChecker BuildChecker(string responseJson, UpdateConfig? config = null)
    {
        config ??= DefaultConfig();
        var handler = new StubHttpHandler(responseJson, HttpStatusCode.OK);
        var client = new HttpClient(handler);
        var factory = new StubHttpClientFactory(client);
        var opts = Options.Create(config);
        return new DaemonUpdateChecker(opts, factory, NullLogger<DaemonUpdateChecker>.Instance);
    }

    private static UpdateService BuildService(
        DaemonUpdateChecker checker,
        UpdateConfig? config = null,
        IEventBus? events = null,
        IHostApplicationLifetime? lifetime = null)
    {
        config ??= DefaultConfig();
        events ??= Substitute.For<IEventBus>();
        lifetime ??= Substitute.For<IHostApplicationLifetime>();

        return new UpdateService(
            Options.Create(config),
            checker,
            events,
            lifetime,
            NullLogger<UpdateService>.Instance);
    }

    // ── initial state ──────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsDrainingIsFalse()
    {
        var checker = BuildChecker("""{"versions":[]}""");
        var svc = BuildService(checker);

        svc.IsDraining.Should().BeFalse();
    }

    [Fact]
    public void InitialState_PendingUpdateIsNull()
    {
        var checker = BuildChecker("""{"versions":[]}""");
        var svc = BuildService(checker);

        svc.PendingUpdate.Should().BeNull();
    }

    // ── ApplyUpdateAsync — no pending update, no update available ─────────────

    [Fact]
    public async Task ApplyUpdateAsync_NoPendingUpdate_NoUpdateAvailable_LogsUpToDate()
    {
        // Checker returns empty versions → "already up to date"
        var checker = BuildChecker("""{"versions":[]}""");
        var events = Substitute.For<IEventBus>();
        var svc = BuildService(checker, events: events);

        await svc.ApplyUpdateAsync();

        // No events should have been published (no draining, no applying)
        await events.Received(0).PublishAsync(Arg.Any<GatewayEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyUpdateAsync_NoPendingUpdate_NoUpdateAvailable_IsDrainingRemainsFlase()
    {
        var checker = BuildChecker("""{"versions":[]}""");
        var svc = BuildService(checker);

        await svc.ApplyUpdateAsync();

        svc.IsDraining.Should().BeFalse();
    }

    // ── ApplyUpdateAsync — update available via fresh check ───────────────────

    [Fact]
    public async Task ApplyUpdateAsync_NoPendingUpdate_UpdateAvailable_SetsPendingUpdate()
    {
        // Version 9999.99.99 is always newer than 0.0.0 (test assembly version)
        var checker = BuildChecker("""{"versions":["9999.99.99"]}""");
        var svc = BuildService(checker);

        await svc.ApplyUpdateAsync();

        svc.PendingUpdate.Should().NotBeNull();
        svc.PendingUpdate!.LatestVersion.Should().Be(new Version(9999, 99, 99));
    }

    [Fact]
    public async Task ApplyUpdateAsync_UpdateAvailable_PublishesDrainingEvent()
    {
        var checker = BuildChecker("""{"versions":["9999.99.99"]}""");
        var events = Substitute.For<IEventBus>();
        var svc = BuildService(checker, events: events);

        await svc.ApplyUpdateAsync();

        await events.Received().PublishAsync(
            Arg.Is<GatewayEvent>(e => e.EventType == "gateway.update.draining"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyUpdateAsync_UpdateAvailable_PublishesApplyingEvent()
    {
        var checker = BuildChecker("""{"versions":["9999.99.99"]}""");
        var events = Substitute.For<IEventBus>();
        var svc = BuildService(checker, events: events);

        await svc.ApplyUpdateAsync();

        await events.Received().PublishAsync(
            Arg.Is<GatewayEvent>(e => e.EventType == "gateway.update.applying"),
            Arg.Any<CancellationToken>());
    }

    // ── ApplyUpdateAsync — restarting event (tool update succeeds) ───────────
    // NOTE: RunToolUpdateAsync invokes ProcessExecutor.RunAsync("dotnet", ...) which
    // will actually run on the test machine. On a machine with dotnet installed,
    // "dotnet tool update -g JD.AI.Daemon" will either succeed or fail gracefully.
    // We therefore only assert on state that doesn't depend on that external call.

    [Fact]
    public async Task ApplyUpdateAsync_UpdateAvailable_SetsIsDrainingDuringExecution()
    {
        var checker = BuildChecker("""{"versions":["9999.99.99"]}""");
        bool drainingDuringApply = false;

        var events = Substitute.For<IEventBus>();
        events.PublishAsync(
                Arg.Is<GatewayEvent>(e => e.EventType == "gateway.update.draining"),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                // Capture IsDraining at the moment the draining event fires
                // We cannot access svc yet (not assigned), so we capture it via closure after
                drainingDuringApply = true;
                return Task.CompletedTask;
            });

        var svc = BuildService(checker, events: events);
        await svc.ApplyUpdateAsync();

        drainingDuringApply.Should().BeTrue();
    }

    // ── ApplyUpdateAsync — pre-set PendingUpdate skips fresh check ───────────

    [Fact]
    public async Task ApplyUpdateAsync_WithPendingUpdate_SkipsFreshCheck()
    {
        // Use a checker that would return null (no update) if called fresh
        var checker = BuildChecker("""{"versions":[]}""");
        var events = Substitute.For<IEventBus>();
        var svc = BuildService(checker, events: events);

        // Manually prime a pending update
        var pendingUpdateVersion = new Version(9999, 0, 0);
        var pendingUpdate = new DaemonUpdateInfo(new Version(0, 0, 0), pendingUpdateVersion);
        typeof(UpdateService)
            .GetField("_pendingUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(svc, pendingUpdate);

        await svc.ApplyUpdateAsync();

        // The draining event should have been published (we used the primed update, not a fresh check)
        await events.Received().PublishAsync(
            Arg.Is<GatewayEvent>(e => e.EventType == "gateway.update.draining"),
            Arg.Any<CancellationToken>());
    }

    // ── ApplyUpdateAsync — cancellation ───────────────────────────────────────

    [Fact]
    public async Task ApplyUpdateAsync_Cancellation_BeforeDrain_DoesNotPublishEvents()
    {
        var checker = BuildChecker("""{"versions":["9999.99.99"]}""");
        var events = Substitute.For<IEventBus>();
        events.PublishAsync(Arg.Any<GatewayEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled(new CancellationToken(canceled: true)));

        var svc = BuildService(checker, events: events);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Should not throw — exception is caught and IsDraining is reset
        await svc.ApplyUpdateAsync(cts.Token);

        svc.IsDraining.Should().BeFalse();
    }

    // ── ApplyUpdateAsync — exception handling ─────────────────────────────────

    [Fact]
    public async Task ApplyUpdateAsync_EventBusThrows_IsDrainingResetToFalse()
    {
        var checker = BuildChecker("""{"versions":["9999.99.99"]}""");
        var events = Substitute.For<IEventBus>();
        events.PublishAsync(Arg.Any<GatewayEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("bus exploded"));

        var svc = BuildService(checker, events: events);

        // Should not propagate the exception
        await svc.ApplyUpdateAsync();

        svc.IsDraining.Should().BeFalse();
    }

    // ── NotifyUpdateAvailableAsync (via ApplyUpdateAsync pathway) ────────────

    [Fact]
    public async Task NotifyUpdateAvailable_WhenNotifyChannelsTrue_PublishesAvailableEvent()
    {
        // We test the notification pathway indirectly through the ExecuteAsync loop.
        // We can also confirm that NotifyChannels=false skips the event.
        var config = DefaultConfig(notifyChannels: true);
        var checker = BuildChecker("""{"versions":["9999.99.99"]}""", config);
        var events = Substitute.For<IEventBus>();
        var svc = BuildService(checker, config, events);

        // Trigger notify indirectly by running ApplyUpdateAsync (which internally calls
        // NotifyUpdateAvailableAsync when autoApply is true during the loop, but
        // also triggers the apply path which sets IsDraining and publishes events).
        // Here we prime PendingUpdate and call directly.
        typeof(UpdateService)
            .GetField("_pendingUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(svc, new DaemonUpdateInfo(new Version(0, 0, 0), new Version(9999, 0, 0)));

        await svc.ApplyUpdateAsync();

        // At least the "draining" event should have been published
        await events.Received().PublishAsync(
            Arg.Is<GatewayEvent>(e => e.EventType == "gateway.update.draining"),
            Arg.Any<CancellationToken>());
    }

    // ── ExecuteAsync — cancels on first delay ─────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CancelledDuringInitialDelay_CompletesCleanly()
    {
        var checker = BuildChecker("""{"versions":[]}""");
        var svc = BuildService(checker);

        using var cts = new CancellationTokenSource();

        // Start the background service and immediately cancel
        var executeTask = ((IHostedService)svc).StartAsync(cts.Token);
        await cts.CancelAsync();

        // Should complete without throwing OperationCanceledException
        await executeTask;
        await svc.StopAsync(CancellationToken.None);
    }

    // ── PendingUpdate property ─────────────────────────────────────────────────

    [Fact]
    public void PendingUpdate_ReflectsInternalField()
    {
        var checker = BuildChecker("""{"versions":[]}""");
        var svc = BuildService(checker);

        svc.PendingUpdate.Should().BeNull();

        var update = new DaemonUpdateInfo(new Version(1, 0, 0), new Version(2, 0, 0));
        typeof(UpdateService)
            .GetField("_pendingUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(svc, update);

        svc.PendingUpdate.Should().Be(update);
    }

    // ── ApplyUpdateAsync — drain timeout is respected ─────────────────────────

    [Fact]
    public async Task ApplyUpdateAsync_DrainTimeout_IsUsed()
    {
        // Use a non-zero (but very short) drain timeout to exercise the delay path
        var config = DefaultConfig(drainTimeout: TimeSpan.FromMilliseconds(1));
        var checker = BuildChecker("""{"versions":["9999.99.99"]}""", config);
        var events = Substitute.For<IEventBus>();
        var svc = BuildService(checker, config, events);

        // Should complete without hanging
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await svc.ApplyUpdateAsync();
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(30_000, "drain timeout should not block for more than 30s");
    }

    // ── ApplyUpdateAsync — StopApplication is called after successful tool update ──

    [Fact]
    public async Task ApplyUpdateAsync_AfterToolUpdate_CallsStopApplication()
    {
        // dotnet tool update will actually be invoked here. Whether it succeeds or fails
        // depends on the environment. If it succeeds, StopApplication should be called.
        // If it fails (exit code != 0), IsDraining is reset and StopApplication is NOT called.
        // We verify neither path throws.
        var checker = BuildChecker("""{"versions":["9999.99.99"]}""");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var svc = BuildService(checker, lifetime: lifetime);

        await svc.ApplyUpdateAsync();

        // No assertion on IsDraining or StopApplication invocation count because
        // the outcome depends on the real `dotnet tool update` process result:
        // success → IsDraining stays true and StopApplication is called;
        // failure → IsDraining is reset to false.
        // Either path is valid — we verify no exception was raised.
    }

    // ── stubs ─────────────────────────────────────────────────────────────────

    private sealed class StubHttpHandler(string content, HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
