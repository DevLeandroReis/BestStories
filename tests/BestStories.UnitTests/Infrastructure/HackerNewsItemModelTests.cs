using BestStories.Infrastructure.Models;

namespace BestStories.UnitTests.Infrastructure;

public sealed class HackerNewsItemModelTests
{
    [Theory]
    [InlineData("story", false, false, false)]
    [InlineData("Story", false, false, false)]
    [InlineData(null, false, false, false)]
    [InlineData("story", true, false, true)]
    [InlineData("story", false, true, true)]
    [InlineData("comment", false, false, true)]
    [InlineData("job", false, false, true)]
    [InlineData("poll", false, false, true)]
    public void IsUnavailable_ExcludesRemovedFlaggedAndNonStoryItems(
        string? type, bool deleted, bool dead, bool expected)
    {
        var model = new HackerNewsItemModel { Type = type, Deleted = deleted, Dead = dead };

        model.IsUnavailable.Should().Be(expected);
    }
}
