using BestStories.Infrastructure.Http;
using Microsoft.Extensions.Logging;

namespace BestStories.UnitTests.Infrastructure;

public sealed class FlurlRequestExecutorTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceeds_ReturnsValue()
    {
        var result = await FlurlRequestExecutor.ExecuteAsync(
            () => Task.FromResult("value"),
            _logger,
            context: "test");

        result.Should().Be("value");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnexpectedException_Propagates()
    {
        var act = () => FlurlRequestExecutor.ExecuteAsync<string>(
            () => throw new InvalidOperationException("unexpected"),
            _logger,
            context: "test");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteWithFallbackAsync_WhenOperationSucceeds_ReturnsValue()
    {
        var result = await FlurlRequestExecutor.ExecuteWithFallbackAsync(
            () => Task.FromResult<string?>("value"),
            _logger,
            context: "test");

        result.Should().Be("value");
    }

    [Fact]
    public async Task ExecuteWithFallbackAsync_WhenUnexpectedException_ReturnsFallback()
    {
        var result = await FlurlRequestExecutor.ExecuteWithFallbackAsync<string>(
            () => throw new InvalidOperationException("some error"),
            _logger,
            context: "test",
            fallback: null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteWithFallbackAsync_WhenOperationCanceled_PropagatesException()
    {
        var act = () => FlurlRequestExecutor.ExecuteWithFallbackAsync<string>(
            () => throw new OperationCanceledException(),
            _logger,
            context: "test");

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
