namespace BestStories.Domain.Entities;

public sealed class Story
{
    public string Title { get; init; } = string.Empty;
    public string? Uri { get; init; }
    public string? PostedBy { get; init; }
    public DateTimeOffset Time { get; init; }
    public int Score { get; init; }
    public int CommentCount { get; init; }
}
