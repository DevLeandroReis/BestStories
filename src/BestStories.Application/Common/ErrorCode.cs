namespace BestStories.Application.Common;

public enum ErrorCode
{
    None,
    Validation,
    ExternalApiTimeout,
    ExternalApiUnavailable,
    ExternalApiRateLimited,
    ExternalApiError
}
