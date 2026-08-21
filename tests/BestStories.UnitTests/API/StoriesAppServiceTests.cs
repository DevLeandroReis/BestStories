using BestStories.API.AppServices;
using BestStories.Application.Settings;
using BestStories.Application.Interfaces;
using BestStories.Application.UseCases;
using BestStories.Application.Validators;
using BestStories.Domain.Entities;
using Microsoft.Extensions.Options;

namespace BestStories.UnitTests.API;

public sealed class StoriesAppServiceTests
{
    private readonly IHackerNewsRepository _repository = Substitute.For<IHackerNewsRepository>();
    private readonly StoriesAppService _sut;

    public StoriesAppServiceTests()
    {
        var settings = Options.Create(new HackerNewsApplicationSettings());
        var validator = new GetBestStoriesValidator(settings);
        var useCase = new GetBestStoriesUseCase(_repository, validator);
        _sut = new StoriesAppService(useCase);
    }

    [Fact]
    public async Task GetBestStoriesAsync_WhenUseCaseFails_PropagatesError()
    {
        var result = await _sut.GetBestStoriesAsync(0);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetBestStoriesAsync_WhenUseCaseSucceeds_ReturnsMappedResponses()
    {
        _repository.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<int>>([1]));
        _repository.GetStoriesByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Story?>>(
            [
                new Story
                {
                    Title = "Test Story",
                    Uri = "https://example.com",
                    PostedBy = "author",
                    Score = 100,
                    CommentCount = 5,
                    Time = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
                }
            ]));

        var result = await _sut.GetBestStoriesAsync(1);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value!.Single();
        response.Title.Should().Be("Test Story");
        response.Uri.Should().Be("https://example.com");
        response.PostedBy.Should().Be("author");
        response.Score.Should().Be(100);
        response.CommentCount.Should().Be(5);
    }
}
