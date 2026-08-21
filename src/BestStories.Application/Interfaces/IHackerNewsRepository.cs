using BestStories.Domain.Entities;

namespace BestStories.Application.Interfaces;

public interface IHackerNewsRepository
{
    Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Story?>> GetStoriesByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Proactively reloads the full working set from Hacker News and replaces the cached copy.
    /// Lets the hosting layer keep data warm on a fixed schedule, so the volume of calls made to
    /// Hacker News is driven by that schedule rather than by inbound request volume.
    /// </summary>
    /// <returns>How many stories were successfully refreshed.</returns>
    Task<int> RefreshAsync(CancellationToken cancellationToken = default);
}
