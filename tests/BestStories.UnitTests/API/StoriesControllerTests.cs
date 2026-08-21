using BestStories.API.AppServices;
using BestStories.API.Controllers;
using BestStories.API.DTOs;
using BestStories.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BestStories.UnitTests.API;

public sealed class StoriesControllerTests
{
    private readonly IStoriesAppService _appService = Substitute.For<IStoriesAppService>();
    private readonly StoriesController _sut;

    public StoriesControllerTests()
    {
        _sut = new StoriesController(_appService, Substitute.For<ILogger<StoriesController>>());
    }

    [Fact]
    public async Task GetBestStories_WhenValidationFails_ReturnsBadRequestWithErrorResponse()
    {
        _appService.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StoryResponse>>.Fail("Count must be a positive integer.", ErrorCode.Validation));

        var actionResult = await _sut.GetBestStories(0, CancellationToken.None);

        var badRequest = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequest.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Error.Should().Be("Count must be a positive integer.");
        error.Code.Should().Be("Validation");
    }

    [Fact]
    public async Task GetBestStories_WhenApiTimesOut_Returns504WithErrorResponse()
    {
        _appService.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StoryResponse>>.Fail("The request timed out.", ErrorCode.ExternalApiTimeout));

        var actionResult = await _sut.GetBestStories(5, CancellationToken.None);

        var result = actionResult.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status504GatewayTimeout);
        var error = result.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Code.Should().Be("ExternalApiTimeout");
    }

    [Fact]
    public async Task GetBestStories_WhenApiRateLimited_Returns503WithErrorResponse()
    {
        _appService.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StoryResponse>>.Fail("Rate limited.", ErrorCode.ExternalApiRateLimited));

        var actionResult = await _sut.GetBestStories(5, CancellationToken.None);

        var result = actionResult.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var error = result.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Code.Should().Be("ExternalApiRateLimited");
    }

    [Fact]
    public async Task GetBestStories_WhenApiUnavailable_Returns503WithErrorResponse()
    {
        _appService.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StoryResponse>>.Fail("Unavailable.", ErrorCode.ExternalApiUnavailable));

        var actionResult = await _sut.GetBestStories(5, CancellationToken.None);

        var result = actionResult.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetBestStories_WhenApiError_Returns502WithErrorResponse()
    {
        _appService.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StoryResponse>>.Fail("Unexpected error.", ErrorCode.ExternalApiError));

        var actionResult = await _sut.GetBestStories(5, CancellationToken.None);

        var result = actionResult.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
    }

    [Fact]
    public async Task GetBestStories_WhenServiceSucceeds_ReturnsOkWithStories()
    {
        var stories = new List<StoryResponse>
        {
            new() { Title = "Top Story", Score = 500 },
            new() { Title = "Second Story", Score = 300 }
        };
        _appService.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StoryResponse>>.Ok(stories));

        var actionResult = await _sut.GetBestStories(2, CancellationToken.None);

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(stories);
    }
}
