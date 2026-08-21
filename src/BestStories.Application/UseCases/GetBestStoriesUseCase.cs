using BestStories.Application.Common;
using BestStories.Application.Interfaces;
using BestStories.Application.Validators;
using BestStories.Domain.Entities;
using FluentValidation.Results;

namespace BestStories.Application.UseCases;

public sealed class GetBestStoriesUseCase(
    IHackerNewsRepository repository,
    GetBestStoriesValidator validator)
{
    public async Task<Result<IReadOnlyList<Story>>> ExecuteAsync(int count, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(count, cancellationToken);
        if (!validation.IsValid)
            return ToValidationFailure(validation);

        return await ExternalApiGuard.ExecuteAsync(() => FetchStoriesAsync(count, cancellationToken));
    }

    private async Task<IReadOnlyList<Story>> FetchStoriesAsync(int count, CancellationToken cancellationToken)
    {
        var ids = await repository.GetBestStoryIdsAsync(cancellationToken);
        if (ids.Count == 0)
            return [];

        // Every candidate is scored, not just a prefix of the list. Hacker News does not document
        // beststories.json as being ordered by score, so ranking a subset would quietly depend on
        // an implementation detail that could change. The full set is bounded (at most 500 IDs) and
        // kept warm in cache, so this costs one dictionary lookup per story on the request path.
        var stories = await repository.GetStoriesByIdsAsync(ids, cancellationToken);

        return SelectTopByScore(stories, count);
    }

    private static IReadOnlyList<Story> SelectTopByScore(IReadOnlyList<Story?> stories, int count) =>
        [.. stories
            .OfType<Story>()
            .OrderByDescending(story => story.Score)
            .Take(count)];

    private static Result<IReadOnlyList<Story>> ToValidationFailure(ValidationResult validation)
    {
        var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
        return Result<IReadOnlyList<Story>>.Fail(errors, ErrorCode.Validation);
    }
}
