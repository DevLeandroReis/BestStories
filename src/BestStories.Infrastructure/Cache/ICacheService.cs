namespace BestStories.Infrastructure.Cache;

internal interface ICacheService
{
    Task<T?> GetOrCreateAsync<T>(
        string key,
        TimeSpan duration,
        Func<CancellationToken, Task<T?>> factory,
        CancellationToken cancellationToken) where T : class;

    /// <summary>
    /// Writes a value unconditionally, replacing any existing entry and restarting its TTL.
    /// Used by the background warm-up, which already holds fresh data and must refresh
    /// entries <i>before</i> they expire rather than waiting for a miss.
    /// </summary>
    void Set<T>(string key, T value, TimeSpan duration) where T : class;
}
