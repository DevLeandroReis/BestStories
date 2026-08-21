using BestStories.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace BestStories.Infrastructure.Http;

/// <summary>
/// Process-wide cap on how many requests may be in flight to the Hacker News API
/// at any moment.
/// <para>
/// Registered as a <b>singleton</b> on purpose. Message handler chains are pooled and
/// rotated by <c>IHttpClientFactory</c>, so a semaphore owned by the handler itself
/// would be silently recreated — and the cap reset — every rotation. Holding the
/// semaphore in a singleton keeps one shared limit for the whole process, covering
/// every caller: inbound API requests and the background cache warmer alike.
/// </para>
/// </summary>
internal sealed class HackerNewsThrottle : IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public HackerNewsThrottle(IOptions<HackerNewsInfrastructureSettings> options)
    {
        MaxConcurrency = Math.Max(1, options.Value.MaxParallelRequests);
        _semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
    }

    /// <summary>Maximum number of simultaneous calls allowed to Hacker News.</summary>
    public int MaxConcurrency { get; }

    /// <summary>Calls currently holding a slot. Exposed for diagnostics and tests.</summary>
    public int InFlight => MaxConcurrency - _semaphore.CurrentCount;

    public Task WaitAsync(CancellationToken cancellationToken) => _semaphore.WaitAsync(cancellationToken);

    public void Release() => _semaphore.Release();

    public void Dispose() => _semaphore.Dispose();
}
