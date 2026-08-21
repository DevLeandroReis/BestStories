using BestStories.Application.Exceptions;
using BestStories.Application.Interfaces;
using BestStories.Domain.Entities;
using BestStories.Infrastructure.Cache;
using BestStories.Infrastructure.Http;
using BestStories.Infrastructure.Mappings;
using BestStories.Infrastructure.Models;
using BestStories.Infrastructure.Settings;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BestStories.Infrastructure.Repositories;

internal sealed class HackerNewsRepository : IHackerNewsRepository
{
    private const string BestStoriesCacheKey = "best_story_ids";
    private const string StoryItemCacheKeyPrefix = "story_";

    private readonly IFlurlClient _flurlClient;
    private readonly ICacheService _cache;
    private readonly ILogger<HackerNewsRepository> _logger;
    private readonly TimeSpan _idListCacheDuration;
    private readonly TimeSpan _storyItemCacheDuration;

    public HackerNewsRepository(
        HttpClient httpClient,
        ICacheService cache,
        ILogger<HackerNewsRepository> logger,
        IOptions<HackerNewsInfrastructureSettings> settingsOptions)
    {
        _flurlClient = new FlurlClient(httpClient);
        _cache = cache;
        _logger = logger;

        var settings = settingsOptions.Value;
        _idListCacheDuration = TimeSpan.FromSeconds(settings.IdListCacheSeconds);
        _storyItemCacheDuration = TimeSpan.FromSeconds(settings.StoryItemCacheSeconds);
    }

    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken = default)
    {
        var ids = await FlurlRequestExecutor.ExecuteAsync(
            () => _cache.GetOrCreateAsync<IReadOnlyList<int>>(
                BestStoriesCacheKey,
                _idListCacheDuration,
                FetchBestStoryIdsFromApiAsync,
                cancellationToken),
            _logger,
            context: "fetching best story IDs");

        return ids ?? [];
    }

    /// <summary>
    /// Fetches every requested story concurrently. Outbound concurrency is capped globally by
    /// <see cref="ConcurrencyLimitingHandler"/>, and identical concurrent misses are collapsed
    /// into a single call by the cache — so fanning out here cannot overload Hacker News.
    /// </summary>
    public async Task<IReadOnlyList<Story?>> GetStoriesByIdsAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken = default) =>
        await Task.WhenAll(ids.Select(id => GetStoryByIdAsync(id, cancellationToken)));

    /// <summary>
    /// Force-refreshes the whole working set (ID list plus every story it references),
    /// replacing cached entries before they expire. Called only by the background warm-up.
    /// </summary>
    public async Task<int> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var ids = await FlurlRequestExecutor.ExecuteAsync(
            () => FetchBestStoryIdsFromApiAsync(cancellationToken),
            _logger,
            context: "refreshing best story IDs") ?? [];

        _cache.Set(BestStoriesCacheKey, ids, _idListCacheDuration);

        var refreshed = 0;
        await Task.WhenAll(ids.Select(async id =>
        {
            var story = await FlurlRequestExecutor.ExecuteWithFallbackAsync(
                () => FetchStoryFromApiAsync(id, cancellationToken),
                _logger,
                context: $"refreshing story {id}");

            if (story is null)
                return;

            _cache.Set($"{StoryItemCacheKeyPrefix}{id}", story, _storyItemCacheDuration);
            Interlocked.Increment(ref refreshed);
        }));

        return refreshed;
    }

    private Task<Story?> GetStoryByIdAsync(int id, CancellationToken cancellationToken) =>
        FlurlRequestExecutor.ExecuteWithFallbackAsync(
            () => _cache.GetOrCreateAsync<Story>(
                $"{StoryItemCacheKeyPrefix}{id}",
                _storyItemCacheDuration,
                ct => FetchStoryFromApiAsync(id, ct),
                cancellationToken),
            _logger,
            context: $"fetching story {id}");

    private async Task<IReadOnlyList<int>?> FetchBestStoryIdsFromApiAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching best story IDs from Hacker News API");
        return await _flurlClient
            .Request("beststories.json")
            .GetJsonAsync<int[]>(cancellationToken: cancellationToken)
            ?? throw new ExternalApiException("Hacker News returned no best-stories list.");
    }

    private async Task<Story?> FetchStoryFromApiAsync(int id, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching story {StoryId} from Hacker News API", id);
        var item = await _flurlClient
            .Request($"item/{id}.json")
            .GetJsonAsync<HackerNewsItemModel>(cancellationToken: cancellationToken);

        return item is null || item.IsUnavailable ? null : item.ToStory();
    }
}
