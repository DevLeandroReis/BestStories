using BestStories.API.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BestStories.UnitTests.API;

public sealed class GlobalExceptionHandlerTests
{
    private readonly GlobalExceptionHandler _sut = new(Substitute.For<ILogger<GlobalExceptionHandler>>());

    [Fact]
    public async Task TryHandleAsync_WhenOperationCanceled_ReturnsTrueWithoutChangingResponse()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var result = await _sut.TryHandleAsync(context, new OperationCanceledException(), default);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task TryHandleAsync_WhenUnhandledException_ReturnsTrueWith500Response()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var result = await _sut.TryHandleAsync(context, new InvalidOperationException("boom"), default);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Contain("application/json");
    }
}
