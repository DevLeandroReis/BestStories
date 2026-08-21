using System.Net;
using System.Text.Json;

namespace BestStories.IntegrationTests;

/// <summary>
/// Stands in for the Hacker News API so integration tests are deterministic and offline.
/// Also records how many calls were made and the highest observed concurrency, which lets
/// tests assert the caching and throttling guarantees rather than just the happy path.
/// </summary>
internal sealed class HackerNewsStubHandler : HttpMessageHandler
{
    private readonly IReadOnlyList<StubStory> _stories;
    private int _inFlight;

    public HackerNewsStubHandler(IReadOnlyList<StubStory> stories) => _stories = stories;

    public int IdListCallCount;
    public int ItemCallCount;
    public int PeakConcurrency;

    /// <summary>Delay applied to every response, used to create overlap in concurrency tests.</summary>
    public TimeSpan ResponseDelay { get; set; } = TimeSpan.Zero;

    /// <summary>When set, every request fails with this status, simulating a Hacker News outage.</summary>
    public HttpStatusCode? ForcedFailure { get; set; }

    /// <summary>When true, the ID list endpoint returns a JSON null instead of an array.</summary>
    public bool ReturnNullIdList { get; set; }

    public int TotalCallCount => IdListCallCount + ItemCallCount;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var current = Interlocked.Increment(ref _inFlight);
        int peak;
        while (current > (peak = Volatile.Read(ref PeakConcurrency)))
            Interlocked.CompareExchange(ref PeakConcurrency, current, peak);

        try
        {
            if (ResponseDelay > TimeSpan.Zero)
                await Task.Delay(ResponseDelay, cancellationToken);

            if (ForcedFailure is { } failure)
                return new HttpResponseMessage(failure);

            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/beststories.json", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref IdListCallCount);
                return ReturnNullIdList ? Json<object?>(null) : Json(_stories.Select(s => s.Id).ToArray());
            }

            var id = int.Parse(path.Split('/').Last().Replace(".json", string.Empty));
            Interlocked.Increment(ref ItemCallCount);

            var story = _stories.FirstOrDefault(s => s.Id == id);
            return story is null ? Json<object?>(null) : Json(story.ToItem());
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private static HttpResponseMessage Json<T>(T payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
    };
}

internal sealed record StubStory(
    int Id,
    string Title,
    string? Url,
    string By,
    long Time,
    int Score,
    int Descendants,
    string Type = "story",
    bool Deleted = false,
    bool Dead = false)
{
    public object ToItem() => new
    {
        id = Id,
        title = Title,
        url = Url,
        by = By,
        time = Time,
        score = Score,
        descendants = Descendants,
        type = Type,
        deleted = Deleted,
        dead = Dead
    };
}
