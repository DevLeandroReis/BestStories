using BestStories.Application.Common;
using BestStories.Application.Exceptions;

namespace BestStories.UnitTests.Application;

public sealed class ExternalApiGuardTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceeds_ReturnsOkWithValue()
    {
        var result = await ExternalApiGuard.ExecuteAsync(() => Task.FromResult(42));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTimeoutException_ReturnsTimeoutError()
    {
        var result = await ExternalApiGuard.ExecuteAsync<int>(() =>
            throw new ExternalApiTimeoutException("timeout"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.ExternalApiTimeout);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRateLimitedException_ReturnsRateLimitedError()
    {
        var result = await ExternalApiGuard.ExecuteAsync<int>(() =>
            throw new ExternalApiRateLimitedException("rate limited"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.ExternalApiRateLimited);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnavailableException_ReturnsUnavailableError()
    {
        var result = await ExternalApiGuard.ExecuteAsync<int>(() =>
            throw new ExternalApiUnavailableException("unavailable"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.ExternalApiUnavailable);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGenericExternalApiException_ReturnsApiError()
    {
        var result = await ExternalApiGuard.ExecuteAsync<int>(() =>
            throw new ExternalApiException("unexpected"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.ExternalApiError);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnexpectedException_DoesNotCatch()
    {
        var act = () => ExternalApiGuard.ExecuteAsync<int>(() =>
            throw new InvalidOperationException("not an API error"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
