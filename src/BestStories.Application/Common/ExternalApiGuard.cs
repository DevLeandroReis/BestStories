using BestStories.Application.Exceptions;

namespace BestStories.Application.Common;

public static class ExternalApiGuard
{
    public static async Task<Result<T>> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return Result<T>.Ok(await operation());
        }
        catch (ExternalApiTimeoutException)
        {
            return Result<T>.Fail(
                "The request to Hacker News timed out. Please try again.",
                ErrorCode.ExternalApiTimeout);
        }
        catch (ExternalApiRateLimitedException)
        {
            return Result<T>.Fail(
                "Hacker News is rate limiting requests. Please try again later.",
                ErrorCode.ExternalApiRateLimited);
        }
        catch (ExternalApiUnavailableException)
        {
            return Result<T>.Fail(
                "Hacker News is currently unavailable. Please try again later.",
                ErrorCode.ExternalApiUnavailable);
        }
        catch (ExternalApiException)
        {
            return Result<T>.Fail(
                "An error occurred communicating with Hacker News. Please try again.",
                ErrorCode.ExternalApiError);
        }
    }
}
