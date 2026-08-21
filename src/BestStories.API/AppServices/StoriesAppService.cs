using BestStories.API.DTOs;
using BestStories.API.Mappings;
using BestStories.Application.Common;
using BestStories.Application.UseCases;

namespace BestStories.API.AppServices;

public sealed class StoriesAppService(GetBestStoriesUseCase getBestStoriesUseCase) : IStoriesAppService
{
    public async Task<Result<IReadOnlyList<StoryResponse>>> GetBestStoriesAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        var result = await getBestStoriesUseCase.ExecuteAsync(count, cancellationToken);
        return result.Map(stories => stories.ToResponseList());
    }
}
