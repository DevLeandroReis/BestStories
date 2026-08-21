using BestStories.Application.Common;

namespace BestStories.UnitTests.Application;

public sealed class ResultTests
{
    [Fact]
    public void Ok_ReturnsSuccessResultWithValue()
    {
        var result = Result<string>.Ok("hello");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
        result.Error.Should().BeNull();
        result.ErrorCode.Should().Be(ErrorCode.None);
    }

    [Fact]
    public void Fail_ReturnsFailureResultWithError()
    {
        var result = Result<string>.Fail("something went wrong");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("something went wrong");
        result.Value.Should().BeNull();
        result.ErrorCode.Should().Be(ErrorCode.Validation);
    }

    [Fact]
    public void Fail_WithExplicitErrorCode_ReturnsCorrectCode()
    {
        var result = Result<string>.Fail("timeout", ErrorCode.ExternalApiTimeout);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.ExternalApiTimeout);
    }

    [Fact]
    public void Map_WhenSuccess_TransformsValue()
    {
        var result = Result<string>.Ok("hello").Map(s => s.Length);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(5);
    }

    [Fact]
    public void Map_WhenFailure_PropagatesErrorAndCode()
    {
        var result = Result<string>.Fail("oops", ErrorCode.ExternalApiUnavailable).Map(s => s.Length);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("oops");
        result.ErrorCode.Should().Be(ErrorCode.ExternalApiUnavailable);
    }
}
