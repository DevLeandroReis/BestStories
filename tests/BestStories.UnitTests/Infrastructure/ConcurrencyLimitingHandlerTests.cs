using BestStories.Infrastructure.Http;
using BestStories.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace BestStories.UnitTests.Infrastructure;

public sealed class ConcurrencyLimitingHandlerTests
{
    /// <summary>
    /// Inner handler that records the highest number of requests it ever saw in flight
    /// simultaneously, so a test can assert the cap actually held.
    /// </summary>
    private sealed class ConcurrencyRecordingHandler : HttpMessageHandler
    {
        private int _inFlight;
        public int PeakConcurrency;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref _inFlight);

            int observedPeak;
            while (current > (observedPeak = Volatile.Read(ref PeakConcurrency)))
                Interlocked.CompareExchange(ref PeakConcurrency, current, observedPeak);

            try
            {
                await Task.Delay(20, cancellationToken);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
    }

    private static HackerNewsThrottle ThrottleOf(int max) =>
        new(Options.Create(new HackerNewsInfrastructureSettings { MaxParallelRequests = max }));

    [Fact]
    public async Task SendAsync_NeverExceedsConfiguredConcurrency()
    {
        const int maxParallel = 5;
        var recorder = new ConcurrencyRecordingHandler();
        using var throttle = ThrottleOf(maxParallel);
        using var sut = new ConcurrencyLimitingHandler(throttle) { InnerHandler = recorder };
        using var client = new HttpMessageInvoker(sut);

        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ =>
            client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://hn.test/item.json"), default)));

        recorder.PeakConcurrency.Should().BeLessThanOrEqualTo(maxParallel);
        recorder.PeakConcurrency.Should().BeGreaterThan(1, "requests should still run in parallel up to the cap");
    }

    [Fact]
    public async Task SendAsync_WhenManyCallersShareTheThrottle_CapIsGlobalNotPerCaller()
    {
        // The requirement is that inbound request volume cannot translate into unbounded
        // outbound load. Ten simultaneous "API requests", each fanning out to 20 stories,
        // must still respect one shared limit.
        const int maxParallel = 4;
        var recorder = new ConcurrencyRecordingHandler();
        using var throttle = ThrottleOf(maxParallel);

        async Task SimulateInboundRequest()
        {
            using var handler = new ConcurrencyLimitingHandler(throttle) { InnerHandler = recorder };
            using var client = new HttpMessageInvoker(handler, disposeHandler: false);

            await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
                client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://hn.test/item.json"), default)));
        }

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => SimulateInboundRequest()));

        recorder.PeakConcurrency.Should().BeLessThanOrEqualTo(maxParallel);
    }

    [Fact]
    public async Task SendAsync_ReleasesTheSlotWhenTheInnerHandlerThrows()
    {
        using var throttle = ThrottleOf(1);
        using var sut = new ConcurrencyLimitingHandler(throttle) { InnerHandler = new ThrowingHandler() };
        using var client = new HttpMessageInvoker(sut);

        for (var i = 0; i < 3; i++)
        {
            var act = async () => await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "https://hn.test/item.json"), default);

            await act.Should().ThrowAsync<HttpRequestException>();
        }

        throttle.InFlight.Should().Be(0, "a failed call must not leak its slot");
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("boom"));
    }
}
