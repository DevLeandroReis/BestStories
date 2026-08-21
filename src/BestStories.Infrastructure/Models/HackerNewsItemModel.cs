namespace BestStories.Infrastructure.Models;

internal sealed class HackerNewsItemModel
{
    public int Id { get; init; }
    public string? Title { get; init; }
    public string? Url { get; init; }
    public string? By { get; init; }
    public long Time { get; init; }
    public int Score { get; init; }
    public int Descendants { get; init; }
    public string? Type { get; init; }
    public bool Deleted { get; init; }
    public bool Dead { get; init; }

    /// <summary>
    /// True for items that should never surface in the response: removed or flagged posts,
    /// and any item that is not a story (the best-stories list is documented to contain only
    /// stories, but the item endpoint can return comments, jobs and polls).
    /// </summary>
    public bool IsUnavailable =>
        Deleted || Dead || (Type is not null && !string.Equals(Type, "story", StringComparison.OrdinalIgnoreCase));
}
