using BestStories.Domain.Entities;
using BestStories.Infrastructure.Models;

namespace BestStories.Infrastructure.Mappings;

/// <summary>
/// Translates the Hacker News wire format into the domain entity.
/// <para>
/// Written by hand rather than with a mapping library: the shape is small, the field renames
/// (<c>url</c>/<c>by</c>/<c>descendants</c>) are the interesting part and belong in plain sight,
/// and this way a change to either type is a compile error instead of a runtime surprise.
/// </para>
/// </summary>
internal static class StoryMapper
{
    public static Story ToStory(this HackerNewsItemModel item) => new()
    {
        Title = item.Title ?? string.Empty,
        Uri = item.Url,
        PostedBy = item.By,
        Time = DateTimeOffset.FromUnixTimeSeconds(item.Time),
        Score = item.Score,
        CommentCount = item.Descendants
    };
}
