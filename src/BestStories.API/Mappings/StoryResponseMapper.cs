using BestStories.API.DTOs;
using BestStories.Domain.Entities;

namespace BestStories.API.Mappings;

/// <summary>Projects the domain entity onto the response contract defined by the specification.</summary>
internal static class StoryResponseMapper
{
    public static StoryResponse ToResponse(this Story story) => new()
    {
        Title = story.Title,
        Uri = story.Uri,
        PostedBy = story.PostedBy,
        Time = story.Time,
        Score = story.Score,
        CommentCount = story.CommentCount
    };

    public static IReadOnlyList<StoryResponse> ToResponseList(this IReadOnlyList<Story> stories) =>
        [.. stories.Select(ToResponse)];
}
