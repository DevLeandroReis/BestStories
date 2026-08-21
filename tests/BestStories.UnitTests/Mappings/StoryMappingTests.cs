using BestStories.API.DTOs;
using BestStories.API.Mappings;
using BestStories.Domain.Entities;
using BestStories.Infrastructure.Mappings;
using BestStories.Infrastructure.Models;

namespace BestStories.UnitTests.Mappings;

public sealed class StoryMappingTests
{
    [Fact]
    public void ToStory_MapsEveryHackerNewsFieldOntoTheDomainEntity()
    {
        var model = new HackerNewsItemModel
        {
            Id = 42,
            Title = "Clean Architecture Post",
            Url = "https://example.com/post",
            By = "author123",
            Time = 1_700_000_000,
            Score = 250,
            Descendants = 80
        };

        var story = model.ToStory();

        story.Title.Should().Be("Clean Architecture Post");
        story.Uri.Should().Be("https://example.com/post");
        story.PostedBy.Should().Be("author123");
        story.Score.Should().Be(250);
        story.CommentCount.Should().Be(80);
        story.Time.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
    }

    [Fact]
    public void ToStory_WhenTitleIsNull_MapsToEmptyString()
    {
        new HackerNewsItemModel { Title = null }.ToStory().Title.Should().Be(string.Empty);
    }

    [Fact]
    public void ToStory_WhenUrlIsAbsent_LeavesUriNull()
    {
        // Ask HN and similar text posts carry no url; they are still valid stories.
        new HackerNewsItemModel { Title = "Ask HN", Url = null }.ToStory().Uri.Should().BeNull();
    }

    [Fact]
    public void ToStory_ConvertsUnixSecondsToUtcOffset()
    {
        var story = new HackerNewsItemModel { Time = 1570887781 }.ToStory();

        story.Time.Offset.Should().Be(TimeSpan.Zero);
        story.Time.Should().Be(DateTimeOffset.Parse("2019-10-12T13:43:01+00:00"));
    }

    [Fact]
    public void ToResponse_MapsStoryOntoTheSpecifiedContract()
    {
        var time = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var story = new Story
        {
            Title = "Sample",
            Uri = "https://example.com",
            PostedBy = "user",
            Time = time,
            Score = 99,
            CommentCount = 7
        };

        var response = story.ToResponse();

        response.Should().BeEquivalentTo(new StoryResponse
        {
            Title = "Sample",
            Uri = "https://example.com",
            PostedBy = "user",
            Time = time,
            Score = 99,
            CommentCount = 7
        });
    }

    [Fact]
    public void ToResponseList_PreservesOrder()
    {
        IReadOnlyList<Story> stories =
        [
            new Story { Title = "First", Score = 30 },
            new Story { Title = "Second", Score = 20 }
        ];

        stories.ToResponseList().Select(r => r.Title).Should().Equal("First", "Second");
    }
}
