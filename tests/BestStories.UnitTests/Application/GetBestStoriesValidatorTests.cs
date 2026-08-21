using BestStories.Application.Settings;
using BestStories.Application.Validators;
using Microsoft.Extensions.Options;

namespace BestStories.UnitTests.Application;

public sealed class GetBestStoriesValidatorTests
{
    private readonly GetBestStoriesValidator _sut =
        new(Options.Create(new HackerNewsApplicationSettings { MaxStoriesCount = 500 }));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ValidateAsync_WhenCountIsNotPositive_ReturnsInvalid(int count)
    {
        var result = await _sut.ValidateAsync(count);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "Count must be a positive integer.");
    }

    [Fact]
    public async Task ValidateAsync_WhenCountExceedsMax_ReturnsInvalid()
    {
        var result = await _sut.ValidateAsync(501);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "Count cannot exceed 500.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(250)]
    [InlineData(500)]
    public async Task ValidateAsync_WhenCountIsWithinRange_ReturnsValid(int count)
    {
        var result = await _sut.ValidateAsync(count);

        result.IsValid.Should().BeTrue();
    }
}
