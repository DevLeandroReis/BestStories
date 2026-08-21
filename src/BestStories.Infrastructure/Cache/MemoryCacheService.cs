using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace BestStories.Infrastructure.Cache;

internal sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public MemoryCacheService(IMemoryCache cache) => _cache = cache;

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        TimeSpan duration,
        Func<CancellationToken, Task<T?>> factory,
        CancellationToken cancellationToken) where T : class
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(key, out cached) && cached is not null)
                return cached;

            var value = await factory(cancellationToken);
            if (value is not null)
                Set(key, value, duration);

            return value;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void Set<T>(string key, T value, TimeSpan duration) where T : class
    {
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(duration)
            .RegisterPostEvictionCallback((k, _, _, _) => _locks.TryRemove((string)k, out _));

        _cache.Set(key, value, options);
    }
}
