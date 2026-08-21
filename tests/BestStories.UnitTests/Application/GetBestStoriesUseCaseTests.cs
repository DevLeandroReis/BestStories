using BestStories.Application.Settings;
using BestStories.Application.Common;
using BestStories.Application.Exceptions;
using BestStories.Application.Interfaces;
using BestStories.Application.UseCases;
using BestStories.Application.Validators;
using BestStories.Domain.Entities;
using Microsoft.Extensions.Options;

namespace BestStories.UnitTests.Application;

public sealed class GetBestStoriesUseCaseTests
{
    private readonly IHackerNewsRepository _repository = Substitute.For<IHackerNewsRepository>();
    private readonly GetBestStoriesUseCase _sut;

    public GetBestStoriesUseCaseTests()
    {
        var settings = Options.Create(new HackerNewsApplicationSettings());
        var validator = new GetBestStoriesValidator(settings);
        _sut = new GetBestStoriesUseCase(_repository, validator);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_WhenCountIsInvalid_ReturnsFailWithValidationError(int count)
    {
        var result = await _sut.ExecuteAsync(count);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("positive integer");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCountExceedsMax_ReturnsFailWithValidationError()
    {
        var result = await _sut.ExecuteAsync(501);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cannot exceed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoStoriesAvailable_ReturnsEmptyList()
    {
        _repository.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<int>>([]));

        var result = await _sut.ExecuteAsync(5);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsStoriesSortedByScoreDescending()
    {
        _repository.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<int>>([1, 2, 3]));
        _repository.GetStoriesByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Story?>>(
            [
                new Story { Title = "Low", Score = 10 },
                new Story { Title = "High", Score = 30 },
                new Story { Title = "Mid", Score = 20 }
            ]));

        var result = await _sut.ExecuteAsync(3);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(s => s.Score).Should().BeInDescendingOrder();
        result.Value![0].Title.Should().Be("High");
    }

    [Fact]
    public async Task ExecuteAsync_FiltersOutNullStoriesFromRepository()
    {
        _repository.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<int>>([1, 2]));
        _repository.GetStoriesByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Story?>>(
            [
                new Story { Title = "Valid", Score = 10 },
                null
            ]));

        var result = await _sut.ExecuteAsync(2);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1).And.ContainSingle(s => s.Title == "Valid");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCountLessThanAvailable_ReturnsOnlyTopNByScore()
    {
        _repository.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<int>>([1, 2, 3]));
        _repository.GetStoriesByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Story?>>(
            [
                new Story { Title = "A", Score = 10 },
                new Story { Title = "B", Score = 30 },
                new Story { Title = "C", Score = 20 }
            ]));

        var result = await _sut.ExecuteAsync(2);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value![0].Score.Should().Be(30);
        result.Value![1].Score.Should().Be(20);
    }

    [Fact]
    public async Task ExecuteAsync_WhenApiTimesOut_ReturnsTimeoutError()
    {
        _repository.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<int>>(new ExternalApiTimeoutException("Timed out.")));

        var result = await _sut.ExecuteAsync(5);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.ExternalApiTimeout);
    }

    [Fact]
    public async Task ExecuteAsync_WhenApiRateLimited_ReturnsRateLimitedError()
    {
        _repository.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<int>>(new ExternalApiRateLimitedException("Rate limited.")));

        var result = await _sut.ExecuteAsync(5);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.ExternalApiRateLimited);
    }

    [Fact]
    public async Task ExecuteAsync_WhenApiUnavailable_ReturnsUnavailableError()
    {
        _repository.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<int>>(new ExternalApiUnavailableException("Unavailable.")));

        var result = await _sut.ExecuteAsync(5);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.ExternalApiUnavailable);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGenericApiError_ReturnsApiError()
    {
        _repository.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<int>>(new ExternalApiException("Unexpected error.")));

        var result = await _sut.ExecuteAsync(5);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.ExternalApiError);
    }

    [Fact]
    public async Task ExecuteAsync_ScoresEveryCandidateNotJustTheListPrefix()
    {
        // Guards against ranking only a prefix of beststories.json: Hacker News does not
        // document that list as score-ordered, so every ID must be scored.
        var ids = Enumerable.Range(1, 200).ToList();
        _repository.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<int>>(ids));
        _repository.GetStoriesByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Story?>>([]));

        await _sut.ExecuteAsync(5);

        await _repository.Received(1).GetStoriesByIdsAsync(
            Arg.Is<IEnumerable<int>>(x => x.Count() == 200),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenHighestScoringStoryIsLastInTheList_StillReturnsIt()
    {
        _repository.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<int>>([1, 2, 3]));
        _repository.GetStoriesByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Story?>>(
            [
                new Story { Title = "First in list", Score = 10 },
                new Story { Title = "Second in list", Score = 20 },
                new Story { Title = "Last in list", Score = 999 }
            ]));

        var result = await _sut.ExecuteAsync(1);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle().Which.Title.Should().Be("Last in list");
    }
}
