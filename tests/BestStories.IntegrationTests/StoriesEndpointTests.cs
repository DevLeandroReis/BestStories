using System.Net;
using System.Text.Json;

namespace BestStories.IntegrationTests;

public sealed class StoriesEndpointTests
{
    /// <summary>Deliberately NOT in score order, so a prefix-only implementation would fail.</summary>
    private static IReadOnlyList<StubStory> SampleStories(int count = 50) =>
        [.. Enumerable.Range(1, count).Select(i => new StubStory(
            Id: i,
            Title: $"Story {i}",
            Url: $"https://example.test/{i}",
            By: $"author{i}",
            Time: 1570887781, // 2019-10-12T13:43:01+00:00
            Score: i % 2 == 0 ? i : count * 10 - i,
            Descendants: i))];

    [Fact]
    public async Task GetBestStories_ReturnsTheExactShapeRequiredBySpecification()
    {
        var stub = new HackerNewsStubHandler(
        [
            new StubStory(
                Id: 21233041,
                Title: "A uBlock Origin update was rejected from the Chrome Web Store",
                Url: "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
                By: "ismaildonmez",
                Time: 1570887781,
                Score: 1716,
                Descendants: 572)
        ]);

        using var factory = new BestStoriesApiFactory(stub);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/stories/1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        var story = document.RootElement[0];

        story.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo(["title", "uri", "postedBy", "time", "score", "commentCount"],
                options => options.WithStrictOrdering(),
                "the response must match the field names and order in the specification");

        story.GetProperty("title").GetString()
            .Should().Be("A uBlock Origin update was rejected from the Chrome Web Store");
        story.GetProperty("uri").GetString()
            .Should().Be("https://github.com/uBlockOrigin/uBlock-issues/issues/745");
        story.GetProperty("postedBy").GetString().Should().Be("ismaildonmez");
        story.GetProperty("time").GetString().Should().Be("2019-10-12T13:43:01+00:00");
        story.GetProperty("score").GetInt32().Should().Be(1716);
        story.GetProperty("commentCount").GetInt32().Should().Be(572);
    }

    [Fact]
    public async Task GetBestStories_ReturnsExactlyNStoriesInDescendingScoreOrder()
    {
        var stub = new HackerNewsStubHandler(SampleStories());
        using var factory = new BestStoriesApiFactory(stub);
        using var client = factory.CreateClient();

        var stories = await client.GetFromJsonArrayAsync("/stories/10");

        stories.Should().HaveCount(10);
        stories.Select(s => s.Score).Should().BeInDescendingOrder();
        stories.Select(s => s.Score).Should().Equal(
            SampleStories().Select(s => s.Score).OrderByDescending(s => s).Take(10),
            "the top 10 must be selected across every candidate, not the first 10 in the list");
    }

    [Fact]
    public async Task GetBestStories_WhenNExceedsAvailableStories_ReturnsAllAvailable()
    {
        var stub = new HackerNewsStubHandler(SampleStories(5));
        using var factory = new BestStoriesApiFactory(stub);
        using var client = factory.CreateClient();

        var stories = await client.GetFromJsonArrayAsync("/stories/500");

        stories.Should().HaveCount(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public async Task GetBestStories_WhenNIsOutOfRange_ReturnsBadRequestWithoutCallingHackerNews(int n)
    {
        var stub = new HackerNewsStubHandler(SampleStories());
        using var factory = new BestStoriesApiFactory(stub);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/stories/{n}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        stub.TotalCallCount.Should().Be(0, "invalid input must be rejected before any external call");
    }

    [Fact]
    public async Task GetBestStories_ExcludesDeletedDeadAndNonStoryItems()
    {
        var stub = new HackerNewsStubHandler(
        [
            new StubStory(1, "Good", "https://example.test/1", "a", 1570887781, 100, 10),
            new StubStory(2, "Deleted", null, "b", 1570887781, 900, 10, Deleted: true),
            new StubStory(3, "Dead", null, "c", 1570887781, 900, 10, Dead: true),
            new StubStory(4, "A comment", null, "d", 1570887781, 900, 10, Type: "comment")
        ]);

        using var factory = new BestStoriesApiFactory(stub);
        using var client = factory.CreateClient();

        var stories = await client.GetFromJsonArrayAsync("/stories/10");

        stories.Should().ContainSingle().Which.Title.Should().Be("Good");
    }

    [Fact]
    public async Task GetBestStories_WhenStoryHasNoUrl_ReturnsNullUriRatherThanOmittingTheStory()
    {
        var stub = new HackerNewsStubHandler(
            [new StubStory(1, "Ask HN: something", null, "asker", 1570887781, 42, 7)]);

        using var factory = new BestStoriesApiFactory(stub);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/stories/1");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetArrayLength().Should().Be(1);
        document.RootElement[0].GetProperty("uri").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetBestStories_WhenHackerNewsIsDown_ReturnsServiceUnavailableNotServerError()
    {
        var stub = new HackerNewsStubHandler(SampleStories())
        {
            ForcedFailure = HttpStatusCode.ServiceUnavailable
        };

        using var factory = new BestStoriesApiFactory(stub);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/stories/5");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Hacker News");
    }

    [Theory]
    [InlineData("/stories/abc")]
    [InlineData("/stories")]
    public async Task GetBestStories_WhenNIsMissingOrNonNumeric_ReturnsNotFound(string url)
    {
        // The int route constraint means these match no route at all, which is a 404 rather
        // than a validation failure.
        var stub = new HackerNewsStubHandler(SampleStories());
        using var factory = new BestStoriesApiFactory(stub);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        stub.TotalCallCount.Should().Be(0);
    }

    [Fact]
    public async Task GetBestStories_WhenHackerNewsReturnsNoList_ReturnsBadGatewayNotServerError()
    {
        var stub = new HackerNewsStubHandler(SampleStories()) { ReturnNullIdList = true };
        using var factory = new BestStoriesApiFactory(stub);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/stories/5");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway,
            "a malformed upstream response is an upstream fault, not an unhandled server error");
    }

    [Fact]
    public async Task GetBestStories_RouteIsCaseInsensitive()
    {
        var stub = new HackerNewsStubHandler(SampleStories(3));
        using var factory = new BestStoriesApiFactory(stub);
        using var client = factory.CreateClient();

        foreach (var url in new[] { "/stories/2", "/Stories/2" })
            (await client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
