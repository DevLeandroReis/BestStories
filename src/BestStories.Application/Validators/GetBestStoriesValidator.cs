using BestStories.Application.Settings;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace BestStories.Application.Validators;

public sealed class GetBestStoriesValidator : AbstractValidator<int>
{
    public GetBestStoriesValidator(IOptions<HackerNewsApplicationSettings> settings)
    {
        var maxCount = settings.Value.MaxStoriesCount;
        RuleFor(n => n)
            .GreaterThan(0)
            .WithMessage("Count must be a positive integer.")
            .LessThanOrEqualTo(maxCount)
            .WithMessage($"Count cannot exceed {maxCount}.");
    }
}
