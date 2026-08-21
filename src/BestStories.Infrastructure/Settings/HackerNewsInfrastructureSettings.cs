namespace BestStories.Infrastructure.Settings;

public sealed record HackerNewsInfrastructureSettings
{
    public string BaseUrl { get; init; } = "https://hacker-news.firebaseio.com/v0/";

    public int HttpClientTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Safety-net TTL for the cached ID list. Under normal operation the background warm-up
    /// replaces the entry well before it expires; this only matters if the warm-up is disabled
    /// or has been failing.
    /// </summary>
    public int IdListCacheSeconds { get; init; } = 600;

    /// <summary>Safety-net TTL for cached story details. See <see cref="IdListCacheSeconds"/>.</summary>
    public int StoryItemCacheSeconds { get; init; } = 600;

    /// <summary>
    /// Hard ceiling on simultaneous calls to Hacker News, shared process-wide. This is the
    /// guarantee that inbound traffic volume can never translate into unbounded outbound load.
    /// </summary>
    public int MaxParallelRequests { get; init; } = 25;

    /// <summary>Whether the background warm-up runs. Disabled in tests for determinism.</summary>
    public bool CacheWarmupEnabled { get; init; } = true;

    /// <summary>
    /// How often the working set is refreshed. This — not request volume — determines the
    /// steady-state call rate against Hacker News.
    /// </summary>
    public int CacheWarmupIntervalSeconds { get; init; } = 120;
}
