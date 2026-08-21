using System.Diagnostics;

namespace BestStories.IntegrationTests;

/// <summary>
/// Covers the specification's second requirement: "efficiently service large numbers of requests
/// without risking overloading of the Hacker News API".
/// </summary>
public sealed class LoadBehaviourTests
{
    private const int StoryCount = 60;

    private static IReadOnlyList<StubStory> Stories() =>
        [.. Enumerable.Range(1, StoryCount).Select(i => new StubStory(
            Id: i,
            Title: $"Story {i}",
            Url: $"https://example.test/{i}",
            By: $"author{i}",
            Time: 1570887781,
            Score: (i * 37) % 500,
            Descendants: i))];

    [Fact]
    public async Task ManyConcurrentRequests_CollapseIntoOneCallPerStory()
    {
        // 200 simultaneous callers, each asking for the full list. Without caching and
        // stampede protection this would be 200 x 61 = 12,200 calls to Hacker News.
        var stub = new HackerNewsStubHandler(Stories()) { ResponseDelay = TimeSpan.FromMilliseconds(15) };
        using var factory = new BestStoriesApiFactory(stub);
        using var client = factory.CreateClient();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 200).Select(_ => client.GetFromJsonArrayAsync("/stories/50")));

        responses.Should().AllSatisfy(stories => stories.Should().HaveCount(50));

        stub.ItemCallCount.Should().Be(StoryCount,
            "each story must be fetched from Hacker News exactly once, however many callers arrive");
        stub.IdListCallCount.Should().Be(1,
            "the best-stories list must be fetched once, not once per caller");
    }

    [Fact]
    public async Task ManyConcurrentRequests_NeverExceedTheConfiguredHackerNewsConcurrency()
    {
        const int maxParallel = 8;
        var stub = new HackerNewsStubHandler(Stories()) { ResponseDelay = TimeSpan.FromMilliseconds(15) };

        using var factory = new BestStoriesApiFactory(
            stub,
            settings => settings["HackerNews:MaxParallelRequests"] = maxParallel.ToString());
        using var client = factory.CreateClient();

        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => client.GetFromJsonArrayAsync("/stories/50")));

        stub.PeakConcurrency.Should().BeLessThanOrEqualTo(maxParallel,
            "the cap on simultaneous Hacker News calls is process-wide, not per request");
    }

    [Fact]
    public async Task RepeatedRequests_AreServedFromCacheWithoutFurtherHackerNewsCalls()
    {
        var stub = new HackerNewsStubHandler(Stories());
        using var factory = new BestStoriesApiFactory(stub);
        using var client = factory.CreateClient();

        await client.GetFromJsonArrayAsync("/stories/50");
        var callsAfterWarmUp = stub.TotalCallCount;

        for (var i = 0; i < 25; i++)
            await client.GetFromJsonArrayAsync("/stories/50");

        stub.TotalCallCount.Should().Be(callsAfterWarmUp,
            "subsequent requests must not reach Hacker News at all while the cache is valid");
    }

    [Fact]
    public async Task DifferentValuesOfN_ShareOneCachedWorkingSet()
    {
        var stub = new HackerNewsStubHandler(Stories());
        using var factory = new BestStoriesApiFactory(stub);
        using var client = factory.CreateClient();

        await client.GetFromJsonArrayAsync("/stories/1");
        var callsAfterFirst = stub.TotalCallCount;

        await client.GetFromJsonArrayAsync("/stories/25");
        await client.GetFromJsonArrayAsync("/stories/60");

        stub.TotalCallCount.Should().Be(callsAfterFirst,
            "every value of n is answered from the same cached set, so varying n costs nothing");
    }

    [Fact]
    public async Task BackgroundWarmUp_PopulatesTheCacheSoRequestsNeverCallHackerNews()
    {
        var stub = new HackerNewsStubHandler(Stories());

        using var factory = new BestStoriesApiFactory(stub, settings =>
        {
            settings["HackerNews:CacheWarmupEnabled"] = "true";
            settings["HackerNews:CacheWarmupIntervalSeconds"] = "60";
        });

        // Creating the client starts the host, which starts the warm-up.
        using var client = factory.CreateClient();

        var stopwatch = Stopwatch.StartNew();
        while (stub.ItemCallCount < StoryCount && stopwatch.Elapsed < TimeSpan.FromSeconds(30))
            await Task.Delay(25);

        stub.ItemCallCount.Should().Be(StoryCount, "the warm-up should have loaded every story");

        var callsAfterWarmUp = stub.TotalCallCount;
        var stories = await client.GetFromJsonArrayAsync("/stories/50");

        stories.Should().HaveCount(50);
        stub.TotalCallCount.Should().Be(callsAfterWarmUp,
            "with the cache kept warm in the background, serving a request costs zero Hacker News calls");
    }
}
