namespace BestStories.Application.Settings;

public sealed record HackerNewsApplicationSettings
{
    /// <summary>
    /// Upper bound accepted for <c>n</c>. Matches the documented maximum size of the Hacker News
    /// best-stories list; requests above this are rejected before any external call is made.
    /// </summary>
    public int MaxStoriesCount { get; init; } = 500;
}
