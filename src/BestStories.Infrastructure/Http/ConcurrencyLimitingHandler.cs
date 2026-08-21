namespace BestStories.Infrastructure.Http;

/// <summary>
/// Delegating handler that funnels every outbound Hacker News call through the shared
/// <see cref="HackerNewsThrottle"/>.
/// <para>
/// Placing the limit here rather than in the repository means it cannot be bypassed:
/// the ID list request, every story item request and the background warm-up all travel
/// through the same handler chain, so the cap holds no matter how many requests the API
/// is serving concurrently.
/// </para>
/// </summary>
internal sealed class ConcurrencyLimitingHandler(HackerNewsThrottle throttle) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(cancellationToken);
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            throttle.Release();
        }
    }
}
