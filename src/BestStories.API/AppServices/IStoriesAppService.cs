using BestStories.API.DTOs;
using BestStories.Application.Common;

namespace BestStories.API.AppServices;

public interface IStoriesAppService
{
    Task<Result<IReadOnlyList<StoryResponse>>> GetBestStoriesAsync(int count, CancellationToken cancellationToken = default);
}
